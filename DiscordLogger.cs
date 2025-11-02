using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using MCGalaxy;
using MCGalaxy.Config;
using MCGalaxy.Events.ServerEvents;
using MCGalaxy.Modules.Relay.Discord;
using MCGalaxy.Tasks;

namespace VeryPlugins
{
    public class DiscordLoggerPlugin : Plugin
    {
        public override string MCGalaxy_Version { get { return "1.9.5.2"; } }
        public override string name { get { return "DiscordLogger"; } }
        public override string creator { get { return "icanttellyou"; } }

        public static DiscordLoggerConfig config = new DiscordLoggerConfig();

        public override void Load(bool auto)
        {
            OnConfigUpdatedEvent.Register(OnConfigUpdated, Priority.Low);
            OnConfigUpdated();
            
            DiscordLogger.Init();
        }
        
        public override void Unload(bool auto)
        {
            OnConfigUpdatedEvent.Unregister(OnConfigUpdated);
            DiscordLogger.Dispose();
        }

        static void OnConfigUpdated()
        {
            if (!File.Exists("plugins/discordlogger.properties")) config.Save("plugins");

            config.Load("plugins");
        }
    }
    
    public class DiscordLoggerConfig
    {
        [ConfigString("logs-channel", "General", "", true)]
        public string LogsChannelID = "";
        [ConfigInt("flush-delay", "Logging", 2000)]
        public int FlushDelay = 2000;
        [ConfigInt("payload-delay", "Logging", 500)]
        public int PayloadDelay = 500;

        static ConfigElement[] cfg;
        public void Load(string path)
        {
            if (cfg == null) cfg = ConfigElement.GetAll(typeof(DiscordLoggerConfig));
            ConfigElement.ParseFile(cfg, path + "/discordlogger.properties", this);
        }

        public void Save(string path)
        {
            if (cfg == null) cfg = ConfigElement.GetAll(typeof(DiscordLoggerConfig));
            using (StreamWriter w = FileIO.CreateGuarded(path + "/discordlogger.properties"))
            {
                w.WriteLine("# This file contains settings for configuring the Discord logger plugin.");
                w.WriteLine("# logs-channel - The ID of the logs channel");
                w.WriteLine("# flush-delay - How often should lines be flushed");
                w.WriteLine("# payload-delay - How often should messages be created and edited");
                w.WriteLine();

                ConfigElement.Serialise(cfg, w, this);
            }
        }
    }
    
    public class DiscordLogger
    {
        const int ADDITIONAL_LENGTH = 4 * 2;

        static bool flushingPayloads;
        static bool disposed;
        static string curString = "";
        static string lastMessageID = "";
        
        static readonly object logLock = new object();
        static Queue<string> cache = new Queue<string>();
        static Queue<DiscordApiMessage> payloads = new Queue<DiscordApiMessage>();
        static SchedulerTask logTask;
        static SchedulerTask payloadTask;
        
        public static void Init() 
        {
            if (!DiscordPlugin.Bot.Enabled)
                return;
            
            Logger.LogHandler += LogMessage;
            
            logTask = Server.MainScheduler.QueueRepeat(Flush, null,
                                                       TimeSpan.FromMilliseconds(DiscordLoggerPlugin.config.FlushDelay));
            payloadTask = Server.MainScheduler.QueueRepeat(FlushPayloads, null,
                                                       TimeSpan.FromMilliseconds(DiscordLoggerPlugin.config.PayloadDelay));
        }
        
        public static void Dispose() 
        {
            if (disposed) return;
            disposed = true;
            Server.MainScheduler.Cancel(logTask);
            Server.MainScheduler.Cancel(payloadTask);
            
            lock (logLock) 
                cache.Clear();
        }
        
        static void LogMessage(LogType type, string message) 
        {
            if (string.IsNullOrEmpty(message)) return;
            if (!Server.Config.FileLogging[(int)type]) return;
            
            if (type == LogType.Error) 
            {
                message = "!!!Error! See " + FileLogger.ErrorLogPath + " for more information.";
            }
            
            string now = DateTime.Now.ToString("(HH:mm:ss) ");
            lock (logLock) cache.Enqueue(now + Colors.Strip(message));
        }
        
        static void Flush(SchedulerTask task)
        {
            if (!DiscordPlugin.Bot.Connected || DiscordLoggerPlugin.config.LogsChannelID.Length == 0) return;
            
            bool flush = false;
            bool toCreate = false;
            
            string dump = "";
            int remaining = 2000 - ADDITIONAL_LENGTH - curString.Length;
            while (cache.Count > 0)
            {
                flush = true;
                string line;
                
                lock (logLock)
                    line = cache.Dequeue();

                if (remaining - line.Length - 1 < 0)
                {
                    AppendOrCreateMessage(dump);
                    dump = "";
                    remaining = 2000 - ADDITIONAL_LENGTH;
                    
                    if (toCreate)
                        CreateMessage(line + "\n");
                    
                    toCreate = true;
                    flush = false;
                }
                
                dump += line + "\n";
                remaining -= line.Length + 1;
            }
            
            if (flush && !toCreate)
                AppendOrCreateMessage(dump);
            else if (toCreate)
                CreateMessage(dump);
        }

        static void FlushPayloads(SchedulerTask task)
        {
            if (flushingPayloads)
                return;
            
            flushingPayloads = true;
            while (payloads.Count > 0)
            {
                DiscordApiMessageWaiter msg = new DiscordApiMessageWaiter(payloads.Dequeue());
                DiscordPlugin.Bot.Send(msg);
                
                while (!msg.isReady)
                    Thread.Sleep(1);
                
                Thread.Sleep(DiscordLoggerPlugin.config.PayloadDelay);
            }
            
            flushingPayloads = false;
        }

        static void AppendOrCreateMessage(string message)
        {
            if (lastMessageID.Length == 0)
            {
                CreateMessage(message);
                return;
            }
            
            curString += message;
            DiscordApiMessage msg = new ChannelEditMessage(DiscordLoggerPlugin.config.LogsChannelID, lastMessageID, "```\n" + curString + "\n```");
            payloads.Enqueue(msg);
        }

        static void CreateMessage(string message)
        {
            curString = message;
            DiscordApiMessage msg = new ChannelSendMessageResp(DiscordLoggerPlugin.config.LogsChannelID, "```\n" + message + "\n```", resp =>
            {
                JsonObject read = new JsonReader(resp).Parse() as JsonObject;
                if (read == null)
                    return;

                lastMessageID = (string)read["id"];
            });
            
            payloads.Enqueue(msg);
        }
    }
    
    public class ChannelEditMessage : DiscordApiMessage
    {
        readonly string Contents;
        
        public ChannelEditMessage(string channelID, string messageID, string contents)
        {
            Path = string.Format("/channels/{0}/messages/{1}", channelID, messageID);
            Method = "PATCH";
            Contents = contents;
        }

        public override JsonObject ToJson()
        {
            return new JsonObject()
            {
                {
                    "content", Contents
                }
            };
        }
    }
    
    public delegate void OnResponse(string message);
    
    public class ChannelSendMessageResp : ChannelSendMessage
    {
        readonly OnResponse OnResponse;

        public ChannelSendMessageResp(string channelID, string message, OnResponse resp) : base(channelID, message)
        {
            OnResponse = resp;
        }

        public override void ProcessResponse(string response)
        {
            OnResponse(response);
        }

        public override bool CombineWith(DiscordApiMessage prior)
        {
            return false;
        }
    }
    
    public class DiscordApiMessageWaiter : DiscordApiMessage
    {
        public bool isReady;
        readonly DiscordApiMessage msg;

        public DiscordApiMessageWaiter(DiscordApiMessage msg)
        {
            Path = msg.Path;
            Method = msg.Method;
            
            this.msg = msg;
        }
        
        public override JsonObject ToJson()
        {
            return msg.ToJson();
        }
        
        public override bool CombineWith(DiscordApiMessage prior)
        {
            return false; 
        }

        public override void OnRequest(HttpWebRequest req)
        {
            msg.OnRequest(req);
        }

        public override void ProcessResponse(string response)
        {
            msg.ProcessResponse(response);
            isReady = true;
        }
    }
}
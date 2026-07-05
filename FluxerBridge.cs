//reference System.dll
/*
    Copyright 2015-2024 MCGalaxy
        
    Dual-licensed under the Educational Community License, Version 2.0 and
    the GNU General Public License, Version 3 (the "Licenses"); you may
    not use this file except in compliance with the Licenses. You may
    obtain a copy of the Licenses at
    
    https://opensource.org/license/ecl-2-0/
    https://www.gnu.org/licenses/gpl-3.0.html
    
    Unless required by applicable law or agreed to in writing,
    software distributed under the Licenses are distributed on an "AS IS"
    BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express
    or implied. See the Licenses for the specific language governing
    permissions and limitations under the Licenses.
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using MCGalaxy;
using MCGalaxy.Config;
using MCGalaxy.Events;
using MCGalaxy.Events.PlayerEvents;
using MCGalaxy.Events.ServerEvents;
using MCGalaxy.Games;
using MCGalaxy.Modules.Relay;
using MCGalaxy.Modules.Relay.Discord;
using MCGalaxy.Network;
using MCGalaxy.Tasks;
using MCGalaxy.Util;

namespace VeryPlugins
{
    public sealed class FluxerConfig 
    {
        [ConfigBool("enabled", "General", false)]
        public bool Enabled;

        [ConfigString("api-path", "General", "https://api.fluxer.app", false)]
        public string ApiPath = "https://api.fluxer.app";
        [ConfigInt("api-version", "General", 1)]

        public int ApiVersion = 1;
        [ConfigString("bot-token", "General", "", true)]
        public string BotToken = "";
        [ConfigBool("use-nicknames", "General", true)]
        public bool UseNicks = true;
        
        [ConfigString("channel-ids", "General", "", true)]
        public string Channels = "";
        [ConfigString("op-channel-ids", "General", "", true)]
        public string OpChannels = "";
        [ConfigString("ignored-user-ids", "General", "", true)]
        public string IgnoredUsers = "";
        
        [ConfigBool("presence-enabled", "Presence (Status)", true)]
        public bool PresenceEnabled = true;
        [ConfigEnum("presence-status", "Presence (Status)", PresenceStatus.online, typeof(PresenceStatus))]
        public PresenceStatus Status = PresenceStatus.online;        
        [ConfigString("status-message", "Presence (Status)", "{PLAYERS} players online")]
        public string StatusMessage = "{PLAYERS} players online";
        
        [ConfigBool("can-mention-users", "Mentions", true)]
        public bool CanMentionUsers = true;
        [ConfigBool("can-mention-roles", "Mentions", true)]
        public bool CanMentionRoles = true;
        [ConfigBool("can-mention-everyone", "Mentions", false)]
        public bool CanMentionHere;
        
        [ConfigInt("embed-color", "Embeds", 9758051)]
        public int EmbedColor = 9758051;
        [ConfigBool("embed-show-game-statuses", "Embeds", true)]
        public bool EmbedGameStatuses = true;
        
        public const string PROPS_PATH = "properties/fluxerbot.properties";
        static ConfigElement[] cfg;
        
        public void Load() {
            // create default config file
            if (!File.Exists(PROPS_PATH)) Save();

            if (cfg == null) cfg = ConfigElement.GetAll(typeof(FluxerConfig));
            ConfigElement.ParseFile(cfg, PROPS_PATH, this);
        }
        
        public void Save() {
            if (cfg == null) cfg = ConfigElement.GetAll(typeof(FluxerConfig));
            
            using (StreamWriter w = FileIO.CreateGuarded(PROPS_PATH)) 
            {
                w.WriteLine("# Fluxer relay bot configuration");
                w.WriteLine();
                ConfigElement.Serialise(cfg, w, this);
            }
        }
    }
    
    public enum PresenceStatus { online, dnd, idle, invisible }
    
    public sealed class FluxerPlugin : Plugin 
    {
        public override string name { get { return "FluxerRelay"; } }
        public override string MCGalaxy_Version { get { return "1.9.5.3"; } }
        public override string creator { get { return "icanttellyou"; } }
        
        public static FluxerConfig Config = new FluxerConfig();
        public static FluxerBot Bot = new FluxerBot();
        
        static Command cmdFluxerBot   = new CmdFluxerBot();
        static Command cmdFluxerCtrls = new CmdFluxerControllers();
        
        public override void Load(bool startup) {
            Server.EnsureDirectoryExists("text/fluxer");
            Command.Register(cmdFluxerBot);
            Command.Register(cmdFluxerCtrls);

            Bot.Config = Config;
            Bot.ReloadConfig();
            Bot.Connect();
            OnConfigUpdatedEvent.Register(OnConfigUpdated, Priority.Low);
        }
        
        public override void Unload(bool shutdown) {
            Command.Unregister(cmdFluxerBot, cmdFluxerCtrls);
            
            OnConfigUpdatedEvent.Unregister(OnConfigUpdated);
            Bot.Disconnect("Disconnecting Fluxer bot");
        }
        
        void OnConfigUpdated() { Bot.ReloadConfig(); }
    }
    
    sealed class CmdFluxerBot : RelayBotCmd 
    {
        public override string name { get { return "FluxerBot"; } }
        protected override RelayBot Bot { get { return FluxerPlugin.Bot; } }
    }
    
    sealed class CmdFluxerControllers : BotControllersCmd 
    {
        public override string name { get { return "FluxerControllers"; } }
        protected override RelayBot Bot { get { return FluxerPlugin.Bot; } }
    }
    
    /// <summary> Implements a basic web client for sending messages to the Fluxer API </summary>
    /// <remarks> https://docs.fluxer.app/ </remarks>
    public class FluxerApiClient : AsyncWorker<DiscordApiMessage>
    {
        public string Token;
        public string Host;
        readonly object msgLock = new object();
        
        DiscordApiMessage GetNextRequest() {
            if (queue.Count == 0) return null;
            DiscordApiMessage first = queue.Dequeue();
            
            // try to combine messages to minimise API calls
            while (queue.Count > 0) {
                DiscordApiMessage next = queue.Peek();
                if (!next.CombineWith(first)) break;
                queue.Dequeue();
            }
            return first;
        }
        
        protected override string ThreadName { get { return "Fluxer-ApiClient"; } }
        protected override void HandleNext() {
            DiscordApiMessage msg = null;
            
            lock (queueLock) { msg = GetNextRequest(); }
            if (msg == null) { WaitForWork(); return;  }
            
            lock (msgLock) { ProcessMessage(msg); }
        }
        
        public void SendNow(DiscordApiMessage msg) {
            lock (msgLock) { ProcessMessage(msg); }
        }
        
        void ProcessMessage(DiscordApiMessage msg) {
            WebResponse res = null;
            
            for (int retry = 0; retry < 10; retry++)
            {
                try {
                    HttpWebRequest req = HttpUtil.CreateRequest(Host + msg.Path);
                    req.Method         = msg.Method;
                    req.Headers[HttpRequestHeader.Authorization] = "Bot " + Token;
                    
                    JsonObject obj = msg.ToJson();
                    if (obj != null) {
                        req.ContentType = "application/json";
                        string data = Json.SerialiseObject(obj);
                        HttpUtil.SetRequestData(req, Encoding.UTF8.GetBytes(data));
                    }
                    
                    msg.OnRequest(req);
                    res = req.GetResponse();
                    
                    string resp = HttpUtil.GetResponseText(res);
                    msg.ProcessResponse(resp);
                    break;
                } catch (WebException ex) {
                    bool canRetry = HandleErrorResponse(ex, msg, retry);
                    HttpUtil.DisposeErrorResponse(ex);
                    
                    if (!canRetry) return;
                } catch (Exception ex) {
                    LogError(ex, msg);
                    return;
                }
            }
            
            // Avoid triggering HTTP 429 error if possible
            string remaining = res.Headers["X-RateLimit-Remaining"];
            if (remaining == "1") SleepForRetryPeriod(res);            
        }
        
        static bool HandleErrorResponse(WebException ex, DiscordApiMessage msg, int retry) {
            string err = HttpUtil.GetErrorResponse(ex);
            HttpStatusCode status = GetStatus(ex);
            
            // 429 errors simply require retrying after sleeping for a bit
            if (status == (HttpStatusCode)429) {
                SleepForRetryPeriod(ex.Response);
                return true;
            }
            
            // 500 errors might be temporary Fluxer outage, so still retry a few times
            if (status >= (HttpStatusCode)500 && status <= (HttpStatusCode)504) {
                LogWarning(ex);
                LogResponse(err);
                return retry < 2;
            }
            
            // If unable to reach Fluxer at all, immediately give up
            if (ex.Status == WebExceptionStatus.NameResolutionFailure) {
                LogWarning(ex);
                return false;
            }
            
            // May be caused by connection dropout/reset, so still retry a few times
            if (ex.InnerException is IOException) {
                LogWarning(ex);
                return retry < 2;
            }
            
            LogError(ex, msg);
            LogResponse(err);
            return false;
        }
        
        
        static HttpStatusCode GetStatus(WebException ex) {
            if (ex.Response == null) return 0;
            return ((HttpWebResponse)ex.Response).StatusCode;
        }
        
        static void LogError(Exception ex, DiscordApiMessage msg) {
            string target = "(" + msg.Method + " " + msg.Path + ")";
            Logger.LogError("Error sending request to Fluxer API " + target, ex);
        }
        
        static void LogWarning(Exception ex) {
            Logger.Log(LogType.Warning, "Error sending request to Fluxer API - " + ex.Message);
        }
        
        static void LogResponse(string err) {
            if (string.IsNullOrEmpty(err)) return;
            
            // The service might return <html>..</html> responses for internal server errors
            //  most of this is useless content, so just truncate these particular errors
            if (err.Length > 200) err = err.Substring(0, 200) + "...";
            
            Logger.Log(LogType.Warning, "Fluxer API returned: " + err);
        }
        
        
        static void SleepForRetryPeriod(WebResponse res) {
            string resetAfter = res.Headers["X-RateLimit-Reset-After"];
            string retryAfter = res.Headers["Retry-After"];
            float delay;
            
            if (NumberUtils.TryParseSingle(resetAfter, out delay) && delay > 0) {
                // Prefer Fluxer "X-RateLimit-Reset-After" (millisecond precision)
            } else if (NumberUtils.TryParseSingle(retryAfter, out delay) && delay > 0) {
                // Fallback to general "Retry-After" header
            } else {
                // No recommended retry delay.. 30 seconds is a good bet
                delay = 30;
            }

            Logger.Log(LogType.SystemActivity, "Fluxer bot ratelimited! Trying again in {0} seconds..", delay);
            Thread.Sleep(TimeSpan.FromSeconds(delay + 0.5f));
        }
    }
    
    public class FluxerSession 
    { 
        public string ID, LastSeq;
    }
    public delegate string FluxerGetStatus();
    public delegate void GatewayEventCallback(string eventName, JsonObject data);
    
    /// <summary> Implements a basic websocket for communicating with Fluxer's gateway </summary>
    /// <remarks> https://docs.fluxer.app/gateway/overview/ </remarks>
    public class FluxerWebsocket : ClientWebSocket 
    {       
        /// <summary> Authorisation token for the bot account </summary>
        public string Token;
        public string Host;
        
        public bool CanReconnect = true, SentIdentify;
        public FluxerSession Session;
        
        /// <summary> Whether presence support is enabled </summary>
        public bool Presence = true;
        /// <summary> Presence status (E.g. online) </summary>
        public PresenceStatus Status;
        /// <summary> Callback function to retrieve the activity status message </summary>
        public FluxerGetStatus GetStatus;
        
        /// <summary> Callback invoked when a ready event has been received </summary>
        public Action<JsonObject> OnReady;
        /// <summary> Callback invoked when a resumed event has been received </summary>
        public Action<JsonObject> OnResumed;
        /// <summary> Callback invoked when a message created event has been received </summary>
        public Action<JsonObject> OnMessageCreate;
        /// <summary> Callback invoked when a channel created event has been received </summary>
        public Action<JsonObject> OnChannelCreate;
        /// <summary> Callback invoked when a gateway event has been received </summary>
        public GatewayEventCallback OnGatewayEvent;
        
        readonly object sendLock = new object();
        SchedulerTask heartbeat;
        TcpClient client;
        SslStream stream;
        bool readable;

        const int OPCODE_DISPATCH        = 0;
        const int OPCODE_HEARTBEAT       = 1;
        const int OPCODE_IDENTIFY        = 2;
        const int OPCODE_STATUS_UPDATE   = 3;
        const int OPCODE_VOICE_STATE_UPDATE = 4;
        const int OPCODE_RESUME          = 6;
        const int OPCODE_REQUEST_SERVER_MEMBERS = 8;
        const int OPCODE_INVALID_SESSION = 9;
        const int OPCODE_HELLO           = 10;
        const int OPCODE_HEARTBEAT_ACK   = 11;
        
        
        public FluxerWebsocket(string apiPath) {
            path = apiPath;
        }
        
        // stubs
        public override bool LowLatency { set { } }
        public override IPAddress IP { get { return null; } }
        
        public void Connect() {
            client = new TcpClient();
            client.Connect(Host, 443);
            readable = true;

            stream   = HttpUtil.WrapSSLStream(client.GetStream(), Host);
            protocol = this;
            Init();
        }
        
        protected override void WriteCustomHeaders() {
            WriteHeader("Authorization: " + Token);
            WriteHeader("Host: " + Host);
        }
        
        public override void Close() {
            readable = false;
            Server.Heartbeats.Cancel(heartbeat);
            try {
                client.Close();
            } catch {
                // ignore errors when closing socket
            }
        }
        
        const int REASON_INVALID_TOKEN = 4004;
        
        protected override void OnDisconnected(int reason) {
            SentIdentify = false;
            if (readable) Logger.Log(LogType.SystemActivity, "Fluxer relay bot closing: " + reason);
            Close();

            if (reason == REASON_INVALID_TOKEN) {
                CanReconnect = false;
                throw new InvalidOperationException("Fluxer relay: Invalid bot token provided - unable to connect");
            }
        }
        
        
        public void ReadLoop() {
            byte[] data = new byte[4096];
            readable = true;

            while (readable) 
            {
                int len = stream.Read(data, 0, 4096);
                if (len == 0) throw new IOException("stream.Read returned 0");
                
                HandleReceived(data, len);
            }
        }
        
        protected override void HandleData(byte[] data, int len) {
            string value   = Encoding.UTF8.GetString(data, 0, len);
            JsonReader ctx = new JsonReader(value);
            JsonObject obj = (JsonObject)ctx.Parse();
            if (obj == null) return;
            
            int opcode = NumberUtils.ParseInt32((string)obj["op"]);
            DispatchPacket(opcode, obj);
        }
        
        void DispatchPacket(int opcode, JsonObject obj) {
            if (opcode == OPCODE_DISPATCH) {
                HandleDispatch(obj);
            } else if (opcode == OPCODE_HELLO) {
                HandleHello(obj);
            } else if (opcode == OPCODE_INVALID_SESSION) {
                // See notes at https://discord.com/developers/docs/topics/gateway#resuming
                //  (note that in this implementation, if resume fails, the bot just
                //   gives up altogether instead of trying to resume again later)
                Session.ID      = null;
                Session.LastSeq = null;
                
                Logger.Log(LogType.Warning, "Fluxer relay: Resuming failed, trying again in 5 seconds");
                Thread.Sleep(5 * 1000);
                Identify();
            }
        }
        
        
        void HandleHello(JsonObject obj) {
            JsonObject data = (JsonObject)obj["d"];
            string interval = (string)data["heartbeat_interval"];            
            int msInterval  = NumberUtils.ParseInt32(interval);
            
            heartbeat = Server.Heartbeats.QueueRepeat(SendHeartbeat, null, 
                                          TimeSpan.FromMilliseconds(msInterval));
            Identify();
        }
        
        void HandleDispatch(JsonObject obj) {
            // update last sequence number
            object sequence;
            if (obj.TryGetValue("s", out sequence)) 
                Session.LastSeq = (string)sequence;
            
            string eventName = (string)obj["t"];
            
            object rawData;            
            obj.TryGetValue("d", out rawData);
            JsonObject data = rawData as JsonObject;
            
            if (eventName == "READY") {
                HandleReady(data);
                OnReady(data);
            } else if (eventName == "RESUMED") {
                OnResumed(data);
            } else if (eventName == "MESSAGE_CREATE") {
                OnMessageCreate(data);
            } else if (eventName == "CHANNEL_CREATE") {
                OnChannelCreate(data);
            }
            OnGatewayEvent(eventName, data);
        }
        
        void HandleReady(JsonObject data) {
            object session;
            if (data.TryGetValue("session_id", out session)) 
                Session.ID = (string)session;
        }
        
        
        public void SendMessage(int opcode, JsonObject data) {
            JsonObject obj = new JsonObject()
            {
                { "op", opcode },
                { "d",  data }
            };
            SendMessage(obj);
        }
        
        public void SendMessage(JsonObject obj) {
            string str = Json.SerialiseObject(obj);
            Send(Encoding.UTF8.GetBytes(str), SendFlags.None);
        }
        
        protected override void SendRaw(byte[] data, SendFlags flags) {
            lock (sendLock) stream.Write(data);
        }
        
        void SendHeartbeat(SchedulerTask task) {
            JsonObject obj = new JsonObject();
            obj["op"] = OPCODE_HEARTBEAT;
            
            if (Session.LastSeq != null) {
                obj["d"] = NumberUtils.ParseInt32(Session.LastSeq);
            } else {
                obj["d"] = null;
            }
            SendMessage(obj);
        }
        
        public void Identify() {
            if (Session.ID != null && Session.LastSeq != null) {
                SendMessage(OPCODE_RESUME,   MakeResume());
            } else {
                SendMessage(OPCODE_IDENTIFY, MakeIdentify());
            }
            SentIdentify = true;
        }
        
        public void UpdateStatus() {
            JsonObject data = MakePresence();
            SendMessage(OPCODE_STATUS_UPDATE, data);
        }
        
        JsonObject MakeResume() {
            return new JsonObject()
            {
                { "token",      Token },
                { "session_id", Session.ID },
                { "seq",        NumberUtils.ParseInt32(Session.LastSeq) }
            };
        }
        
        JsonObject MakeIdentify() {
            JsonObject props = new JsonObject()
            {
                { "os",      "linux" },
                { "browser", Server.SoftwareName },
                { "device",  Server.SoftwareName }
            };
            
            return new JsonObject()
            {
                { "token",      Token },
                { "properties", props },
                { "presence",   MakePresence() }
            };
        }
        
        JsonObject MakePresence() {
            if (!Presence) return null;

            string expires = DateTime.UtcNow.AddMinutes(10).ToString("o", CultureInfo.InvariantCulture);
            JsonObject activity = new JsonObject()
            {
                { "text",       GetStatus() },
                { "expires_at", expires },
                { "emoji_id",   null },
                { "emoji_name", null }
            };
            return new JsonObject()
            {
                { "custom_status", activity },
                { "status",        Status.ToString() },
                { "afk",           false }
            };
        }
    }
    
    sealed class FluxerUser : RelayUser
    {
        public string ReferencedUser;
        
        public override string GetMessagePrefix() {
            if (string.IsNullOrEmpty(ReferencedUser))
                return "";
            
            return "@" + ReferencedUser + " ";
        }
    }
    
    public class FluxerBot : RelayBot
    {
        protected FluxerApiClient api;
        protected FluxerWebsocket socket;
        protected FluxerSession session;
        protected string botUserID;
        
        Dictionary<string, byte> channelTypes = new Dictionary<string, byte>();
        const byte CHANNEL_DIRECT = 0;
        const byte CHANNEL_TEXT   = 1;

        List<string> filter_triggers = new List<string>();
        List<string> filter_replacements = new List<string>();
        JsonArray allowed;

        public override string RelayName { get { return "Fluxer"; } }
        public override bool Enabled     { get { return Config.Enabled; } }
        public override string UserID    { get { return botUserID; } }
        public FluxerConfig Config;
        
        TextFile replacementsFile = new TextFile("text/fluxer/replacements.txt",
                                        "// This file is used to replace words/phrases sent to Fluxer",
                                        "// Lines starting with // are ignored",
                                        "// Lines should be formatted like this:",
                                        "// example:http://example.org",
                                        "// That would replace 'example' in messages sent to Fluxer with 'http://example.org'");
        
        private Uri _wsUri;
        private int _msgLenLimit;
        
        protected override bool CanReconnect {
            get { return canReconnect && (socket == null || socket.CanReconnect); }
        }
        
        protected override void DoConnect()
        {
            socket = new FluxerWebsocket(_wsUri.PathAndQuery + "?v=" + Config.ApiVersion + "&encoding=json");
            socket.Session   = session;
            socket.Token     = Config.BotToken;
            socket.Host      = _wsUri.Host;
            socket.Presence  = Config.PresenceEnabled;
            socket.Status    = Config.Status;
            socket.GetStatus = GetStatusMessage;
            
            socket.OnReady         = HandleReadyEvent;
            socket.OnResumed       = HandleResumedEvent;
            socket.OnMessageCreate = HandleMessageEvent;
            socket.OnChannelCreate = HandleChannelEvent;
            socket.OnGatewayEvent  = HandleGatewayEvent;
            socket.Connect();
        }
                
        // mono wraps exceptions from reading in an AggregateException, e.g:
        //   * AggregateException - One or more errors occurred.
        //      * ObjectDisposedException - Cannot access a disposed object.
        // .NET sometimes wraps exceptions from reading in an IOException, e.g.:
        //   * IOException - The read operation failed, see inner exception.
        //      * ObjectDisposedException - Cannot access a disposed object.
        static Exception UnpackError(Exception ex) {
            if (ex.InnerException is ObjectDisposedException)
                return ex.InnerException;
            if (ex.InnerException is IOException)
                return ex.InnerException;
            
            // TODO can we ever get an IOException wrapping an IOException?
            return null;
        }
        
        protected override void DoReadLoop() {
            try {
                socket.ReadLoop();
            } catch (Exception ex) {
                Exception unpacked = UnpackError(ex);
                // throw a more specific exception if possible
                if (unpacked != null) throw unpacked;
                
                // rethrow original exception otherwise
                throw;
            }
        }
        
        protected override void DoDisconnect(string reason) {
            try {
                socket.Disconnect();
            } catch {
                // no point logging disconnect failures
            }
        }
        
        
        public override void ReloadConfig() {
            Config.Load();
            base.ReloadConfig();
            LoadReplacements();
            
            if (!Config.CanMentionHere) return;
            Logger.Log(LogType.Warning, "can-mention-everyone option is enabled in {0}, " +
                       "which allows pinging all users on Fluxer from in-game. " +
                       "It is recommended that this option be disabled.", FluxerConfig.PROPS_PATH);
        }
        
        protected override void UpdateConfig() {
            Channels     = Config.Channels.SplitComma();
            OpChannels   = Config.OpChannels.SplitComma();
            IgnoredUsers = Config.IgnoredUsers.SplitComma();
            
            UpdateAllowed();
            LoadBannedCommands();
        }
        
        void UpdateAllowed() {
            JsonArray mentions = new JsonArray();
            if (Config.CanMentionUsers) mentions.Add("users");
            if (Config.CanMentionRoles) mentions.Add("roles");
            if (Config.CanMentionHere)  mentions.Add("everyone");
            allowed = mentions;
        }
        
        void LoadReplacements() {
            replacementsFile.EnsureExists();            
            string[] lines = replacementsFile.GetText();
            
            filter_triggers.Clear();
            filter_replacements.Clear();
            
            ChatTokens.LoadTokens(lines, (phrase, replacement) => 
                                  {
                                      filter_triggers.Add(phrase);
                                      filter_replacements.Add(DiscordUtils.MarkdownToSpecial(replacement));
                                  });
        }
        
        public override void LoadControllers() {
            Controllers = PlayerList.Load("text/fluxer/controllers.txt");
        }
        
        
        FluxerUser ExtractUser(JsonObject data) {
            JsonObject author = (JsonObject)data["author"];
            
            FluxerUser user = new FluxerUser();
            user.Nick = GetNick(data) ?? GetUser(author);
            user.ID   = (string)author["id"];
            
            user.ReferencedUser = ExtractReferencedUser(data);
            return user;
        }
        
        string GetNick(JsonObject data) {
            if (!Config.UseNicks) return null;
            object raw;
            if (!data.TryGetValue("member", out raw)) return null;
            
            // Make sure this is really a member object first
            JsonObject member = raw as JsonObject;
            if (member == null) return null;
            
            member.TryGetValue("nick", out raw);
            return raw as string;
        }
        
        string GetUser(JsonObject author) {
            // User's chosen display name (configurable)
            object name = null;
            author.TryGetValue("global_name", out name);
            if (name != null) return (string)name;

            return (string)author["username"];
        }
        
        string ExtractReferencedUser(JsonObject data) {
            object refMsgRaw;
            data.TryGetValue("referenced_message", out refMsgRaw);
            
            JsonObject refMsgData = refMsgRaw as JsonObject;
            if (refMsgData == null) return null;
            
            object authorRaw;
            refMsgData.TryGetValue("author", out authorRaw);
            if (authorRaw == null) return null;
            
            return GetUser((JsonObject)authorRaw);
        }
        
        
        void HandleMessageEvent(JsonObject data) {
            FluxerUser user = ExtractUser(data);
            // ignore messages from self
            if (user.ID == botUserID) return;
            
            string channel = (string)data["channel_id"];
            string message = (string)data["content"];
            byte type;
            
            if (!channelTypes.TryGetValue(channel, out type)) {
                type = GuessChannelType(data);
                // channel is definitely a text/normal channel
                if (type == CHANNEL_TEXT) channelTypes[channel] = type;
            }
            
            if (type == CHANNEL_DIRECT) {
                HandleDirectMessage(user,  channel, message);
            } else {
                HandleChannelMessage(user, channel, message);
                PrintAttachments(user, data, channel);
            }
        }
        
        void PrintAttachments(RelayUser user, JsonObject data, string channel) {
            object raw;
            if (!data.TryGetValue("attachments", out raw)) return;
            
            JsonArray list = raw as JsonArray;
            if (list == null) return;
            
            foreach (object entry in list) 
            {
                JsonObject attachment = entry as JsonObject;
                if (attachment == null) continue;
                
                string url = (string)attachment["url"];
                HandleChannelMessage(user, channel, url);
            }
        }
        
        
        void HandleChannelEvent(JsonObject data) {
            string channel = (string)data["id"];
            string type    = (string)data["type"];

            // 1 = direct/private message channel type
            if (type == "1") channelTypes[channel] = CHANNEL_DIRECT;
        }

        byte GuessChannelType(JsonObject data) {
            // As per discord's documentation:
            //  "The member object exists in MESSAGE_CREATE and MESSAGE_UPDATE
            //   events from text-based guild channels, provided that the
            //   author of the message is not a webhook"
            if (data.ContainsKey("member")) return CHANNEL_TEXT;

            // As per discord's documentation
            //  "You can tell if a message is generated by a webhook by
            //   checking for the webhook_id on the message object."
            if (data.ContainsKey("webhook_id")) return CHANNEL_TEXT;

            // TODO are there any other cases to consider?
            return CHANNEL_DIRECT; // unknown
        }

        
        void HandleReadyEvent(JsonObject data) {
            JsonObject user = (JsonObject)data["user"];
            botUserID       = (string)user["id"];
            HandleResumedEvent(data);
        }
        
        void HandleResumedEvent(JsonObject data) {
            // May not be null when reconnecting
            if (api == null) {
                InitApi();
            }
            OnReady();
        }
        
        void HandleGatewayEvent(string eventName, JsonObject data) {
            OnFluxerGatewayEventReceivedEvent.Call(this, eventName, data);
        }


        static bool IsEscaped(char c) {
            // To match Discord: \a --> \a, \* --> *
            return (c >  ' ' && c <= '/') || (c >= ':' && c <= '@') 
                || (c >= '[' && c <= '`') || (c >= '{' && c <= '~');
        }        
        protected override string ParseMessage(string input) {
            StringBuilder sb = new StringBuilder(input);
            SimplifyCharacters(sb);
            
            // remove variant selector character used with some emotes
            sb.Replace("\uFE0F", "");
            
            // unescape \ escaped characters
            //  -1 in case message ends with a \
            int length = sb.Length - 1;
            for (int i = 0; i < length; i++) 
            {
                if (sb[i] != '\\') continue;
                if (!IsEscaped(sb[i + 1])) continue;
                
                sb.Remove(i, 1); length--;
            }
            
            StripMarkdown(sb);
            return sb.ToString();
        }
        
        static void StripMarkdown(StringBuilder sb) {
            // TODO proper markdown parsing
            sb.Replace("**", "");
        }


        readonly object updateLocker = new object();
        volatile bool updateScheduled;
        DateTime nextUpdate;

        public void UpdateFluxerStatus() {
            TimeSpan delay = default(TimeSpan);
            DateTime now   = DateTime.UtcNow;

            // websocket gets disconnected with code 4008 if try to send too many updates too quickly
            lock (updateLocker) {
                // status update already pending?
                if (updateScheduled) return;
                updateScheduled = true;

                // slowdown if sending too many status updates
                if (nextUpdate > now) delay = nextUpdate - now;
            }
            
            Server.MainScheduler.QueueOnce(DoUpdateStatus, null, delay);
        }

        void DoUpdateStatus(SchedulerTask task) {
            DateTime now = DateTime.UtcNow;
            // OK to queue next status update now
            lock (updateLocker) {
                updateScheduled = false;
                nextUpdate      = now.AddSeconds(0.5);
                // ensures status update can't be sent more than once every 0.5 seconds
            }

            FluxerWebsocket s = socket;
            // websocket gets disconnected with code 4003 if tries to send data before identifying
            //  https://discord.com/developers/docs/topics/opcodes-and-status-codes
            if (s == null || !s.SentIdentify) return;

            try { s.UpdateStatus(); } catch { }
        }

        string GetStatusMessage() {
            fakeGuest.group     = Group.DefaultRank;
            List<Player> online = PlayerInfo.GetOnlineCanSee(fakeGuest, fakeGuest.Rank); 

            string numOnline = NumberUtils.StringifyInt(online.Count);
            return Config.StatusMessage.Replace("{PLAYERS}", numOnline);
        }

        private void InitApi()
        {
            api = new FluxerApiClient();
            api.Token = Config.BotToken;
            api.Host  = Config.ApiPath + "/v" + Config.ApiVersion;
            api.RunAsync();
        }
        
        protected override void OnStart() {
            InitApi();
            
            //TODO: maybe rework this
            SendNow(new WellKnownDetailsMessage(resp =>
            {
                JsonObject read = new JsonReader(resp).Parse() as JsonObject;
                if (read == null)
                    return;
                
                JsonObject endpoints = read["endpoints"] as JsonObject;
                _wsUri = new Uri((string)endpoints["gateway"]);
                
                JsonObject limits = read["limits"] as JsonObject;
                JsonArray rules = limits["rules"] as JsonArray;

                foreach (JsonObject rule in rules)
                {
                    object value;
                    if (!rule.TryGetValue("overrides", out value))
                        continue;
                    
                    JsonObject overrides = value as JsonObject;
                    
                    object @override;
                    if (overrides.TryGetValue("max_message_length", out @override))
                    {
                        string maxMsgLength = (string)@override;
                        _msgLenLimit = int.Parse(maxMsgLength);
                    }
                }
            }));
            
            FluxerSession s = new FluxerSession();
            session   = s;
            
            base.OnStart();            
            OnPlayerConnectEvent.Register(HandlePlayerConnect, Priority.Low);
            OnPlayerDisconnectEvent.Register(HandlePlayerDisconnect, Priority.Low);
            OnPlayerActionEvent.Register(HandlePlayerAction, Priority.Low);
        }
        
        protected override void OnStop() {
            socket = null;
            if (api != null) {
                api.StopAsync();
                api = null;
            }
            base.OnStop();
            
            OnPlayerConnectEvent.Unregister(HandlePlayerConnect);
            OnPlayerDisconnectEvent.Unregister(HandlePlayerDisconnect);
            OnPlayerActionEvent.Unregister(HandlePlayerAction);
        }
        
        void HandlePlayerConnect(Player p) { UpdateFluxerStatus(); }
        void HandlePlayerDisconnect(Player p, string reason) { UpdateFluxerStatus(); }
        
        void HandlePlayerAction(Player p, PlayerAction action, string message, bool stealth) {
            if (action != PlayerAction.Hide && action != PlayerAction.Unhide) return;
            UpdateFluxerStatus();
        }
        
        
        /// <summary> Asynchronously sends a message to the Fluxer API </summary>
        public void Send(DiscordApiMessage msg) {
            // can be null in gap between initial connection and ready event received
            if (api != null) api.QueueAsync(msg);
        }
        
        /// <summary> Synchronously sends a message to the Fluxer API </summary>
        /// <remarks> Use with CAUTION, as synchronous messages can be sent out of order 
        /// (i.e. may be sent before pending async messages) </remarks>
        public void SendNow(DiscordApiMessage msg) {
            if (api != null) api.SendNow(msg);
        }
        
        protected override void DoSendMessage(string channel, string message) {
            message = ConvertMessage(message);
            
            // Break up message into multiple parts in an extremely rare case
            for (int offset = 0; offset < message.Length; offset += _msgLenLimit)
            {
                int partLen = Math.Min(message.Length - offset, _msgLenLimit);
                string part = message.Substring(offset, partLen);
                
                ChannelSendMessage msg = new ChannelSendMessage(channel, part);
                msg.Allowed = allowed;
                Send(msg);
            }
        }
        
        /// <summary> Formats a message for displaying on Fluxer </summary>
        /// <example> Escapes markdown characters such as _ and * </example>
        protected string ConvertMessage(string message) {
            message = ConvertMessageCommon(message);
            message = Colors.StripUsed(message);
            message = DiscordUtils.EscapeMarkdown(message);
            message = DiscordUtils.SpecialToMarkdown(message);
            return message;
        }
        
        protected override string PrepareMessage(string message) {
            // allow uses to do things like replacing '+' with ':green_square:'
            for (int i = 0; i < filter_triggers.Count; i++) 
            {
                message = message.Replace(filter_triggers[i], filter_replacements[i]);
            }
            return message;
        }
        
        
        // all users are already verified by Fluxer
        protected override bool CheckController(string userID, ref string error) { return true; }
        
        protected override string UnescapeFull(Player p) {
            return BOLD + base.UnescapeFull(p) + BOLD;
        }        
        protected override string UnescapeNick(Player p) {
            return BOLD + base.UnescapeNick(p) + BOLD;
        }
        
        protected override void MessagePlayers(RelayPlayer p) {
            ChannelSendEmbed embed = new ChannelSendEmbed(p.ChannelID);
            int total;
            List<OnlineListEntry> entries = PlayerInfo.GetOnlineList(p, p.Rank, out total);
            
            embed.Color  = Config.EmbedColor;
            embed.Title  = string.Format("{0} player{1} currently online",
                                        total, total.Plural());
            
            foreach (OnlineListEntry e in entries) 
            {
                if (e.players.Count == 0) continue;
                
                embed.Fields.Add(
                    ConvertMessage(FormatRank(e)),
                    ConvertMessage(FormatPlayers(p, e))
                );
            }
            
            AddGameStatus(embed);
            OnFluxerSendingWhoEmbedEvent.Call(this, p.User, ref embed);
            Send(embed);
        }
        
        static string FormatPlayers(Player p, OnlineListEntry e) {
            return e.players.Join(pl => FormatNick(p, pl), ", ");
        }
        
        static string FormatRank(OnlineListEntry e) {
            return string.Format(UNDERLINE + "{0}" + UNDERLINE + " (" + CODE + "{1}" + CODE + ")",
                                 e.group.GetFormattedName(), e.players.Count);
        }

        static string FormatNick(Player p, Player pl) {
            string flags  = OnlineListEntry.GetFlags(pl);
            string format;
            
            if (flags.Length > 0) {
                format = BOLD + "{0}" + BOLD + ITALIC + "{2}" + ITALIC + " (" + CODE + "{1}" + CODE + ")";
            } else {
                format = BOLD + "{0}" + BOLD                           + " (" + CODE + "{1}" + CODE + ")";
            }
            return string.Format(format, p.FormatNick(pl), 
                                 // level name must not have _ escaped as the level name is in a code block -
                                 //  otherwise the escaped "\_" actually shows as "\_" instead of "_" 
                                 pl.level.name.Replace('_', DiscordUtils.UNDERSCORE),
                                 flags);
        }
        
        void AddGameStatus(ChannelSendEmbed embed) {
            if (!Config.EmbedGameStatuses) return;
            
            StringBuilder sb = new StringBuilder();
            IGame[] games    = IGame.RunningGames.Items;
            
            foreach (IGame game in games)
            {
                Level lvl = game.Map;
                if (!game.Running || lvl == null) continue;
                sb.Append(BOLD + game.GameName + BOLD + " is running on " + lvl.name + "\n");
            }
            
            if (sb.Length == 0) return;
            embed.Fields.Add("Running games", ConvertMessage(sb.ToString()));
        }
        
        
        public const string UNDERLINE     = DiscordUtils.UNDERLINE;
        public const string BOLD          = DiscordUtils.BOLD;
        public const string ITALIC        = DiscordUtils.ITALIC;
        public const string CODE          = DiscordUtils.CODE;
        public const string SPOILER       = DiscordUtils.SPOILER;
        public const string STRIKETHROUGH = DiscordUtils.STRIKETHROUGH;
    }
    
    public class WellKnownDetailsMessage : DiscordApiMessage
    {
        private Action<string> _onResp;
        
        public WellKnownDetailsMessage(Action<string> onResp)
        {
            Method = "GET";
            Path = "/.well-known/fluxer";

            _onResp = onResp;
        }
        
        public override JsonObject ToJson()
        { 
            return null;
        }

        public override void ProcessResponse(string response)
        {
            _onResp(response);
        }
    }
    
    public delegate void OnFluxerSendingWhoEmbed(FluxerBot bot, RelayUser user, ref ChannelSendEmbed embed);
    /// <summary> Called when sending an embed response to a .who message from Fluxer </summary>
    public sealed class OnFluxerSendingWhoEmbedEvent : IEvent<OnFluxerSendingWhoEmbed> 
    { 
        public static void Call(FluxerBot bot, RelayUser user, ref ChannelSendEmbed embed) {
            IEvent<OnFluxerSendingWhoEmbed>[] items = handlers.Items;
            for (int i = 0; i < items.Length; i++) 
            {
                try {
                    items[i].method(bot, user, ref embed);
                } catch (Exception ex) {
                    LogHandlerException(ex, items[i]);
                }
            }
        }
    }
    
    public delegate void OnFluxerGatewayEventReceived(FluxerBot bot, string eventName, JsonObject data);
    /// <summary> Called when a gateway event has been received from Fluxer </summary>
    public sealed class OnFluxerGatewayEventReceivedEvent : IEvent<OnFluxerGatewayEventReceived> 
    { 
        public static void Call(FluxerBot bot, string eventName, JsonObject data) {
            IEvent<OnFluxerGatewayEventReceived>[] items = handlers.Items;
            for (int i = 0; i < items.Length; i++) 
            {
                try {
                    items[i].method(bot, eventName, data);
                } catch (Exception ex) {
                    LogHandlerException(ex, items[i]);
                }
            }
        }
    }
}
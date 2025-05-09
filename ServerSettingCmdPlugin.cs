using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using MCGalaxy;

namespace VeryPlugins
{
    public class ServerSettingCmdPlugin : Plugin
    {
        public override string name { get { return "ServerSettingCmdPlugin"; } }
        public override string creator { get { return "icanttellyou"; } }
        public override string MCGalaxy_Version { get { return "1.9.4.9"; } }

        internal Command css = new CmdChangeServerSetting();
        internal Command vss = new CmdViewServerSettings();

        public override void Load(bool auto)
        {
            Command.Register(css);
            Command.Register(vss);
        }

        public override void Unload(bool auto)
        {
            Command.Unregister(css);
            Command.Unregister(vss);
        }

        public static T GetStaticValue<T>(Type type, string field)
        {
            object value = type.GetField(field, BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            return value != null ? (T) value : default(T);
        }
    }

    public struct TempLogger
    {
        readonly List<Tuple<LogType, string>> lines;
        readonly Thread checkThread;
        
        public bool Logged { get { return lines.Count > 0; } }

        public TempLogger(Thread checkThread)
        {
            this.checkThread = checkThread;
            lines = new List<Tuple<LogType, string>>();
        }

        void OnLog(LogType type, string message)
        {
            if (Thread.CurrentThread != checkThread) return;
            lines.Add(new Tuple<LogType, string>(type, message));
        }

        public void Setup()
        {
            Logger.LogHandler += OnLog;
        }

        public void Cleanup()
        {
            Logger.LogHandler -= OnLog;
        }

        public void DumpLinesToPlayer(Player p)
        {
            foreach (Tuple<LogType, string> line in lines)
            {
                string prefix;

                // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
                switch (line.Item1)
                {
                    case LogType.Warning:
                        prefix = "&e[WARN] ";
                        break;
                    case LogType.Error:
                        prefix = "&W[ERROR] ";
                        break;
                    default:
                        prefix = "";
                        break;
                }
                
                p.Message(prefix + line.Item2);
            }
            
            lines.Clear();
        }
    }

    public class CmdChangeServerSetting : Command2
    {
        public override string name { get { return "ChangeServerSetting"; } }
        public override string type { get { return CommandTypes.Other; } }
        public override string shortcut { get { return "css"; } }
        public override LevelPermission defaultRank { get { return LevelPermission.Owner; } }

        public override void Use(Player p, string message)
        {
            string[] parts = message.SplitSpaces(2);
            if (parts.Length < 2) { Help(p); return; }

            string setting = parts[0];
            string value = parts[1];
            
            ConfigElement[] serverConfig = ServerSettingCmdPlugin.GetStaticValue<ConfigElement[]>(typeof(Server), "serverConfig");
            int elemI = Array.FindIndex(serverConfig, e => e.Attrib.Name.CaselessEq(setting));

            if (elemI != -1)
            {
                TempLogger tmp = new TempLogger(Thread.CurrentThread);
                tmp.Setup();
                
                ConfigElement elem = serverConfig[elemI];
                elem.Field.SetValue(Server.Config, elem.Attrib.Parse(value));
                SrvProperties.Save();
                
                p.Message("Changed setting &T{0} &Sto &T{1}", setting, value);

                if (tmp.Logged)
                {
                    p.Message("There have been warnings changing the setting, see below messages.");
                    tmp.DumpLinesToPlayer(p);
                }
                
                tmp.Cleanup();
            }
            else
            {
                p.Message("&W{0} is not a valid server setting!", setting);
            }
        }

        public override void Help(Player p)
        {
            p.Message("&T/ChangeServerSetting [setting] [value]");
            p.Message("&HChanges values in server config.");
            p.Message("&HTo view all server properties use &T/ViewServerSettings");
        }
    }

    public class CmdViewServerSettings : Command2
    {
        public override string name { get { return "ViewServerSettings"; } }
        public override string type { get { return CommandTypes.Other; } }
        public override string shortcut { get { return "vss"; } }
        public override LevelPermission defaultRank { get { return LevelPermission.Owner; } }

        public override void Use(Player p, string message)
        {
            ConfigElement[] serverConfig = ServerSettingCmdPlugin.GetStaticValue<ConfigElement[]>(typeof(Server), "serverConfig");
            Dictionary<string, List<ConfigElement>> sections = new Dictionary<string, List<ConfigElement>>();
            
            foreach (ConfigElement elem in serverConfig) 
            {
                List<ConfigElement> members;
                if (!sections.TryGetValue(elem.Attrib.Section, out members)) {
                    members = new List<ConfigElement>();
                    sections[elem.Attrib.Section] = members;
                }
                members.Add(elem);
            }

            foreach (var kvp in sections) 
            {
                p.Message("{0} settings:", kvp.Key);
                foreach (ConfigElement elem in kvp.Value)
                {
                    p.Message("&T{0}&S: {1}", elem.Attrib.Name, elem.Attrib.Serialise(elem.Field.GetValue(Server.Config)));
                }
            }
        }

        public override void Help(Player p)
        {
            p.Message("&T/ViewServerSettings");
            p.Message("&HOutputs all server settings, based on category, and the values of the settings");
        }
    }
}
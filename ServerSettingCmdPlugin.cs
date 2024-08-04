using System;
using System.Collections.Generic;
using System.Reflection;
using MCGalaxy;

namespace VeryPlugins
{
    public class ServerSettingCmdPlugin : Plugin
    {
        public override string name => "ServerSettingCmdPlugin";
        public override string creator => "icanttellyou";
        public override string MCGalaxy_Version => "1.9.4.9";

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

    public class CmdChangeServerSetting : Command2
    {
        public override string name => "ChangeServerSetting";
        public override string type => CommandTypes.Other;
        public override string shortcut => "css";
        public override LevelPermission defaultRank => LevelPermission.Owner;

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
                ConfigElement elem = serverConfig[elemI];
                elem.Field.SetValue(Server.Config, elem.Attrib.Parse(value));
                SrvProperties.Save();

                p.Message("Changed setting &T{0} &Sto &T{1}", setting, value);
                p.Message("&WYou may need to check the server logs if the setting changed properly!");
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
            p.Message("&WWarning: &HFeedback on values provided is only output to the logs due to limitations");
            p.Message("&HTo view all server properties use &T/ViewServerSettings");
        }
    }

    public class CmdViewServerSettings : Command2
    {
        public override string name => "ViewServerSettings";
        public override string type => CommandTypes.Other;
        public override string shortcut => "vss";
        public override LevelPermission defaultRank => LevelPermission.Owner;

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
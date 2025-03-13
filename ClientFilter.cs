//reference System.dll
//reference Newtonsoft.Json.dll
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using MCGalaxy;
using MCGalaxy.Events.PlayerEvents;
using MCGalaxy.Events.ServerEvents;
using MCGalaxy.Network;
using MCGalaxy.Util;
using Newtonsoft.Json;

namespace GoodOldLavaSurvival
{
    public class ClientFilter : Plugin
    {
        public override string name { get { return "ClientFilter"; } }
        public override string creator { get { return "icanttellyou"; } }
        public override string MCGalaxy_Version { get { return "1.9.5.0"; } }
        
        static Settings settings;

        public override void Load(bool auto)
        {
            settings = new Settings();
            LoadFilterConfig();
            OnConfigUpdatedEvent.Register(LoadFilterConfig, Priority.Low);
            OnPlayerFinishConnectingEvent.Register(CheckClient, Priority.High);
        }
        
        public override void Unload(bool auto)
        {
            settings = null;
            OnConfigUpdatedEvent.Unregister(LoadFilterConfig);
            OnPlayerFinishConnectingEvent.Unregister(CheckClient);
        }

        static void CheckClient(Player p)
        {
            if (Server.vip.Contains(p.name))
                return;

            IGameSession session = p.Session;
            if (!session.hasCpe && settings.requireCPE)
            {
                p.Leave(settings.cpeRequiredMessage, true);
                p.cancelconnecting = true;
                return;
            }

            foreach (KeyValuePair<string, int> kvp in settings.requiredCPEExtensions)
            {
                string extName = kvp.Key;
                int extVer = kvp.Value;
                
                if (session.Supports(extName, extVer)) continue;
                
                p.Leave(string.Format("Your client does not support {0} v{1}!", extName, extVer), true);
                p.cancelconnecting = true;
                return;
            }
            
            string clientName = session.ClientName();
            foreach (Settings.Block block in settings.blockedClients)
            {
                if (block.Clients == null)
                {
                    Logger.Log(LogType.Error, "The blocked client list for an entry is null!");
                    continue;
                }

                foreach (string client in block.Clients)
                {
                    if (!clientName.CaselessContains(client)) continue;

                    Logger.Log(LogType.SuspiciousActivity, "{0} tried joining using {1}", p.truename, clientName);
                    Chat.Message(ChatScope.Perms, "&W" + p.truename + " tried joining using " + clientName, Chat.OpchatPerms, null, true);
                    p.Leave(block.Warning, true);
                    p.cancelconnecting = true;
                    return;
                }
            }
        }

        static void LoadFilterConfig() { settings.Load(); }
        
        [JsonObject(MemberSerialization.OptIn)]
        public class Settings
        {
            internal static readonly JsonSerializerSettings jsonSettings = new JsonSerializerSettings()
            {
                DefaultValueHandling = DefaultValueHandling.Populate,
                NullValueHandling = NullValueHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Ignore,
            };
            
            [JsonObject(MemberSerialization.OptIn)]
            public struct Block
            {
                [JsonProperty("warnMessage")]
                public string Warning;
                [JsonProperty("clients")]
                public List<string> Clients;
            }

            [JsonProperty("blacklistedClients")]
            public List<Block> blockedClients = new List<Block>();
            [JsonProperty("requiredCPEExtensions")]
            public Dictionary<string, int> requiredCPEExtensions = new Dictionary<string, int>();
            [JsonProperty("requireCPE")] [DefaultValue(false)]
            public bool requireCPE;
            [JsonProperty("cpeRequiredMessage")] [DefaultValue("Please enable enhanced mode in the launcher!")]
            public string cpeRequiredMessage = "Please enable enhanced mode in the launcher!";

            const string FILTER_FILE = "extra/clientFilter.json";

            protected internal void Load()
            {
                if (!File.Exists(FILTER_FILE))
                {
                    Logger.Log(LogType.SystemActivity, FILTER_FILE + " does not exist, creating");
                    Save();
                }

                JsonConvert.PopulateObject(File.ReadAllText(FILTER_FILE), this, jsonSettings);
            }

            protected internal void Save()
            {
                string ser = JsonConvert.SerializeObject(this, Formatting.Indented, jsonSettings);
                File.WriteAllText(FILTER_FILE, ser);
            }
        }
    }
}
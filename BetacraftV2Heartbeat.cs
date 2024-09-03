//reference System.dll
//reference System.Core.dll
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using MCGalaxy;
using MCGalaxy.Authentication;
using MCGalaxy.Config;
using MCGalaxy.Events.ServerEvents;
using MCGalaxy.Network;

namespace VeryPlugins
{
    public sealed class BetacraftV2HeartbeatPlugin : Plugin
    {
        private const string CONFIG_FOLDER = "plugins/bcv2";

        public override string MCGalaxy_Version => "1.9.4.9";
        public override string name => "BetacraftV2Heartbeat";
        public override string creator => "icanttellyou";

        public override void Load(bool auto)
        {
            OnConfigUpdatedEvent.Register(OnConfigUpdated, Priority.Low);
            OnConfigUpdated();
        }

        public override void Unload(bool auto)
        {
            OnConfigUpdatedEvent.Unregister(OnConfigUpdated);
            AuthService.Services.RemoveAll(s => s.URL.CaselessEq("Mojang"));
            Heartbeat.Heartbeats.RemoveAll(h => h is BetacraftV2Heartbeat);
        }

        HeartbeatConfig bcv2Config = new HeartbeatConfig();
        void OnConfigUpdated()
        {
            if (!Directory.Exists(CONFIG_FOLDER)) Directory.CreateDirectory(CONFIG_FOLDER);
            if (!File.Exists(CONFIG_FOLDER + "/heartbeat.properties")) bcv2Config.Save(CONFIG_FOLDER);

            bcv2Config.Load(CONFIG_FOLDER);

            AuthService mojangService = AuthService.GetOrCreate("Mojang", false);
            mojangService.MojangAuth = true;
            mojangService.NameSuffix = bcv2Config.NameSuffix;
            mojangService.SkinPrefix = bcv2Config.SkinPrefix;

            if (!Heartbeat.Heartbeats.Any(h => h is BetacraftV2Heartbeat) && Server.Config.Public)
            {
                Heartbeat bcv2Beat = new BetacraftV2Heartbeat(bcv2Config);
                Heartbeat.Register(bcv2Beat);
            }
        }
    }

    public class PlayerUtils
    {
        public static JsonArray FilterOnlyCanSee(LevelPermission plRank, IEnumerable<Player> players)
        {
            JsonArray list = new JsonArray();
            foreach (Player pl in players)
            {
                if (!pl.hidden || plRank >= pl.hideRank) list.Add(new JsonObject() {
                    { "username", pl.name }
                });
            }
            return list;
        }
    }

    public class BetacraftV2Heartbeat : Heartbeat
    {
        public HeartbeatConfig config;

        public BetacraftV2Heartbeat(HeartbeatConfig conf)
        {
            URL = "https://api.betacraft.uk/v2/server_update";
            config = conf;
        }

        string proxyUrl;
        bool checkedAddr;

        void CheckAddress()
        {
            string hostUrl = "";
            checkedAddr = true;

            try
            {
                hostUrl = GetHost();
                proxyUrl = EnsureIPv4Url(hostUrl);
            }
            catch (Exception ex)
            {
                Logger.LogError("Error retrieving DNS information for " + hostUrl, ex);
            }
        }

        private bool fullHeartbeat = true;
        protected override string GetHeartbeatData()
        {
            if (fullHeartbeat) {
                fullHeartbeat = false;

                return Json.SerialiseObject(new JsonObject()
                {
                    { "private_key", config.PrivateKey },
                    { "name", Server.Config.Name },
                    { "game_version", config.ConnectVersion },
                    { "v1_version", config.ConnectV1Version },
                    { "protocol", config.ConnectProtocol },
                    { "socket", config.ConnectAddress },
                    { "category", "classic" },
                    { "description", config.Description },
                    { "is_public", Server.Config.Public },
                    { "max_players", Server.Config.MaxPlayers },
                    { "online_players", PlayerInfo.NonHiddenUniqueIPCount() },
                    { "software", new JsonObject() {
                        { "name", System.Text.RegularExpressions.Regex.Replace(Server.SoftwareName, "&.", "") },
                        { "version", Server.Version }
                    } },
                    { "online_mode", Server.Config.VerifyNames },
                    { "players", PlayerUtils.FilterOnlyCanSee(Group.DefaultRank.Permission,
                                            PlayerInfo.Online.Items) }
                });
            } else {
                URL = "https://api.betacraft.uk/v2/server_update_ping";
                return Json.SerialiseObject(new JsonObject()
                {
                    { "private_key", config.PrivateKey },
                    { "socket", config.ConnectAddress },
                    { "online_players", PlayerInfo.NonHiddenUniqueIPCount() },
                    { "players", PlayerUtils.FilterOnlyCanSee(Group.DefaultRank.Permission,
                                            PlayerInfo.Online.Items) }
                });
            }
            
        }

        protected override void OnFailure(string response)
        {
            if (!string.IsNullOrEmpty(response))
                Logger.Log(LogType.Warning, "[BCV2] Error: Server responded with " + response);
        }

        protected override void OnRequest(HttpWebRequest request)
        {
            request.ContentType = "application/json";

            if (!checkedAddr) CheckAddress();

            if (proxyUrl == null) return;
            request.Proxy = new WebProxy(proxyUrl);
        }

        protected override void OnResponse(WebResponse response)
        {
            string text = HttpUtil.GetResponseText(response);

            JsonObject responseJson = (JsonObject) new JsonReader(text).Parse();

            if (responseJson.ContainsKey("error") && bool.Parse((string)responseJson["error"])) {
                string message = (string) responseJson["message"];

                Logger.Log(LogType.Warning, "[BCV2] Error: Server responded with " + message);
            }
        }
    }

    public class HeartbeatConfig
    {
        [ConfigString("description", "Information", "Come and join the fun!", true)]
        public string Description = "Come and join the fun!";
        [ConfigString("connect-version", "Version", "c0.30-c-1900")]
        public string ConnectVersion = "c0.30-c-1900";
        [ConfigString("connect-v1_version", "Version", "c0.30-c-1900")]
        public string ConnectV1Version = "c0.30-c-1900";
        [ConfigString("connect-protocol", "Version", "classic_7")]
        public string ConnectProtocol = "classic_7";
        [ConfigString("name-suffix", "Auth service", "", true)]
        public string NameSuffix = "";
        [ConfigString("skin-prefix", "Auth service", "https://minotar.net/skin/", true)]
        public string SkinPrefix = "https://minotar.net/skin/";
        [ConfigString("private-key", "Heartbeat authentication", "", true)]
        public string PrivateKey = "";
        [ConfigString("connect-address", "Heartbeat authentication", "", true)]
        public string ConnectAddress = "";

        static ConfigElement[] cfg;
        public void Load(string path)
        {
            if (cfg == null) cfg = ConfigElement.GetAll(typeof(HeartbeatConfig));
            ConfigElement.ParseFile(cfg, path + "/heartbeat.properties", this);

            // reduce lag on startup
            if (ConnectAddress == "") {
                ConnectAddress = GetExternalIP() + ":" + Server.Config.Port;
            }
        }

        public void Save(string path)
        {
            if (cfg == null) cfg = ConfigElement.GetAll(typeof(HeartbeatConfig));
            using (StreamWriter w = new StreamWriter(path + "/heartbeat.properties"))
            {
                w.WriteLine("# This file contains settings for configuring the Betacraft V2 heartbeat.");
                w.WriteLine("# description - The description that shows up on the server list");
                w.WriteLine("# connect-version - Version to use by default for connecting to the server");
                w.WriteLine("# connect-v1_version - Version to use by Betacraft V1 launcher for connecting to the server");
                w.WriteLine("# connect-protocol - The protocol version to use for the server.");
                w.WriteLine("# name-suffix - See description in authservices.properties");
                w.WriteLine("# skin-prefix - See description in authservices.properties");
                w.WriteLine("# private-key - Private key used to identify with the heartbeat server");
                w.WriteLine("# connect-address - Address of your server");
                w.WriteLine();

                if (ConnectAddress == "") {
                    ConnectAddress = GetExternalIP() + ":" + Server.Config.Port;
                }

                ConfigElement.Serialise(cfg, w, this);
            }
        }

        static string externalIP = null;
        static string GetExternalIP()
        {
            if (externalIP != null) return externalIP;

            try
            {
                externalIP = HttpUtil.LookupExternalIP();
            }
            catch (Exception ex)
            {
                Logger.LogError("Retrieving external IP", ex);
            }
            return externalIP;
        }
    }
}

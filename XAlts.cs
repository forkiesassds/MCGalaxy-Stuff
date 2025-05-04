using System;
using System.Collections.Generic;
using System.Reflection;
using MCGalaxy;
using MCGalaxy.Commands.Moderation;
using MCGalaxy.Events.PlayerEvents;
using MCGalaxy.SQL;

namespace GoodOldLavaSurvival.Moderation
{
    public class XAlts : Plugin
    {
        public override string name { get { return "XAlts"; } }
        public override string creator { get { return "icanttellyou"; } }
        public override string MCGalaxy_Version { get { return "1.9.4.9"; } }

        static readonly Command cmd = new CmdXAlts();
        
        public override void Load(bool auto)
        {
            Init();

            OnPlayerStartConnectingEvent.Register(HandleOnPlayerStartConnecting, Priority.Low);
            Command.Register(cmd);
        }

        public override void Unload(bool auto)
        {
            OnPlayerStartConnectingEvent.Unregister(HandleOnPlayerStartConnecting);
            Command.Unregister(cmd);
        }

        static void HandleOnPlayerStartConnecting(Player p, string mpPass)
        {
            if (!p.verifiedName) return;
            
            DateTime now = DateTime.Now;
            int changed = Database.UpdateRows("LinkedIPs", "LastSeen=@0", "WHERE Name=@1 AND IP=@2", now, p.name, p.ip);

            if (changed == 0)
            {
                Database.AddRow("LinkedIPs", "Name, IP, FirstSeen, LastSeen", p.name, p.ip, now, now);
            }

            List<string> ips = LookupLinkedIPs(p.name);
            int linkedNameCount = CountLinkedPlayers(p.name, ips);

            if (linkedNameCount > 0 && !p.cancelconnecting)
            {
                Chat.MessageOps(string.Format("{0} has {1} linked account{2} and {3} linked IP{4}",
                                              p.truename, linkedNameCount, linkedNameCount > 1 ? "s" : "",
                                              ips.Count, ips.Count > 1 ? "s" : ""));
            }
        }
        
        #region Database
        static readonly ColumnDesc[] schema = {
            new ColumnDesc("Name", ColumnType.Char, 20),
            new ColumnDesc("IP", ColumnType.Char, 15),
            //TODO: make use of these in the future
            new ColumnDesc("FirstSeen", ColumnType.DateTime),
            new ColumnDesc("LastSeen", ColumnType.DateTime)
        };

        struct ImportData
        {
            public string name, ip;
            public DateTime lastLogin;
        }

        static void Init()
        {
            if (Database.TableExists("LinkedIPs")) return;
            
            Logger.Log(LogType.Warning, "[XAlts] Database table doesn't exist! Creating new table.");
            Database.CreateTable("LinkedIPs", schema);

            Logger.Log(LogType.SystemActivity, "[XAlts] Importing IPs from the player table.");
            long start = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            SqlTransaction trans = new SqlTransaction();
            List<ImportData> data = new List<ImportData>();

            Database.ReadRows("Players", "Name, IP, LastLogin", record =>
            {
                string name = record.GetText("Name");
                string ip = record.GetText("IP");

                int loginCol = record.GetOrdinal("LastLogin");
                DateTime lastLogin = record.IsDBNull(loginCol) ? DateTime.Now : record.GetDateTime(loginCol);

                ImportData importData;
                importData.name = name;
                importData.ip = ip;
                importData.lastLogin = lastLogin;

                data.Add(importData);
            });

            foreach (ImportData i in data)
            {
                object[] args = { i.name, i.ip, i.lastLogin, i.lastLogin };

                string sql = Database.Backend.AddRowSql("LinkedIPs", "Name, IP, FirstSeen, LastSeen", args.Length);
                trans.Execute(sql, args);
            }

            trans.Commit();

            long end = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            int delta = (int)(end - start);

            Logger.Log(LogType.SystemActivity, "[XAlts] Finished importing! Took {0}", TimeSpan.FromSeconds(delta).Shorten(true));
        }

        public static List<string> LookupLinkedIPs(string name)
        {
            List<string> ips = new List<string>();
            Database.ReadRows("LinkedIPs", "IP", record => ips.Add(record.GetText("IP")), "WHERE Name=@0", name);
            return ips;
        }

        public static List<string> LookupLinkedPlayers(string name, List<string> ips)
        {
            string args = "";
            for (int i = 0; i < ips.Count; i++)
                args += "@" + i + (i < ips.Count - 1 ? "," : "");

            List<string> names = new List<string>();
            
            //I HATE .NET FRAMEWORK
            object[] values = new object[ips.Count + 1];
            ips.ToArray().CopyTo(values, 0);
            values[ips.Count] = name;
            
            Database.ReadRows("LinkedIPs", "DISTINCT Name", record => names.Add(record.GetText("Name")),
                    "WHERE IP IN (" + args + ") AND NOT Name=@" + ips.Count, values);
            return names;
        }

        public static int CountLinkedIPs(string name)
        {
            return Database.CountRows("LinkedIPs", "WHERE Name=@0", name);
        }

        public static int CountLinkedPlayers(string name, List<string> ips)
        {
            string args = "";
            for (int i = 0; i < ips.Count; i++)
                args += "@" + i + (i < ips.Count - 1 ? "," : "");

            int value = 0;
            
            //I HATE .NET FRAMEWORK
            object[] values = new object[ips.Count + 1];
            ips.ToArray().CopyTo(values, 0);
            values[ips.Count] = name;
            
            Database.ReadRows("LinkedIPs", "COUNT(DISTINCT(Name))", record => value = record.GetInt32(0),
                    "WHERE IP IN (" + args + ") AND NOT Name=@" + ips.Count, values);

            return value;
        }
        #endregion
    }
    
    public class CmdXAlts : Command2
    {
        public override string name { get { return "XAlts"; } }
        public override string type { get { return CommandTypes.Moderation; } }
        public override LevelPermission defaultRank { get { return LevelPermission.Operator; } }

        public override void Use(Player p, string message, CommandData data)
        {
            if (message.Length == 0) { Help(p); return; }

            string[] parts = message.SplitSpaces();

            if (!parts[0].StartsWith("ip:"))
            {
                string pName = PlayerUtils.MatchPlayerName(p, parts[0]);
                if (pName == null) return;

                List<string> ips = XAlts.LookupLinkedIPs(pName);
                List<string> names = XAlts.LookupLinkedPlayers(pName, ips);

                string nameStr = "";

                if (names.Count == 0)
                {
                    p.Message("No possible alts for &f{0}", pName);
                }
                else
                {
                    p.Message("All &f{0} &Spossible alts for &f{1}", names.Count, pName);
                    for (int i = 0; i < names.Count; i++)
                    {
                        string altName = names[i];
                        if (altName == pName) continue;
                        bool banned = Group.BannedRank.Players.Contains(altName);

                        nameStr += (banned ? "&8" : "&f") + altName;
                        if (i < names.Count - 1) nameStr += ", ";
                    }
                    p.Message(nameStr);
                }

                if (parts.Length > 1 && parts[1] == "true")
                {
                    string ipStr = "";

                    p.Message("All &f{0} &Spossible IPs for &f{1}", ips.Count, pName);
                    for (int i = 0; i < ips.Count; i++)
                    {
                        string ip = ips[i];
                        bool banned = Server.bannedIP.Contains(ip);

                        ipStr += (banned ? "&8" : "&f") + ip;
                        if (i < ips.Count - 1) ipStr += ", ";
                    }
                    p.Message(ipStr);
                }
                else
                {
                    p.Message("&f{0} &Shas used a total of &f{1} &SIPs.", pName, ips.Count);
                }
            }
            else
            {
                parts[0] = parts[0].Remove(0, 3);

                string ip = AccessorHack.InvokeStaticFunctionReturnable<string>(typeof(ModActionCmd), "FindIP", p, parts[0], "XAlts", "");
                if (ip == null) return;

                List<string> names = XAlts.LookupLinkedPlayers(name, new List<string>() { ip });

                string nameStr = "";

                p.Message("All &f{0} &Spossible logins for &f{1}", names.Count, ip);
                for (int i = 0; i < names.Count; i++)
                {
                    string altName = names[i];
                    bool banned = Group.BannedRank.Players.Contains(altName);

                    nameStr += (banned ? "&8" : "&f") + altName;
                    if (i < names.Count - 1) nameStr += ", ";
                }
                p.Message(nameStr);
            }
        }

        public override void Help(Player p)
        {
            p.Message("&T/XAlts [name]");
            p.Message("Looks up all alts of a player based on previously used IPs");
            p.Message("To lookup just the IP of a player use ip:[ip/name] instead of [name]");
            p.Message("Add \"true\" at the end of the command to show all previously used IPs");
            p.Message("&8name &H- banned player, &8xxx.xxx.xxx.xxx &H- banned IP");
        }
    }

    #region Portions from private code
    static class PlayerUtils
    {
        public static string MatchPlayerName(Player p, string name)
        {
            if (!Formatter.ValidPlayerName(p, name)) return null;
            string match = AccessorHack.InvokeStaticFunctionReturnable<string>(typeof(ModActionCmd), "MatchName", p, name);

            return match;
        }
    }
    
    static class AccessorHack
    {
        public static T InvokeStaticFunctionReturnable<T>(Type type, string method, params object[] args)
        {
            MethodInfo meth = type.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static);
            if (meth == null) return default(T);
            
            object result = meth.Invoke(null, args);
            return result != null ? (T) result : default(T);
        }
    }
    #endregion
}

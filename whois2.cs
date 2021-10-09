// plugin for alternate whois command
using System;
using MCGalaxy;
using MCGalaxy.DB;
using MCGalaxy.Commands;

namespace MCGalaxy.Commands.Info
{
	public class whois2 : Plugin
	{
		public override string name { get { return "whois2"; } }
		public override string MCGalaxy_Version { get { return "1.9.3.4"; } }
		public override string creator { get { return "TomCube"; } }

		public override void Load(bool startup)
		{
			Command.Unregister(Command.Find("whois"));
			Command.Register(new CmdWhois2());
			Command.Register(new CmdWhoisLegacy());
		}
        
		public override void Unload(bool shutdown)
		{
			
		}
        
		public override void Help(Player p)
		{
		
		}
	}
	
	
	//new whois
	public class CmdWhois2 : Command2
	{
		public override string name { get { return "WhoIs"; } }
		public override string shortcut { get { return "i"; } }
		public override string type { get { return CommandTypes.Information; } }
		public override bool UseableWhenFrozen { get { return true; } }
		public override LevelPermission defaultRank { get { return LevelPermission.Banned; } }

		
		public override void Use(Player p, string message, CommandData data)
		{
			int matches;
			string[] args = message.SplitSpaces();
			if (args[0].Length == 0) args[0] = p.name;
			
			Player who = PlayerInfo.FindMatches(p, args[0], out matches);
			
			
			
			if (args[0].Length > 0 && who == null) { 
				bool useless = false;
				
				if (args.Length == 2 && args[1] == "--noplus") useless = true;
				else args[0] = args[0] + "+";
				
				p.Message("Cannot find online player, searching the Database...");
                PlayerData target = PlayerDB.Match(p, args[0]);
                if (target == null) return;
				Group group = Group.GroupIn(target.Name);
				p.Message("&m-[ &f{0}&m(offline) ]-", (string)target.Name.Replace('+', ' '));
				p.Message("&f· &bHas &6{0} &b{1}", target.Money.ToString(), Server.Config.Currency);
				p.Message("&f· &bHas the rank of &g{0}", group.Name);
				p.Message("&f· &bHas logged in &a{0} &btimes", target.Logins);
				p.Message("&f· &bFirst login: &]{0}", target.FirstLogin.ToString("yyyy-MM-dd"));
				p.Message("&f· &bHas spent &d{0} &bon the server", target.TotalTime.ToString().Replace(".", "d ").Replace(":", "h ").TrimEnd('h', ' ', '1', '2', '3', '4', '5', '6', '7', '8', '9') + "m");
				
				
			}
			else {
				
				
				
				p.Message("&m-[ {0} &m(&7{1}&m) ]-", who.ColoredName, who.name);
				p.Message("&f· &bHas &6{0} &b{1}", who.money, Server.Config.Currency);
				p.Message("&f· &bHas the rank of &g{0}", who.Rank);
				
				p.Message("&f· &bHas logged in &a{0} &btimes", who.TimesVisited);
				
				
				TimeSpan timeOnline = DateTime.UtcNow - who.SessionStartTime;
				
				p.Message("&f· &bFirst login: &]{0}", who.FirstLogin.ToString("yyyy-MM-dd"));
				
				p.Message("&f· &bHas spent &d{0} &bon the server, &d{1} &bthis session", who.TotalTime.Shorten(), timeOnline.Shorten());
				//p.Message("&f· &b");
				
				
				
				TimeSpan idleTime = DateTime.UtcNow - who.LastAction;
				if (who.afkMessage != null) { p.Message("&f· &bIdle for {0} (AFK {1}&b)", idleTime.Shorten(), who.afkMessage); }
				else if (idleTime.TotalMinutes >= 1) { p.Message("&f· &bIdle for {0}", idleTime.Shorten()); }
				
				
				bool hasSkin = !who.SkinName.CaselessEq(who.truename);
				bool hasModel = !(who.Model.CaselessEq("humanoid") || who.Model.CaselessEq("human"));
				if (hasSkin) { p.Message("&f· &bHas the skin of &q{0}", who.SkinName); }
				if (hasModel) { p.Message("&f· &bHas the model of &q{0}", who.Model); }
				
			
			}
		}

		// This is for when a player does /Help Whois2
		public override void Help(Player p)
		{
			p.Message("%T/WhoIs [name]");
            p.Message("%HDisplays information about that player.");
            p.Message("%HNote: if you don't want the plus automatically added at the end of an offline player's name, do --noplus");
		}
	}
	
	//old whois
	public sealed class CmdWhoisLegacy : Command2 {
        public override string name { get { return "whoislegacy"; } }
        public override string shortcut { get { return ""; } }
        public override string type { get { return CommandTypes.Information; } }
        public override bool UseableWhenFrozen { get { return true; } }
        public override CommandPerm[] ExtraPerms {
            get { return new[] { new CommandPerm(LevelPermission.AdvBuilder, "can see player's IP and if on whitelist") }; }
        }
                
        public override void Use(Player p, string message, CommandData data) {
            if (message.Length == 0) message = p.name;
            if (!Formatter.ValidName(p, message, "player")) return;
            
            int matches;
            Player who = PlayerInfo.FindMatches(p, message, out matches);
            if (matches > 1) return;
            
            if (matches == 0) {
                p.Message("Searching database for the player..");
                PlayerData target = PlayerDB.Match(p, message);
                if (target == null) return;
                
                foreach (OfflineStatPrinter printer in OfflineStat.Stats) {
                    printer(p, target);
                }
            } else {
                foreach (OnlineStatPrinter printer in OnlineStat.Stats) {
                    printer(p, who);
                }
            }
        }

        public override void Help(Player p) {
            p.Message("%T/WhoIsLegacy [name]");
            p.Message("%HDisplays information about that player.");
            p.Message("%HNote: Works for both online and offline players.");
        }
    }
}
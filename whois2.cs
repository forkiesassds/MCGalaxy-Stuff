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
		}
        
		public override void Unload(bool shutdown)
		{
			Logger.Log(LogType.Warning, "&cRestart the server to prevent problems!");
			Command.Unregister(new CmdWhois2());
		}
	}
	
	
	//new whois
	public class CmdWhois2 : Command2
	{
		public override string name { get { return "WhoIs"; } }
		public override string shortcut { get { return "WhoWas"; } }
		public override string type { get { return CommandTypes.Information; } }
		public override bool UseableWhenFrozen { get { return true; } }
		public override LevelPermission defaultRank { get { return LevelPermission.Guest; } }
		public override CommandPerm[] ExtraPerms {
            get { return new[] { new CommandPerm(LevelPermission.AdvBuilder, "can see player's IP and if on whitelist") }; }
        }
		public override CommandAlias[] Aliases {
            get { return new CommandAlias[] { new CommandAlias("Info"), new CommandAlias("i") }; }
        }

		
		public override void Use(Player p, string message, CommandData data)
		{
			int matches;
			string[] args = message.SplitSpaces();
			if (args[0].Length == 0) args[0] = p.name;
			
			Player who = PlayerInfo.FindMatches(p, args[0], out matches);
			
			
			
			if (args[0].Length > 0 && who == null) { 
				p.Message("Cannot find online player, searching PlayerDB...");
                PlayerData target = PlayerDB.Match(p, args[0]);
                if (target == null) return;
				Group group   = Group.GroupIn(target.Name);
				string color  = target.Color.Length == 0 ? group.Color : target.Color;
				string prefix = target.Title.Length == 0 ? "" : color + "[" + target.TitleColor + target.Title + color + "] ";
				
				string fullName = prefix + color + target.Name.RemoveLastPlus();
				p.Message("&S-[ {0} &S({1}) &S(&coffline&S) ]-", fullName, target.Name);
				p.Message("&f• &SHas &T{0} &S{1}", target.Money.ToString(), Server.Config.Currency);
				p.Message("&f• &SHas the rank of {0}", group.ColoredName);
				p.Message("&f• &SHas modified &T{0} &Sblocks", target.TotalModified);
				p.Message("&f• &SHas logged in &T{0} &Stimes", target.Logins);
				p.Message("&f• &SFirst login: &T{0}", target.FirstLogin.ToString("yyyy-MM-dd"));
				p.Message("&f• &SHas spent &T{0} &Son the server", target.TotalTime.Shorten());
				ItemPerms seeIpPerms = CommandExtraPerms.Find("WhoIs", 1);
				if (seeIpPerms.UsableBy(p.Rank)) {
					string ipMsg = target.IP;
					if (Server.bannedIP.Contains(target.IP)) ipMsg = "&8" + target.IP + ", which is banned";	
					p.Message("&f• &SThe IP of &T" + ipMsg);
					if (Server.Config.WhitelistedOnly && Server.whiteList.Contains(name)) p.Message("&f• &SPlayer is &fWhitelisted");
				}

				if (Server.Config.OwnerName.CaselessEq(target.Name)) p.Message("&f• &SPlayer is the &cServer owner");
				if (!Group.BannedRank.Players.Contains(target.Name)) return;            
				string banner, reason, prevRank;
				DateTime time;
				Ban.GetBanData(target.Name, out banner, out reason, out time, out prevRank);
				
				if (banner != null) p.Message("&f• &SBanned for &T{0} by {1}", reason, p.FormatNick(banner));
				else p.Message("&f• &SIs banned");
			}
			else {
				Group group = Group.GroupIn(who.name);
				string prefix = who.title.Length == 0 ? "" : group.Color + "[" + who.titlecolor + who.title + group.Color + "] ";
				string fullName = prefix + who.ColoredName;
				p.Message("&S-[ {0} &S({1}) ]-", fullName, who.name);
				p.Message("&f• &SHas &T{0} &S{1}", who.money.ToString(), Server.Config.Currency);
				p.Message("&f• &SHas the rank of {0}", group.ColoredName);
				p.Message("&f• &SHas modified &T{0} &Sblocks, &T{1} &Ssince login", who.TotalModified, who.SessionModified);
				p.Message("&f• &SHas logged in &T{0} &Stimes", who.TimesVisited);
				p.Message("&f• &SFirst login: &T{0}", who.FirstLogin.ToString("yyyy-MM-dd"));
				TimeSpan timeOnline = DateTime.UtcNow - who.SessionStartTime;
				p.Message("&f• &SHas spent &T{0} &Son the server, &T{1} &Sthis session", who.TotalTime.Shorten(), timeOnline.Shorten());

				TimeSpan idleTime = DateTime.UtcNow - who.LastAction;
				if (who.afkMessage != null) { p.Message("&f· &SIdle for {0} (AFK {1}&b)", idleTime.Shorten(), who.afkMessage); }
				else if (idleTime.TotalMinutes >= 1) { p.Message("&f· &SIdle for {0}", idleTime.Shorten()); }
				
				bool hasSkin = !who.SkinName.CaselessEq(who.truename);
				bool hasModel = !(who.Model.CaselessEq("humanoid") || who.Model.CaselessEq("human"));
				if (hasSkin) p.Message("&f• &SHas the skin of &T{0}", who.SkinName);
				if (hasModel) p.Message("&f• &SHas the model of &T{0}", who.Model);
				ItemPerms seeIpPerms = CommandExtraPerms.Find("WhoIs", 1);
				if (seeIpPerms.UsableBy(p.Rank)) {
					string ipMsg = who.ip;
					if (Server.bannedIP.Contains(who.ip)) ipMsg = "&8" + who.ip + ", which is banned";
					p.Message("&f• &SThe IP of &T" + ipMsg);
					if (Server.Config.WhitelistedOnly && Server.whiteList.Contains(name)) p.Message("&f• &SPlayer is &fWhitelisted");
				}
				
				if (Server.Config.OwnerName.CaselessEq(who.name)) p.Message("&f• &SPlayer is the &cServer owner");
				if (!Group.BannedRank.Players.Contains(name)) return;            
				string banner, reason, prevRank;
				DateTime time;
				Ban.GetBanData(name, out banner, out reason, out time, out prevRank);
				
				if (banner != null) p.Message("&f• &SBanned for &T{0} by {1}", reason, p.FormatNick(banner));
				else p.Message("&f• &SIs banned");
			}
		}

		// This is for when a player does /Help Whois2
		public override void Help(Player p)
		{
			p.Message("%T/WhoIs [name]");
            p.Message("%HDisplays information about that player.");
            p.Message("%HNote: Works for both online and offline players.");
		}
	}
}
using System;

namespace Supernova
{
	public class CmdBeatparkour : Command
	{
		public override string name { get { return "BeatParkour"; } }
		public override string shortcut { get { return ""; } }
		public override string type { get { return "chat"; } }
		public override bool museumUsable { get { return false; } }
		public override LevelPermission defaultRank { get { return LevelPermission.Guest; } }
		public override void Use(Player p, string message)
		{
			p.lastCMD = "nothing2";
			if (!message.StartsWith("leetpassword")) {p.Message("You can't use this command normally!"); return; }
			

			
			string[] bits = message.SplitSpaces(2);
			string parkourName = "the parkour";
			if (bits.Length >= 2) {
				parkourName = bits[1];
			}
			
            if (p.Extras.GetBoolean("BEATPARKOUR_"+parkourName) ) {
				p.Message("Parkour complete announcements only work once per parkour, once per session.");
				return;
			}
			
			p.Extras["BEATPARKOUR_"+parkourName] = true;
			Chat.MessageGlobal(p.color + p.DisplayName +"%S has completed "+parkourName+"%S! %6Congratulations!");
		}
		public override void Help(Player p)
		{
			//usage: /beatparkour changethispassword [parkour name]
            p.Message("%T/BeatParkour");
            p.Message("%HCommand used in message blocks at the end of some parkours.");
		}
	}
}

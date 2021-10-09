using System;

namespace MCGalaxy 
{
	public class CmdLinebreak : Command
	{
		public override string name { get { return "Linebreak"; } }

		public override string shortcut { get { return "lbrk"; } }
		public override string type { get { return "other"; } }
		public override bool museumUsable { get { return true; } }

		public override LevelPermission defaultRank { get { return LevelPermission.Banned; } }

		public override void Use(Player p, string message)
		{
			p.Message(" &0 &1 &2 &3 &4 &5 &6 &7 &8 &9 ");
		}

		public override void Help(Player p)
		{
			p.Message("%T/Linebreak");
			p.Message("%HBreaks a line.");
		}
	}
}
//reference System.dll

//	Auto-generated command skeleton class.
//	Use this as a basis for custom MCGalaxy commands.
//	File and class should be named a specific way. For example, /update is named 'CmdUpdate.cs' for the file, and 'CmdUpdate' for the class.
// As a note, MCGalaxy is designed for .NET 4.0

// To reference other assemblies, put a "//reference [assembly filename]" at the top of the file
//   e.g. to reference the System.Data assembly, put "//reference System.Data.dll"

// Add any other using statements you need after this
using System;
using System.Timers;
using MCGalaxy;
using MCGalaxy.Events.PlayerEvents;
using MCGalaxy.Tasks;
using MCGalaxy.Util;

namespace MCGalaxy 
{
	public class CmdDecide : Command
	{
		// The command's name (what you put after a slash to use this command)
		public override string name { get { return "Decide"; } }

		// Command's shortcut, can be left blank (e.g. "/copy" has a shortcut of "c")
		public override string shortcut { get { return ""; } }

		// Which submenu this command displays in under /Help
		public override string type { get { return "chat"; } }

		// Whether or not this command can be used in a museum. Block/map altering commands should return false to avoid errors.
		public override bool museumUsable { get { return true; } }

		// The default rank required to use this command. Valid values are:
		//   LevelPermission.Guest, LevelPermission.Builder, LevelPermission.AdvBuilder,
		//   LevelPermission.Operator, LevelPermission.Admin, LevelPermission.Nobody
		public override LevelPermission defaultRank { get { return LevelPermission.Guest; } }

		// This is for when a player executes this command by doing /Decide
		//   p is the player object for the player executing the command. 
		//   message is the arguments given to the command. (e.g. for '/update this', message is "this")
		public override void Use(Player p, string message)
		{
			System.Timers.Timer extraTimer = new System.Timers.Timer(2000);
			Random random = new Random();
			string msg = "The magic ball is deciding for " + p.ColoredName + " %Sbetween &f";
			int i = 0;
		    var choices = string.Join(" ", message).Split(',');
		    while (i < choices.Length) 
		    {
		    	if (choices[i][0] == ' ') { msg += choices[i].Substring(1, choices[i].Length - 1); }
				else {msg += choices[i]; }
		    	if (i == choices.Length - 2) 
		    	{
		    		msg += " %Sand &f";
		    	}
				else if (i  == choices.Length - 1) 
				{
					msg += "%S.";
				}
		    	else
		    	{
		    		msg += "%S, &f";
		    	}
		    	i++;
		    }
		    //await ctx.RespondAsync();
		    Chat.Message(ChatScope.Global, msg, null, Filter8Ball);
			extraTimer.Start();
			extraTimer.Elapsed += delegate {
				extraTimer.Stop();
				Chat.Message(ChatScope.Global, "The magic ball has decided for " + p.ColoredName + ", choosing &f" + choices[random.Next(0, choices.Length)], null, Filter8Ball);
			};
		}

		// This is for when a player does /Help Decide
		public override void Help(Player p)
		{
			p.Message("/Decide - Allows you to decide between different options, Usage: %a/decide choice, choice2");
		}
		static bool Filter8Ball(Player p, object arg) { return !p.Ignores.EightBall; }
	}
}

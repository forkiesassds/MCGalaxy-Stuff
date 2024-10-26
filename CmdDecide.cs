//reference System.dll
using System;
using MCGalaxy;
using MCGalaxy.Tasks;

namespace VeryPlugins 
{
	public class CmdDecide : Command
	{
		public override string name => "Decide";
		public override string type => CommandTypes.Chat;
		public override LevelPermission defaultRank => LevelPermission.Guest;

		static readonly TimeSpan delay = TimeSpan.FromSeconds(2);

		public override void Use(Player p, string message)
		{
			string msg = "The magic ball is deciding for " + p.ColoredName + " &Sbetween &f";
		    string[] choices = string.Join(" ", message).Split(',');
		    for (int i = 0; i < choices.Length; i++) 
		    {
		    	if (choices[i][0] == ' ') 
					msg += choices[i].Substring(1, choices[i].Length - 1);
				else 
					msg += choices[i];

		    	if (i == choices.Length - 2) 
		    	{
		    		msg += " %Sand &f";
		    	}
				else if (i == choices.Length - 1) 
				{
					msg += "%S.";
				}
		    	else
		    	{
		    		msg += "%S, &f";
		    	}
		    }
		    Chat.Message(ChatScope.Global, msg, null, (pl, arg) => !pl.Ignores.EightBall);

			DecideState state;
			state.p = p;
			state.choices = choices;

			Server.MainScheduler.QueueOnce(DecideCallback, state, delay);
		}

		static void DecideCallback(SchedulerTask task)
		{
			DecideState state = (DecideState)task.State;
			string[] choices = state.choices;
			Player p = state.p;

			Random random = new Random();

			Chat.Message(ChatScope.Global, "The magic ball has decided for " + p.ColoredName + ", choosing &f" + choices[random.Next(0, choices.Length)], 
							null, (pl, arg) => !pl.Ignores.EightBall);

		}

		public override void Help(Player p)
		{
			p.Message("/Decide - Allows you to decide between different options, Usage: %a/decide choice, choice2");
		}

		private struct DecideState
		{
			public Player p;
			public string[] choices;
		}
	}
}

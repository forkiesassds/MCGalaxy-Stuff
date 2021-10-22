// plugin for na2 like 8ball
using System;
using System.Text;
using System.Collections.Generic;
using System.Threading;
using MCGalaxy.Tasks;
using MCGalaxy.Util;

namespace MCGalaxy.Commands.Info
{
	public class EightBallTwo : Plugin
	{
		public override string name { get { return "8ball2"; } }
		public override string MCGalaxy_Version { get { return "1.9.3.4"; } }
		public override string creator { get { return "icanttellyou"; } }

		public override void Load(bool startup)
		{
			Command.Unregister(Command.Find("8ball"));
			Command.Register(new Cmd8Ball2());
		}
        
		public override void Unload(bool shutdown)
		{
			Logger.Log(LogType.Warning, "&cRestart the server to prevent problems!");
			Command.Unregister(new Cmd8Ball2());
		}
	}
	
	
	public class Cmd8Ball2 : Command2 {
        public override string name { get { return "8ball"; } }
        public override string shortcut { get { return ""; } }
        public override string type { get { return CommandTypes.Chat; } }
        public override bool SuperUseable { get { return false; } }

        static DateTime nextUse;
        static TimeSpan delay = TimeSpan.FromSeconds(2);
        
        public override void Use(Player p, string question, CommandData data) {
            if (!p.CanSpeak()) return;
            if (question.Length == 0) { Help(p); return; }
            
            TimeSpan delta = nextUse - DateTime.UtcNow;
            if (delta.TotalSeconds > 0) {
                p.Message("The 8-ball is still recharging, wait another {0} seconds.",
                               (int)Math.Ceiling(delta.TotalSeconds));
                return;
            }
            nextUse = DateTime.UtcNow.AddSeconds(10 + 2);
           
            StringBuilder builder = new StringBuilder(question.Length);
            foreach (char c in question) {
                if (Char.IsLetterOrDigit(c)) builder.Append(c);
            }
           
            string msg = p.ColoredName + " &Sasked the &b8-Ball: &f" + question;
            Chat.Message(ChatScope.Global, msg, null, Filter8Ball);
            
            string final = builder.ToString();
            Server.MainScheduler.QueueOnce(EightBallCallback, final, delay);
        }
        
        static void EightBallCallback(SchedulerTask task) {
            string final = (string)task.State;
            Random random = new Random(final.ToLower().GetHashCode());
            string[] responses;
			string[] sizes = new string[] { "Small.", "Pretty big.", "Medium sized.", "Insanely small.", "As large as a particle of dust.", "Can't even see!", "Extremely large.", "As large as a pile of landfill", "Universe sized!", "Multiverse sized!", "So large it doesn't exist!", "So small it doesn't exist!" };
			string[] lengths = new string[] { "Short.", "Pretty long.", "Medium in length.", "Insanely short.", "As short as a human hair.", "Don't know!", "Extremely long.", "As long as a room.", "As long as a universe!", "As long as a multiverse!", "So long it doesn't exist!", "So long it doesn't exist!" };
			List<string> colors = new List<string>();
			List<string> players = new List<string>();
			foreach (ColorDesc col in Colors.List) {
                if (col.Undefined) continue;
                colors.Add(col.Name);
            }
			
			foreach (Player pl in PlayerInfo.Online.Items) {
				players.Add(pl.ColoredName);
            }
			
			if (final.StartsWith("what color")) { responses = colors.ToArray(); }
			else if (final.StartsWith("who")) { responses = players.ToArray(); }
			else if (final.StartsWith("howmuch")) { responses = new string[] { "A lot.", "A little bit.", "None at all.", "Too many to even count." }; }
			else if (final.StartsWith("whatsize")) { responses = sizes; }
			else if (final.StartsWith("howlarge")) { responses = sizes; }
			else if (final.StartsWith("howbig")) { responses = sizes; }
			else if (final.StartsWith("howlong")) { responses = lengths; }
			else if (final.StartsWith("howmany")) { responses = new string[] { random.Next(100).ToString() }; }
			else if (final.StartsWith("should")) { responses = new string[] { "Probably not.", "Probably.", "No!", "Yes!", "Definitely!", "Do some more thinking." }; }
			else if (final.StartsWith("when")) { responses = new string[] { DateTime.Now.AddSeconds(random.Next(63113851)).ToString("yyyy-MM-dd") }; }
			else if (final.StartsWith("how")) { responses = new string[] { "Cannot answer your question." }; }
			else if (final.StartsWith("explain")) { responses = new string[] { "Cannot answer your question." }; }
			else if (final.StartsWith("why")) { responses = new string[] { "Cannot answer your question." }; }
			else if (final.StartsWith("where")) { responses = new string[] { "Afghanistan", "Albania", "Algeria", "Andorra", "Angola", "Antigua and Barbuda", "Argentina", "Armenia", "Australia", "Austria", "Azerbaijan", "Bahamas", "Bahrain", "Bangladesh", "Barbados", "Belarus", "Belgium", "Belize", "Benin", "Bhutan", "Bolivia", "Bosnia and Herzegovina", "Botswana", "Brazil", "Brunei", "Bulgaria", "Burkina Faso", "Burundi", "Côte d'Ivoire", "Cabo Verde", "Cambodia", "Cameroon", "Canada", "Central African Republic", "Chad", "Chile", "China", "Colombia", "Comoros", "Congo Congo-Brazzaville", "Costa Rica", "Croatia", "Cuba", "Cyprus", "Czech Republic", "Democratic Republic of the Congo", "Denmark", "Djibouti", "Dominica", "Dominican Republic", "Ecuador", "Egypt", "El Salvador", "Equatorial Guinea", "Eritrea", "Estonia", "Eswatini", "Ethiopia", "Fiji", "Finland", "France", "Gabon", "Gambia", "Georgia", "Germany", "Ghana", "Greece", "Grenada", "Guatemala", "Guinea", "Guinea-Bissau", "Guyana", "Haiti", "Holy See", "Honduras", "Hungary", "Iceland", "India", "Indonesia", "Iran", "Iraq", "Ireland", "Israel", "Italy", "Jamaica", "Japan", "Jordan", "Kazakhstan", "Kenya", "Kiribati", "Kuwait", "Kyrgyzstan", "Laos", "Latvia", "Lebanon", "Lesotho", "Liberia", "Libya", "Liechtenstein", "Lithuania", "Luxembourg", "Madagascar", "Malawi", "Malaysia", "Maldives", "Mali", "Malta", "Marshall Islands", "Mauritania", "Mauritius", "Mexico", "Micronesia", "Moldova", "Monaco", "Mongolia", "Montenegro", "Morocco", "Mozambique", "Myanmar", "Namibia", "Nauru", "Nepal", "Netherlands", "New Zealand", "Nicaragua", "Niger", "Nigeria", "North Korea", "North Macedonia", "Norway", "Oman", "Pakistan", "Palau", "Palestine State", "Panama", "Papua New Guinea", "Paraguay", "Peru", "Philippines", "Poland", "Portugal", "Qatar", "Romania", "Russia", "Rwanda", "Saint Kitts and Nevis", "Saint Lucia", "Saint Vincent and the Grenadines", "Samoa", "San Marino", "Sao Tome and Principe", "Saudi Arabia", "Senegal", "Serbia", "Seychelles", "Sierra Leone", "Singapore", "Slovakia", "Slovenia", "Solomon Islands", "Somalia", "South Africa", "South Korea", "South Sudan", "Spain", "Sri Lanka", "Sudan", "Suriname", "Sweden", "Switzerland", "Syria", "Tajikistan", "Tanzania", "Thailand", "Timor-Leste", "Togo", "Tonga", "Trinidad and Tobago", "Tunisia", "Turkey", "Turkmenistan", "Tuvalu", "Uganda", "Ukraine", "United Arab Emirates", "United Kingdom", "United States of America", "Uruguay", "Uzbekistan", "Vanuatu", "Venezuela", "Vietnam", "Yemen", "Zambia", "Zimbabwe" }; }
			else if (final.EndsWith("myalignment")
			{ 
				string[] first = new string[] { "Lawfull", "Neutral", "Chaotic" };
				string[] second = new string[] { "Good", "Neutral", "Evil" };
				Thread.Sleep(420);
				int findx = new Random(final.ToLower().GetHashCode() + new Random().Next(Int32.MaxValue)).Next(first.Length);
				Thread.Sleep(420);
				int sindx = new Random(final.ToLower().GetHashCode() + new Random().Next(Int32.MaxValue)).Next(second.Length);
				responses = new string[] { "Your alignment is " + first[findx] + " " + second[sindx] };
			}
			else if (final.StartsWith("what")) { responses = new string[] { "Cannot answer your question." }; }
			else 
			{
				TextFile file = TextFile.Files["8ball"];
				file.EnsureExists();
				responses = file.GetText();
			}
            
            string msg = "The &b8-Ball &Ssays: &f" + responses[random.Next(responses.Length)];
            Chat.Message(ChatScope.Global, msg, null, Filter8Ball);
        }
        
        static bool Filter8Ball(Player p, object arg) { return !p.Ignores.EightBall; }
        public override void Help(Player p) {
            p.Message("&T/8ball [question]");
            p.Message("&HGet an answer from the all-knowing 8-Ball!");
        }
    }
}

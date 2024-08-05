using System.Collections.Generic;
using MCGalaxy;
using MCGalaxy.Events.PlayerEvents;

namespace VeryPlugins
{
    public class MessageConsent : Plugin
    {
        public override string name => "MessageConsent";
        public override string creator => "icanttellyou";
        public override string MCGalaxy_Version => "1.9.4.9";

        public override void Load(bool auto)
        {
            OnPlayerChatEvent.Register(HandleChat, Priority.High);
        }

        public override void Unload(bool auto)
        {
            OnPlayerChatEvent.Unregister(HandleChat);
        }

        private void HandleChat(Player source, string msg)
        {

            if (msg.CaselessEq("y") || msg.CaselessEq("yes") || 
                msg.CaselessEq("n") || msg.CaselessEq("no"))
            {
                if (!source.Extras.Contains("CONSENT_QUEUE"))
                {
                    source.Extras["CONSENT_QUEUE"] = new Queue<string>();
                    return;
                }

                Queue<string> mQ = (Queue<string>)source.Extras["CONSENT_QUEUE"];

                if (mQ.Count > 0)
                {
                    string toMsg = mQ.Dequeue();

                    if (!msg.CaselessEq("n") && !msg.CaselessEq("no"))
                    {
                        source.Message(toMsg);
                    }
                    else
                    {
                        source.Message("The message has been discarded.");
                    }
                    source.cancelchat = true;
                    return;
                }
            }
            

            Player[] players = PlayerInfo.Online.Items;

            foreach (Player pl in players)
            {
                if (!pl.Extras.Contains("CONSENT_QUEUE"))
                    pl.Extras["CONSENT_QUEUE"] = new Queue<string>();

                if (Chat.Ignoring(pl, source)) continue;

                string toQueue = UnescapeMessage(pl, source, "λFULL: &f" + msg);
                if (pl != source)
                {
                    ((Queue<string>)pl.Extras["CONSENT_QUEUE"]).Enqueue(toQueue);
                    pl.Message("{0} &Shas sent a message! Do you want to view it? &2Y&S/&cN", pl.FormatNick(source));
                }
                else
                {
                    pl.Message(toQueue);
                }
            }

            source.cancelchat = true;
        }

        static string UnescapeMessage(Player pl, Player src, string msg)
        {
            string nick = pl.FormatNick(src);
            msg = msg.Replace("λNICK", nick);

            if (pl.Ignores.Titles)
            {
                return msg.Replace("λFULL", src.GroupPrefix + nick);
            }
            else if (pl.Ignores.Nicks)
            {
                return msg.Replace("λFULL", src.color + src.prefix + src.truename);
            }
            else
            {
                return msg.Replace("λFULL", src.FullName);
            }
        }
    }
}
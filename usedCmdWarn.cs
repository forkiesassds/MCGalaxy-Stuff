//reference System.Core.dll
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MCGalaxy;
using MCGalaxy.Events.PlayerEvents;
using MCGalaxy.Events.ServerEvents;

namespace VeryPlugins
{
    public sealed class UsedCmdWarnPlugin : Plugin
    {
        private const string WARN_CMD_PATH = "plugins/warncmds.txt";

        public override string name => "usedCmdWarn";
        public override string creator => "icanttellyou";
        public override string MCGalaxy_Version => "1.9.4.9";

        private List<string> warnCmds;

        public override void Load(bool auto)
        {
            OnConfigUpdatedEvent.Register(OnConfigUpdated, Priority.Low);

            OnConfigUpdated();

            OnPlayerCommandEvent.Register(HandlePlayerCommand, Priority.High);
        }

        public override void Unload(bool auto)
        {
            OnConfigUpdatedEvent.Unregister(OnConfigUpdated);
            OnPlayerCommandEvent.Unregister(HandlePlayerCommand);
        }

        void OnConfigUpdated()
        {
            if (!File.Exists(WARN_CMD_PATH)) return;
            warnCmds = File.ReadAllLines(WARN_CMD_PATH).ToList();
        }

        void HandlePlayerCommand(Player p, string cmd, string args, CommandData data)
        {
            if (warnCmds != null && warnCmds.CaselessContains(cmd))
            {
                string msg = "To Ops: " + p.name + " &Sused " + "/" + cmd + " " + args;
                Chat.Message(ChatScope.Perms, msg, Chat.OpchatPerms, null, true);
            }
        }
    }
}

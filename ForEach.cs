using System;
using System.Collections.Generic;
using MCGalaxy;
using MCGalaxy.Maths;
using BlockID = System.UInt16;

namespace VeryPlugins
{
    public class ForEachPlugin : Plugin
    {
        public override string name { get { return "ForEach"; } }
        public override string MCGalaxy_Version { get { return "1.9.4.9"; } }
        public override string creator { get { return "icanttellyou"; } }

        CmdForEach cmd = new CmdForEach();
        
        public override void Load(bool auto)
        {
            Command.Register(cmd);
        }
        
        public override void Unload(bool auto)
        {
            Command.Unregister(cmd);
        }
    }

    public class CmdForEach : Command2
    {
        public override string name { get { return "ForEach"; } }
        public override string type { get { return CommandTypes.Building; } }

        public override void Use(Player p, string message, CommandData data)
        {
            if (message.Length == 0)
            {
                Help(p);
                return;
            }
            
            ForEachArgs args = new ForEachArgs();
            string[] parts = message.SplitSpaces(2);
            
            args.block = Block.Parse(p, parts[0]);
            args.commandData = data;
            
            string[] commands = parts[1].Split('|');
            List<Tuple<Command, string>> commandList = new List<Tuple<Command, string>>();
            foreach (string command in commands)
            {
                string[] cmdParts = command.SplitSpaces(2);
                string cmdName = cmdParts[0], cmdArgs = "";
                if (cmdParts.Length > 1)
                    cmdArgs = cmdParts[1];
                
                Search(ref cmdName, ref cmdArgs);
                Command cmd = Find(cmdName);
                if (cmd == null)
                {
                    p.Message("Unknown command \"{0}\".", cmdName);
                    return;
                }
                
                commandList.Add(new Tuple<Command, string>(cmd, cmdArgs));
            }
            args.commandChain = commandList;
            
            p.Message("Place or break two blocks to determine the edges.");
            p.MakeSelection(2, "Selecting region for &SForEach", args, DoForEach);
        }
        
        bool DoForEach(Player p, Vec3S32[] marks, object state, BlockID block)
        {
            ForEachArgs args = (ForEachArgs)state;
            Level lvl = p.level;
            
            Vec3S32 min = Vec3S32.Min(marks[0], marks[1]);
            Vec3S32 max = Vec3S32.Max(marks[0], marks[1]);

            Vec3U16 p1 = Clamp(lvl, min);
            Vec3U16 p2 = Clamp(lvl, max);
            
            for (ushort y = p1.Y; y <= p2.Y; y++)
                for (ushort z = p1.Z; z <= p2.Z; z++)
                    for (ushort x = p1.X; x <= p2.X; x++)
                        if (lvl.GetBlock(x, y, z) == args.block)
                            foreach (Tuple<Command, string> cmd in args.commandChain)
                            {
                                if (p.level != lvl)
                                {
                                    p.Message("&WSwitched levels, aborting ForEach!");
                                    return true;
                                }
                                
                                try
                                {
                                    string cmdArgs = cmd.Item2;
                                    if (cmdArgs.Contains('~')) cmdArgs = ReplaceCharString(cmdArgs, '~', x.ToString());
                                    if (cmdArgs.Contains('~')) cmdArgs = ReplaceCharString(cmdArgs, '~', y.ToString());
                                    if (cmdArgs.Contains('~')) cmdArgs = ReplaceCharString(cmdArgs, '~', z.ToString());

                                    cmd.Item1.Use(p, cmdArgs, args.commandData);
                                }
                                catch (Exception)
                                {
                                    return false;
                                }
                            }

            return true;
        }

        public override void Help(Player p)
        {
            p.Message("&T/ForEach [block] [commands]");
            p.Message("&HRuns a command on every block within selection");
            p.Message("&HUse | to separate commands within the command chain");
            p.Message("&HUse ~ to substitute coordinates within a command.");
        }

        string ReplaceCharString(string value, char c, string insert)
        {
            int index = value.IndexOf(c);
            
            string placed = value;
            placed = placed.Insert(index + 1, insert);
            placed = placed.Remove(index, 1);
            return placed;
        }
        
        Vec3U16 Clamp(Level lvl, Vec3S32 pos) 
        {
            pos.X = Math.Max(0, Math.Min(pos.X, lvl.Width - 1));
            pos.Y = Math.Max(0, Math.Min(pos.Y, lvl.Height - 1));
            pos.Z = Math.Max(0, Math.Min(pos.Z, lvl.Length - 1));
            return new Vec3U16((ushort)pos.X, (ushort)pos.Y, (ushort)pos.Z);
        }

        struct ForEachArgs
        {
            public BlockID block;
            public CommandData commandData;
            public List<Tuple<Command, string>> commandChain;
        }
    }
}
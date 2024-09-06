using System;
using System.Collections.Generic;
using MCGalaxy.Commands;
using MCGalaxy.Commands.Fun;
using MCGalaxy.Config;
using MCGalaxy.Generator;
using MCGalaxy.Events.PlayerEvents;
using MCGalaxy.Events.ServerEvents;
using MCGalaxy.Network;
using BlockID = System.UInt16;

namespace MCGalaxy.Games
{
    public sealed class SpleefPlugin : Plugin
    {
        public override string creator { get { return "icanttellyou"; } }
        public override string MCGalaxy_Version { get { return "1.9.5.0"; } }
        public override string name { get { return "Spleef"; } }

        Command cmd = new CmdSpleef();
        public override void Load(bool startup)
        {
            Command.Register(cmd);
            SpleefGame game = SpleefGame.Instance;

            game.Config.Path = "plugins/spleef.properties";
            game.ReloadConfig();
            OnConfigUpdatedEvent.Register(game.ReloadConfig, Priority.Low);
            game.AutoStart();
        }

        public override void Unload(bool shutdown)
        {
            Command.Unregister(cmd);
            SpleefGame game = SpleefGame.Instance;
            if (game.Running) game.End();

            OnConfigUpdatedEvent.Unregister(game.ReloadConfig);
        }
    }

    public sealed class SpleefConfig : RoundsGameConfig
    {
        [ConfigInt("money-award", "Game", 30)]
        public int MoneyAward = 30;

        public override bool AllowAutoload { get { return true; } }
        protected override string GameName { get { return "Spleef"; } }

        public override void Load()
        {
            base.Load();
            if (Maps.Count == 0) Maps.Add("spleef");
        }
    }

    public sealed partial class SpleefGame : RoundsGame
    {
        //game
        public VolatileArray<Player> Players = new VolatileArray<Player>();
        public VolatileArray<Player> Remaining = new VolatileArray<Player>();

        public SpleefConfig Config = new SpleefConfig();
        public override string GameName { get { return "Spleef"; } }
        public override RoundsGameConfig GetConfig() { return Config; }

        protected override string WelcomeMessage { get { return "&dSpleef &Sis running! Type &T/Spl join &Sto join!"; } }

        public int Interval;

        public static SpleefGame Instance = new SpleefGame();
        public SpleefGame()
        {
            Picker = new SimpleLevelPicker();
        }

        public override void UpdateMapConfig() { }

        protected override List<Player> GetPlayers()
        {
            return Map.getPlayers();
        }

        public override void OutputStatus(Player p)
        {
            Player[] players = Players.Items;
            p.Message("Players in spleef:");

            if (RoundInProgress)
            {
                p.Message(players.Join(FormatPlayer));
            }
            else
            {
                p.Message(players.Join(pl => pl.ColoredName));
            }
        }

        string FormatPlayer(Player pl)
        {
            string suffix = Remaining.Contains(pl) ? " &a[IN]" : " &c[OUT]";
            return pl.ColoredName + suffix;
        }

        protected override string GetStartMap(Player p, string forcedMap)
        {
            if (!LevelInfo.MapExists("spleef"))
            {
                p.Message("Spleef level not found, generating..");
                GenerateMap(p, 32, 32, 32);
            }
            return "spleef";
        }

        protected override void StartGame() { }
        protected override void EndGame()
        {
            Players.Clear();
            Remaining.Clear();
        }

        public void GenerateMap(Player p, int width, int height, int length)
        {
            Level lvl = SpleefMapGen.Generate(width, height, length);
            Level cur = LevelInfo.FindExact("spleef");
            if (cur != null) LevelActions.Replace(cur, lvl);
            else LevelInfo.Add(lvl);

            lvl.Save();
            Map = lvl;

            const string format = "Generated map ({0}x{1}x{2}), sending you to it..";
            p.Message(format, width, height, length);
            PlayerActions.ChangeMap(p, "spleef");
        }

        public override void PlayerJoinedGame(Player p)
        {
            if (!Players.Contains(p))
            {
                if (p.level != Map && !PlayerActions.ChangeMap(p, "spleef")) return;
                Players.Add(p);
                p.Message("You've joined spleef!");
                Chat.MessageFromLevel(p, "λNICK &Sjoined spleef!");
            }
            else
            {
                p.Message("You've already joined spleef. To leave, go to another map.");
            }
        }

        public override void PlayerLeftGame(Player p)
        {
            Players.Remove(p);
            OnPlayerDied(p);
        }

        protected override void HookEventHandlers()
        {
            base.HookEventHandlers();

            OnPlayerSpawningEvent.Register(HandlePlayerSpawning, Priority.High);
            OnBlockChangingEvent.Register(HandleBlockChanged, Priority.High);
        }

        protected override void UnhookEventHandlers()
        {
            base.UnhookEventHandlers();

            OnPlayerSpawningEvent.Unregister(HandlePlayerSpawning);
            OnBlockChangingEvent.Unregister(HandleBlockChanged);
        }

        void HandlePlayerSpawning(Player p, ref Position pos, ref byte yaw, ref byte pitch, bool respawning)
        {
            if (!respawning || !Remaining.Contains(p)) return;
            Chat.MessageFromLevel(p, "λNICK &Sis out of spleef!");
            OnPlayerDied(p);
        }

        void HandleBlockChanged(Player p, ushort x, ushort y, ushort z, BlockID block, bool placing, ref bool cancel)
        {
            if (Running && !Remaining.Contains(p) && Map == p.level)
            {
                p.Message("You are out of the round, and cannot break blocks.");
                p.RevertBlock(x, y, z);
                cancel = true;
            }
        }

        protected override string FormatStatus1(Player p)
        {
            return RoundInProgress ? Remaining.Count + " players left" : "";
        }
        //rounds
        BufferedBlockSender bulk = new BufferedBlockSender();

        protected override void DoRound()
        {
            bulk.level = Map;
            SetBoardOpening(Block.Glass);
            ResetBoard();
            if (!Running) return;
            DoRoundCountdown(10);
            ResetBoard();
            SetBoardOpening(Block.Air);
            if (!Running) return;

            bulk.Flush();
            if (!Running) return;

            SpawnPlayers();
            if (!Running) return;

            BeginRound();
            SetBoardOpening(Block.Glass);
            if (!Running) return;

            RoundInProgress = true;
            UpdateAllStatus();
            while (RoundInProgress && Running && Remaining.Count > 0);
        }

        protected override void ContinueOnSameMap()
        {
            // spleef only modifies board in the map, so it's fine to continue on the same map
            // without needing to reload the entire map
        }

        void SetBoardOpening(BlockID block)
        {
            int midX = Map.Width / 2, midY = Map.Height / 2, midZ = Map.Length / 2;
            Cuboid(midX - 1, midY, midZ - 1, midX, midY, midZ, block);
            bulk.Flush();
        }

        void BeginRound()
        {
            Map.Message("Starting Spleef");

            if (!Running) return;
            Map.Message("GO!!!!!!!");
            Map.Config.Deletable = true;
            Map.UpdateBlockPermissions();

            Player[] players = Players.Items;
            Remaining.Clear();
            foreach (Player pl in players) { Remaining.Add(pl); }
        }

        void ResetBoard()
        {
            SetBoardOpening(Block.Glass);
            int maxX = Map.Width - 1, maxZ = Map.Length - 1;
            Cuboid(0, 4, 0, maxX, 4, maxZ, Block.White);

            bulk.Flush();
        }

        void Cuboid(int x1, int y1, int z1, int x2, int y2, int z2, BlockID block)
        {
            for (int y = y1; y <= y2; y++)
                for (int z = z1; z <= z2; z++)
                    for (int x = x1; x <= x2; x++)
                    {
                        TryChangeBlock(x, y, z, block);
                    }
        }

        void TryChangeBlock(int x, int y, int z, BlockID block)
        {
            int index = Map.PosToInt((ushort)x, (ushort)y, (ushort)z);
            if (!Map.DoPhysicsBlockchange(index, block)) return;

            bulk.Add(index, block);
        }

        void SpawnPlayers()
        {
            Player[] players = Players.Items;
            int midX = Map.Width / 2, midY = Map.Height / 2, midZ = Map.Length / 2;
            Position pos = Position.FromFeetBlockCoords(midX, midY, midZ);
            pos.X -= 16; pos.Z -= 16;

            foreach (Player pl in players)
            {
                if (pl.level != Map)
                {
                    pl.Message("Sending you to the correct map.");
                    PlayerActions.ChangeMap(pl, Map.name);
                }

                Entities.Spawn(pl, pl, pos, pl.Rot);
                pl.SendPosition(pos, pl.Rot);
            }
        }

        public void OnPlayerDied(Player p)
        {
            if (!Remaining.Remove(p) || !RoundInProgress) return;
            Player[] players = Remaining.Items;

            switch (players.Length)
            {
                case 1:
                    Chat.MessageFromLevel(players[0], "λNICK &Sis the winner!");
                    EndRound(players[0]);
                    break;
                case 2:
                    Map.Message("Only 2 Players left:");
                    Player[] plrs = Players.Items;
                    foreach (Player pl in plrs)
                        pl.Message("{0} &Sand {1}", pl.FormatNick(players[0]), pl.FormatNick(players[1]));
                    break;
                default:
                    Map.Message(players.Length + " players left!");
                    break;
            }
            UpdateAllStatus();
        }

        public override void EndRound() { EndRound(null); }
        public void EndRound(Player winner)
        {
            RoundInProgress = false;
            Map.Config.Deletable = false;
            Map.UpdateBlockPermissions();
            Remaining.Clear();
            UpdateAllStatus();

            if (winner != null)
            {
                winner.SetMoney(winner.money + 30);
                winner.Message("Congratulations, you won this round of spleef!");
                if (winner.Supports(CpeExt.MessageTypes))
                {
                    winner.SendCpeMessage(CpeMessageType.BigAnnouncement, "&SYou win!");
                    winner.SendCpeMessage(CpeMessageType.SmallAnnouncement, string.Format("&a+{0} &S{1}!", Config.MoneyAward, Server.Config.Currency));
                }
                else
                {
                    winner.Message("&a+{0} &S{1}!", Config.MoneyAward, Server.Config.Currency);
                }
                PlayerActions.Respawn(winner);
            }
            else
            {
                Player[] players = Players.Items;
                foreach (Player pl in players)
                {
                    PlayerActions.Respawn(pl);
                }
            }
        }
    }

    public static class SpleefMapGen
    {

        public static Level Generate(int width, int height, int length)
        {
            Level lvl = new Level("spleef", (ushort)width, (ushort)height, (ushort)length);
            MakeBoundaries(lvl);
            MakeViewAreaRoof(lvl);
            MakePlayAreaWalls(lvl);
            MakePlayArea(lvl);

            lvl.VisitAccess.Min = LevelPermission.Guest;
            lvl.BuildAccess.Min = LevelPermission.Guest; //whole map is op_[block] anyways
            lvl.Config.UseBlockDB = false;
            lvl.Config.Deletable = false;
            lvl.Config.Buildable = false;
            lvl.Config.Drawing = false;
            lvl.Config.MOTD = "Welcome to the Spleef map! -hax reach=5 -push";

            lvl.spawnx = (ushort)(lvl.Width / 2);
            lvl.spawny = (ushort)(lvl.Height / 2 + 4);
            lvl.spawnz = (ushort)(lvl.Length / 2);
            return lvl;
        }

        static void MakeBoundaries(Level lvl)
        {
            int maxX = lvl.Width - 1, maxZ = lvl.Length - 1;
            Cuboid(0, 1, 0, maxX, 1, maxZ, Block.Magma, lvl);
        }

        static void MakeViewAreaRoof(Level lvl)
        {
            int maxX = lvl.Width - 1, midY = lvl.Height / 2, maxZ = lvl.Length - 1;
            Cuboid(0, midY, 0, maxX, midY, maxZ, Block.Op_Glass, lvl);
        }

        static void MakePlayAreaWalls(Level lvl)
        {
            int maxX = lvl.Width - 1, maxZ = lvl.Length - 1, maxY = lvl.Height - 1;
            Cuboid(0, 4, 0, 0, maxY / 2 + 1, maxZ, Block.Bedrock, lvl);
            Cuboid(maxX, 4, 0, maxX, maxY / 2 + 1, maxZ, Block.Bedrock, lvl);
            Cuboid(0, 4, 0, maxX, maxY / 2 + 1, 0, Block.Bedrock, lvl);
            Cuboid(0, 4, maxZ, maxX, maxY / 2 + 1, maxZ, Block.Bedrock, lvl);
        }

        static void MakePlayArea(Level lvl)
        {
            int maxX = lvl.Width - 1, maxZ = lvl.Length - 1;
            Cuboid(0, 4, 0, maxX, 4, maxZ, Block.White, lvl);
        }

        static void Cuboid(int x1, int y1, int z1, int x2, int y2, int z2, byte block, Level lvl)
        {
            for (int y = y1; y <= y2; y++)
                for (int z = z1; z <= z2; z++)
                    for (int x = x1; x <= x2; x++)
                    {
                        lvl.SetTile((ushort)x, (ushort)y, (ushort)z, block);
                    }
        }
    }

    public sealed class CmdSpleef : RoundsGameCmd
    {
        public override string name { get { return "Spleef"; } }
        public override string shortcut { get { return "spl"; } }
        protected override RoundsGame Game { get { return SpleefGame.Instance; } }
        public override CommandPerm[] ExtraPerms
        {
            get { return new[] { new CommandPerm(LevelPermission.Operator, "can manage spleef") }; }
        }

        public override void Use(Player p, string message, CommandData data)
        {
            if (message.CaselessEq("join"))
            {
                HandleJoin(p, SpleefGame.Instance);
            }
            else
            {
                Console.WriteLine("HERE WE GO!");
                base.Use(p, message, data);
                Console.WriteLine("AND WE ARE FUCKING DONE!");
            }
        }

        void HandleJoin(Player p, SpleefGame game)
        {
            if (!game.Running)
            {
                p.Message("Cannot join as spleef is not running.");
            }
            else
            {
                if (game.RoundInProgress && !game.Players.Contains(p))
                {
                    p.Message("You have joined, but you will only be able to play until next round.");
                }
                game.PlayerJoinedGame(p);
            }
        }

        protected override void HandleSet(Player p, RoundsGame game_, string[] args)
        {
            if (args.Length < 4) { Help(p); return; }
            if (game_.Running)
            {
                p.Message("You must stop Spleef before replacing the map."); return;
            }

            ushort x = 0, y = 0, z = 0;
            if (!MapGen.GetDimensions(p, args, 1, ref x, ref y, ref z)) return;

            SpleefGame game = (SpleefGame)game_;
            game.GenerateMap(p, x, y, z);
        }

        protected override void HandleStart(Player p, RoundsGame game, string[] args)
        {
            if (game.Running) { p.Message("{0} is already running", game.GameName); return; }

            game.Start(p, "spleef", int.MaxValue);
        }

        public override void Help(Player p)
        {
            p.Message("&T/Spl set [width] [height] [length]");
            p.Message("&HRe-generates the spleef map (default is 32x32x32)");
            p.Message("&T/Spl start &H- Starts Spleef");
            p.Message("&T/Spl stop &H- Stops Spleef");
            p.Message("&T/Spl end &H- Ends current round of Spleef");
            p.Message("&T/Spl join &H- joins the game");
            p.Message("&T/Spl status &H- lists players currently playing");
        }
    }
}

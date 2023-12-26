using System;
using System.Collections.Generic;
using MCGalaxy.Commands;
using MCGalaxy.Commands.Fun;
using MCGalaxy.Generator;
using MCGalaxy.Events.PlayerEvents;
using MCGalaxy.Network;
using BlockID = System.UInt16;

namespace MCGalaxy.Games
{
    public sealed class SpleefPlugin : Plugin
    {
        public override string creator { get { return "icanttellyou"; } }
        public override string MCGalaxy_Version { get { return "1.9.4.6"; } }
        public override string name { get { return "Spleef"; } }

        Command cmd;
        public override void Load(bool startup)
        {
            OnPlayerSpawningEvent.Register(HandlePlayerSpawning, Priority.High);
            OnJoinedLevelEvent.Register(HandleOnJoinedLevel, Priority.High);
            OnBlockChangingEvent.Register(HandleBlockChanged, Priority.High);

            SpleefGame.Instance.Config.Path = "./plugins/spleef.properties";
            cmd = new CmdSpleef();
            Command.Register(cmd);

            RoundsGame game = SpleefGame.Instance;
            game.GetConfig().Load();
            if (!game.Running) game.AutoStart();
        }

        public override void Unload(bool shutdown)
        {
            OnPlayerSpawningEvent.Unregister(HandlePlayerSpawning);
            OnJoinedLevelEvent.Unregister(HandleOnJoinedLevel);
            OnBlockChangingEvent.Unregister(HandleBlockChanged);
            Command.Unregister(cmd);
            RoundsGame game = SpleefGame.Instance;
            if (game.Running) game.End();
        }

        void HandlePlayerSpawning(Player p, ref Position pos, ref byte yaw, ref byte pitch, bool respawning)
        {
            if (!respawning || !SpleefGame.Instance.Remaining.Contains(p)) return;
            SpleefGame.Instance.Map.Message(p.ColoredName + " &Sis out of spleef!");
            SpleefGame.Instance.OnPlayerDied(p);
        }

        void HandleOnJoinedLevel(Player p, Level prevLevel, Level level, ref bool announce)
        {
            if (prevLevel == SpleefGame.Instance.Map && level != SpleefGame.Instance.Map)
            {
                if (SpleefGame.Instance.Picker.Voting) SpleefGame.Instance.Picker.ResetVoteMessage(p);
                p.SendCpeMessage(CpeMessageType.Status1, "");
                p.SendCpeMessage(CpeMessageType.Status2, "");
                p.SendCpeMessage(CpeMessageType.Status3, "");
                SpleefGame.Instance.PlayerLeftGame(p);
            }
            else if (level == SpleefGame.Instance.Map)
            {
                if (SpleefGame.Instance.Picker.Voting) SpleefGame.Instance.Picker.SendVoteMessage(p);
            }

            if (level != SpleefGame.Instance.Map) return;

            if (prevLevel == SpleefGame.Instance.Map || SpleefGame.Instance.LastMap.Length == 0)
            {
                announce = false;
            }
            else if (prevLevel != null && prevLevel.name.CaselessEq(SpleefGame.Instance.LastMap))
            {
                // prevLevel is null when player joins main map
                announce = false;
            }
        }

        void HandleBlockChanged(Player p, ushort x, ushort y, ushort z, BlockID block, bool placing, ref bool cancel)
        {
            if (SpleefGame.Instance.Running && !SpleefGame.Instance.Remaining.Contains(p) && SpleefGame.Instance.Map == p.level)
            {
                p.Message("You are out of the round, and cannot break blocks.");
                p.RevertBlock(x, y, z);
                cancel = true;
                return;
            }
        }
    }

    public sealed class SpleefConfig : RoundsGameConfig
    {
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
            Picker = new LevelPicker();
        }

        public override void UpdateMapConfig() { }

        protected override List<Player> GetPlayers()
        {
            List<Player> playing = new List<Player>();
            playing.AddRange(Players.Items);
            return playing;
        }

        public override void OutputStatus(Player p)
        {
            Player[] players = Players.Items;
            p.Message("Players in spleef:");

            if (RoundInProgress)
            {
                p.Message(players.Join(pl => FormatPlayer(pl)));
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
                Chat.MessageFrom(p, "λNICK &Sjoined spleef!");
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
            RunRound();
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

        void RunRound()
        {
            while (RoundInProgress && Running && Remaining.Count > 0) ;
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
                    Map.Message(players[0].ColoredName + " &Sis the winner!");
                    EndRound(players[0]);
                    break;
                case 2:
                    Map.Message("Only 2 Players left:");
                    Map.Message(players[0].ColoredName + " &Sand " + players[1].ColoredName);
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
                winner.SendCpeMessage(CpeMessageType.BigAnnouncement, "&SYou win!");
                winner.SendCpeMessage(CpeMessageType.SmallAnnouncement, "&T+30 &S" + Server.Config.Currency + "!");
                winner.SetMoney(winner.money + 30);
                winner.Message("Congratulations, you won this round of spleef!");
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
                base.Use(p, message, data);
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

        static string FormatPlayer(Player pl, SpleefGame game)
        {
            string suffix = game.Remaining.Contains(pl) ? " &a[IN]" : " &c[OUT]";
            return pl.ColoredName + suffix;
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

        protected override void HandleStart(Player p, RoundsGame game_, string[] args)
        {
            if (game_.Running) { p.Message("{0} is already running", game_.GameName); return; }

            SpleefGame game = (SpleefGame)game_;
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

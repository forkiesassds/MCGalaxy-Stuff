//reference System.Core.dll
using System;
using System.Linq;
using MCGalaxy;
using MCGalaxy.Events.PlayerEvents;
using MCGalaxy.SQL;

namespace VeryPlugins
{
    public class LoginToLastPosPlugin : Plugin
    {
        public override string name => "LoginToLastPos";
        public override string creator => "icanttellyou";
        public override string MCGalaxy_Version => "1.9.4.9";

        public override void Load(bool auto)
        {
            Database.CreateTable("LastPos", new ColumnDesc[]
            {
                new ColumnDesc("Name", ColumnType.Char, 20),
                new ColumnDesc("Map", ColumnType.VarChar),
                new ColumnDesc("X", ColumnType.Int32),
                new ColumnDesc("Y", ColumnType.Int32),
                new ColumnDesc("Z", ColumnType.Int32),
                new ColumnDesc("Yaw", ColumnType.Int8),
                new ColumnDesc("Pitch", ColumnType.Int8)
            });

            OnPlayerFinishConnectingEvent.Register(HandleOnPlayerFinishConnecting, Priority.High);
            OnJoinedLevelEvent.Register(HandleOnJoinedLevelEvent, Priority.High);
            OnPlayerDisconnectEvent.Register(HandleOnPlayerDisconnect, Priority.High);
        }

        public override void Unload(bool auto)
        {
            OnPlayerFinishConnectingEvent.Unregister(HandleOnPlayerFinishConnecting);
            OnJoinedLevelEvent.Unregister(HandleOnJoinedLevelEvent);
            OnPlayerDisconnectEvent.Unregister(HandleOnPlayerDisconnect);
        }

        private void HandleOnPlayerFinishConnecting(Player p)
        {
            string map = null;
            Database.ReadRows("LastPos", "Map, X, Y, Z, Yaw, Pitch", record =>
            {
                map = record.GetText("Map");
                p.Extras["LAST_X"] = record.GetInt("X");
                p.Extras["LAST_Y"] = record.GetInt("Y");
                p.Extras["LAST_Z"] = record.GetInt("Z");
                p.Extras["LAST_YAW"] = record.GetInt("Yaw");
                p.Extras["LAST_PITCH"] = record.GetInt("Pitch");
            }, "WHERE Name=@0", new string[] { p.name });
            if (map == null) { ClearExtraFields(p); return; }

            if (!LevelInfo.AllMapNames().Contains(map))
            {
                p.Message("&WThe map you were on no longer exists!");
                ClearExtraFields(p); return;
            }

            Level lvl = LevelInfo.FindExact(map);
            if (lvl == null)
            {
                if (!Server.Config.AutoLoadMaps) { ClearExtraFields(p); return; }

                string propsPath = LevelInfo.PropsPath(map);
                LevelConfig cfg = new LevelConfig();
                cfg.Load(propsPath);

                AccessController visitAccess = new LevelAccessController(cfg, map, true);
                if (!visitAccess.CheckDetailed(p, p.Rank) || cfg.MOTD.Contains("-lastpos")) { ClearExtraFields(p); return; }

                lvl = LevelActions.Load(p, map, false);

                if (lvl == null)
                {
                    p.Message("&WFailed to load the map you were on!");
                    ClearExtraFields(p); return;
                }
            }

            bool canJoin = lvl.CanJoin(p);
            //We are joining a level right? As it's not the main level.
            OnJoiningLevelEvent.Call(p, lvl, ref canJoin);
            if (!canJoin || lvl.Config.MOTD.Contains("-lastpos")) { ClearExtraFields(p); return; }

            p.level = lvl;
        }

        private static void ClearExtraFields(Player p)
        {
            p.Extras.Remove("LAST_X");
            p.Extras.Remove("LAST_Y");
            p.Extras.Remove("LAST_Z");
            p.Extras.Remove("LAST_YAW");
            p.Extras.Remove("LAST_PITCH");
        }

        private void HandleOnJoinedLevelEvent(Player p, Level prev, Level lvl, ref bool announce)
        {
            if (prev != null) return;

            if (p.Extras.Contains("LAST_X") && p.Extras.Contains("LAST_Y") && p.Extras.Contains("LAST_Z") &&
                p.Extras.Contains("LAST_YAW") && p.Extras.Contains("LAST_PITCH"))
            {
                Position newPos = new Position(p.Extras.GetInt("LAST_X"), p.Extras.GetInt("LAST_Y"), p.Extras.GetInt("LAST_Z"));
                Orientation newOr = new Orientation((byte)p.Extras.GetInt("LAST_YAW"), (byte)p.Extras.GetInt("LAST_PITCH"));

                p.SendPosition(newPos, newOr);
            }
        }

        private void HandleOnPlayerDisconnect(Player p, string reason)
        {
            object[] args = new object[] { p.level.name, p.Pos.X, p.Pos.Y, p.Pos.Z, p.Rot.RotY, p.Rot.HeadX, p.name };

            int changed = Database.UpdateRows("LastPos", "Map=@0, X=@1, Y=@2, Z=@3, Yaw=@4, Pitch=@5", "WHERE Name=@6", args);

            if (changed == 0)
            {
                Database.AddRow("LastPos", "Map, X, Y, Z, Yaw, Pitch, Name", args);
            }
        }
    }
}

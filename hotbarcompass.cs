//INSTALL INSTRUCTIONS
//Make level blocks 100-107 going clockwise from bottom. For instance: bottom, bottom-left, left, etc.
//Use +compass in the motd to use this.
using System;
using System.Threading;

using MCGalaxy;
using MCGalaxy.Tasks;
using MCGalaxy.Network;
using BlockID = System.UInt16;

namespace MCGalaxy {

    public class Compass : Plugin {
        public override string creator { get { return "TomCube"; } }
        public override string MCGalaxy_Version { get { return "1.9.2.8"; } }
        public override string name { get { return "HotbarCompass"; } }
		SchedulerTask tak;
        public override void Load(bool startup) {
            Server.MainScheduler.QueueRepeat(CheckDirection, null, TimeSpan.FromMilliseconds(100));
        }
		public override void Unload(bool shutdown) {
			Server.MainScheduler.Cancel(tak);
		}

        void CheckDirection(SchedulerTask tak) {
            Player[] players = PlayerInfo.Online.Items;
            foreach (Player p in players) {
				int rotationofplayer = Orientation.PackedToDegrees(p.Rot.RotY);
				if (!p.Supports(CpeExt.SetHotbar)) { continue; }
				bool hasinf = p.hasExtBlocks;
				if (p.level.Config.MOTD.ToLower().Contains("+compass")) {
					if (rotationofplayer >= 339 && rotationofplayer < 361 || rotationofplayer >= 0 && rotationofplayer < 33) {
						p.Send(Packet.SetHotbar(100, 8, hasinf));
					}

					else if (rotationofplayer >= 33 && rotationofplayer < 68) {
						p.Send(Packet.SetHotbar(101, 8, hasinf));
					}

					else if (rotationofplayer >= 68 && rotationofplayer < 113) {
						p.Send(Packet.SetHotbar(102, 8, hasinf));
					}

					else if (rotationofplayer >= 113 && rotationofplayer < 158) {
						p.Send(Packet.SetHotbar(103, 8, hasinf));
					}

					else if (rotationofplayer >= 153 && rotationofplayer < 203) {
						p.Send(Packet.SetHotbar(104 , 8, hasinf));
					}

					else if (rotationofplayer >= 203 && rotationofplayer < 258) {
						p.Send(Packet.SetHotbar(105, 8, hasinf));
					}

					else if (rotationofplayer >= 258 && rotationofplayer < 293) {
						p.Send(Packet.SetHotbar(106, 8, hasinf));
					}

					else if (rotationofplayer >= 293 && rotationofplayer < 339) {
						p.Send(Packet.SetHotbar(107, 8, hasinf));
					}
				}
            }
        }

        
    }
}
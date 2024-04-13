using System;
using LibNoise;
using MCGalaxy;
using MCGalaxy.Generator;

namespace VeryPlugins
{
    public sealed class PluginFancyVoronoiGen : Plugin
    {
        public override string name { get { return "PluginFancyVoronoiGen"; } }
        public override string MCGalaxy_Version { get { return "1.9.4.8"; } }
        public override string creator { get { return ""; } }


        public override void Load(bool startup)
        {
            MapGen.Register("FancyVoronoi", GenType.Advanced, GenVoronoi, MapGen.DEFAULT_HELP);
        }
        public override void Unload(bool shutdown)
        {
            if (MapGen.Generators.RemoveAll(gen => gen.Theme == "FancyVoronoi") == 0)
            {
                Logger.Log(LogType.Warning, "Unable to un-register world type, either the world type was never registered, got removed or other causes. Restart the server to avoid issues!");
            }
        }

        static bool GenVoronoi(Player p, Level lvl, MapGenArgs args)
        {
            Voronoi v = new Voronoi();

            int width = lvl.Width / 4, length = lvl.Length / 4, half = lvl.Height / 2;
            int waterHeight = half - 1;
            v.Frequency = 1 / 25.0;

            if (!args.ParseArgs(p)) return false;
            v.Seed = args.Seed;
            MapGenBiome biome = MapGenBiome.Get(args.Biome);

            for (int xS = 0; xS < width; ++xS)
                for (int zS = 0; zS < length; ++zS)
                {
                    double x0z0 = v.GetValue(xS    , 10, zS    ) * 10;
                    double x0z1 = v.GetValue(xS    , 10, zS + 1) * 10;
                    double x1z0 = v.GetValue(xS + 1, 10, zS    ) * 10;
                    double x1z1 = v.GetValue(xS + 1, 10, zS + 1) * 10;

                    for (int xP = 0; xP < 4; ++xP)
                    {
                        double xLerp = xP / 4.0D;

                        double z0 = x0z0 + (x1z0 - x0z0) * xLerp;
                        double z1 = x0z1 + (x1z1 - x0z1) * xLerp;

                        for (int zP = 0; zP < 4; ++zP)
                        {
                            double zLerp = zP / 4.0D;

                            double lerpedValue = z0 + (z1 - z0) * zLerp;

                            int dirtHeight = (int)Math.Floor(lerpedValue) + half;

                            if (dirtHeight < waterHeight)
                            {
                                // column is underwater
                                for (int y = waterHeight; y >= dirtHeight; y--)
                                {
                                    lvl.SetTile((ushort)(xS * 4 + xP), (ushort)y, (ushort)(zS * 4 + zP), biome.Water);
                                }
                            }
                            else
                            {
                                // top of column is above water
                                int sandHeight = (int)Math.Floor(lerpedValue * 1.5) + half;
                                byte topBlock = dirtHeight < sandHeight ? biome.Surface : biome.BeachSandy;
                                lvl.SetTile((ushort)(xS * 4 + xP), (ushort)dirtHeight, (ushort)(zS * 4 + zP), topBlock);
                            }

                            for (int y = dirtHeight - 1; y >= 0; y--)
                            {
                                byte block = (y > dirtHeight * 3 / 4) ? biome.Ground : biome.Cliff;
                                lvl.SetTile((ushort)(xS * 4 + xP), (ushort)y, (ushort)(zS * 4 + zP), block);
                            }
                        }
                    }
                }
            return true;
        }
    }
}

//reference System.Core.dll
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

using MCGalaxy;
using MCGalaxy.Commands;
using MCGalaxy.Generator;
using MCGalaxy.Generator.Foliage;

namespace VeryPlugins
{
    public sealed class PluginAlphaGen : Plugin
    {
        public override string name { get { return "PluginAlphaGen"; } }
        public override string MCGalaxy_Version { get { return "1.9.5.0"; } }
        public override string creator { get { return ""; } }


        public override void Load(bool startup)
        {
            //HACK: why do messages not support newlines???
            MapGen.Register("alphaGen", GenType.Advanced, AlphaGen.Gen,
                "The map generator supports defining arguments. " +
                LineString("For example: \"genCaves=false,theme=Arctic,seed=Glacier\" generates a map without any caves" +
                ", with the Arctic theme and using the seed \"Glacier\".") +
                LineString("The following arguments are available:") +
                LineString("genCaves (bool): whether or not to generate caves.") +
                LineString("theme (many values): the theme of the map.") +
                LineString("seed (long integer or string): the seed of the map") +
                LineString("xChunkOffset (integer) offset of chunk position in the x direction") +
                LineString("zChunkOffset (integer) offset of chunk position in the z direction") +
                LineString("doIslands (bool): if the terrain generated follows an island shape") +
                LineString("islandShape (many values): the shape of the island, default: Circle.") + 
                LineString(IslandShape.ListShapes() + "&S") + 
                LineString("islandRadius (int): radius of island, default: width/32 - defaultFalloffDist") +
                LineString("islandFalloffDist (int): falloff distance of island, default: 8 ") +
                LineString("islandSlide (double): adjusts how harsh the falloff is, default: -200"));
        }
        public override void Unload(bool shutdown)
        {
            if (MapGen.Generators.RemoveAll(gen => gen.Theme == "alphaGen") == 0)
            {
                Logger.Log(LogType.Warning, "Unable to un-register world type, either the world type was never registered, got removed or other causes. Restart the server to avoid issues!");
            }
        }

        public static string LineString(string str)
        {
            return str + "".PadRight(64 - (str.Length & 63), ' ');
        }
    }

    public class MapGenArgsHack : MapGenArgs
    {
        public class GenArgs
        {
            public bool GenCaves = true;
            public string Biome;
            public bool HasSeed = false;
            public long LongSeed = 0;
            public int xChunkOffset = 0;
            public int zChunkOffset = 0;
            public bool DoIslands = false;
            public DistanceProvider IslandProvider = IslandShape.Providers["Circle"];
            public int IslandRadius = 16;
            public int IslandFalloffDistance = 8;
            public double IslandOceanSlideTarget = -200.0D;

            public GenArgs()
            {
                Biome = Server.Config.DefaultMapGenBiome;
            }
        }

        public delegate bool GenArgSelector(Player p, string arg, ref GenArgs args);
        public GenArgs ArgsForGen = new GenArgs();

        public new MapGenArgSelector ArgFilter = (arg) => arg.Contains('=');
        public new GenArgSelector ArgParser = (Player p, string arg, ref GenArgs args) =>
        {
            string[] split = arg.Split('=');
            string key = split[0];
            string value = split.Skip(1).Join("=");


            //TODO: add more options
            switch (key)
            {
                case "genCaves":
                    if (!bool.TryParse(value, out args.GenCaves))
                    {
                        p.Message("Value " + value + " is not a valid value for genCaves!");
                        return false;
                    }
                    break;
                case "theme":
                    args.Biome = MapGenBiome.FindMatch(p, arg);
                    if (args.Biome == null) return false;
                    break;
                case "seed":
                    if (!long.TryParse(value, out args.LongSeed))
                        args.LongSeed = value.JavaStringHashCode();

                    args.HasSeed = true;
                    break;
                case "xChunkOffset":
                    if (!int.TryParse(value, out args.xChunkOffset))
                    {
                        p.Message("Value " + value + " is not a valid value for xChunkOffset!");
                        return false;
                    }
                    break;
                case "zChunkOffset":
                    if (!int.TryParse(value, out args.zChunkOffset))
                    {
                        p.Message("Value " + value + " is not a valid value for zChunkOffset!");
                        return false;
                    }
                    break;
                case "doIslands":
                    if (!bool.TryParse(value, out args.DoIslands))
                    {
                        p.Message("Value " + value + " is not a valid value for doIslands!");
                        return false;
                    }
                    break;
                case "islandShape":
                    DistanceProvider provider = IslandShape.GetDistanceProvider(p, value);
                    if (provider == null)
                        return false;

                    args.IslandProvider = provider;
                    break;
                case "islandRadius":
                    if (!int.TryParse(value, out args.IslandRadius))
                    {
                        p.Message("Value " + value + " is not a valid value for islandRadius!");
                        return false;
                    }
                    break;
                case "islandFalloffDist":
                    if (!int.TryParse(value, out args.IslandFalloffDistance))
                    {
                        p.Message("Value " + value + " is not a valid value for islandFalloffDist!");
                        return false;
                    }
                    break;
                case "islandSlide":
                    if (!double.TryParse(value, out args.IslandOceanSlideTarget))
                    {
                        p.Message("Value " + value + " is not a valid value for islandSlide!");
                        return false;
                    }
                    break;
            }
            return true;
        };

        private ushort width;
        private ushort height;
        private ushort length;

        public MapGenArgsHack(ushort width, ushort height, ushort length)
        {
            this.width = width;
            this.height = height;
            this.length = length;
        }

        public new bool ParseArgs(Player p)
        {
            ArgsForGen.IslandRadius = (width / (16 * 2)) - ArgsForGen.IslandFalloffDistance;
            ArgsForGen.xChunkOffset = -(width / (16 * 2));
            ArgsForGen.zChunkOffset = -(length / (16 * 2));

            foreach (string arg in Args.Split(','))
            {
                if (arg.Length == 0) continue;

                if (ArgFilter(arg))
                {
                    if (!ArgParser(p, arg, ref ArgsForGen)) return false;
                }
                else if (long.TryParse(arg, out ArgsForGen.LongSeed))
                {
                    ArgsForGen.HasSeed = true;
                }
                else
                {
                    Biome = MapGenBiome.FindMatch(p, arg);
                    if (Biome == null) return false;
                }
            }

            if (!ArgsForGen.HasSeed) ArgsForGen.LongSeed = RandomDefault ? new JavaRandom().NextLong() : -1;
            return true;
        }
    }

    public delegate double DistanceProvider(int x, int z);
    public static class IslandShape
    {
        public static Dictionary<string, DistanceProvider> Providers = new Dictionary<string, DistanceProvider>()
        {
            { "Circle", (x, z) => Math.Sqrt(x * x + z * z) },
            { "Square", (x, z) => Math.Max(Math.Abs(x), Math.Abs(z)) }
        };

        public static DistanceProvider GetDistanceProvider(Player p, string id)
        {
            int matches = 0;
            var match = Matcher.Find(p, id, out matches, Providers, 
                                        null, b => b.Key, "island shape");
            
            if (match.Value == null && matches == 0) p.Message(ListShapes());
            return match.Value;
        }

        public static string ListShapes() 
        {
            return "&HAvailable island shapes: &f" + Providers.Join(b => b.Key);
        }
    }

    public static class AlphaGen
    {
        public static bool Gen(Player p, Level lvl, MapGenArgs mgArgs)
        {
            MapGenArgsHack hack = new MapGenArgsHack(lvl.Width, lvl.Height, lvl.Length);
            hack.Args = mgArgs.Args;

            if (!hack.ParseArgs(p)) return false;
            string theme = hack.ArgsForGen.Biome;
            long rngSeed = hack.ArgsForGen.LongSeed;

            MapGenBiome theme2 = MapGenBiome.Get(theme);
            theme2.ApplyEnv(lvl.Config);

            int width = (int)Math.Ceiling(lvl.Width / 16.0D);
            int length = (int)Math.Ceiling(lvl.Length / 16.0D);

            GenWorld world = new GenWorld(width, length, lvl.Height, theme2, hack.ArgsForGen, lvl);

            ChunkBasedOctaveGenerator generator = new ChunkBasedOctaveGenerator(world, rngSeed);
            world.chunkGenerator = generator;

            p.Message("Beginning generation of world with seed \"" + rngSeed + "\"");

            int totalChunks = width * length;
            int chunksGenerated = 1;

            for (ushort chunkZ = 0; chunkZ < length; chunkZ++)
            {
                for (ushort chunkX = 0; chunkX < width; chunkX++)
                {
                    p.Message(string.Format("Generating chunk {0} out of {1}", chunksGenerated, totalChunks));
                    world.GetBlock(chunkX << 4, 1, chunkZ << 4);
                    chunksGenerated++;
                }
            }

            p.Message("Copying chunks to level");
            for (int chunkX = 0; chunkX < width; chunkX++)
            {
                for (int chunkZ = 0; chunkZ < length; chunkZ++)
                {
                    GenChunk chunk = world.chunks[(chunkX * length) + chunkZ];

                    if (chunk == null)
                    {
                        world.GetBlock(chunkX << 4, 1, chunkZ << 4);
                        chunk = world.chunks[(chunkX * length) + chunkZ];
                    }

                    for (ushort x = 0; x < 16; x++)
                        for (ushort y = 0; y < lvl.Height; y++)
                            for (ushort z = 0; z < 16; z++)
                                lvl.SetBlock((ushort)(chunkX << 4 | x), y, (ushort)(chunkZ << 4 | z), (ushort)chunk.GetBlock(x & 15, y, z & 15));
                }
            }
            return true;
        }
    }

    public class ChunkBasedOctaveGenerator
    {
        private JavaRandom rand;
        private NoiseGeneratorOctaves minLimitNoise;
        private NoiseGeneratorOctaves maxLimitNoise;
        private NoiseGeneratorOctaves mainNoise;
        private NoiseGeneratorOctaves beachNoise;
        private NoiseGeneratorOctaves surfaceHeightNoise;
        private NoiseGeneratorOctaves scaleNoise;
        private NoiseGeneratorOctaves depthNoise;
        private NoiseGeneratorOctaves treeDensityNoise;
        private MapGenBase caveGenerator = new MapGenCaves();
        private double[] noiseArray;
        double[] mainNoiseSample;
        double[] minLimitNoiseSample;
        double[] maxLimitNoiseSample;
        double[] scaleNoiseSample;
        double[] depthNoiseSample;
        GenWorld worldObj;

        public ChunkBasedOctaveGenerator(GenWorld world, long seed)
        {
            worldObj = world;
            rand = new JavaRandom(seed);
            minLimitNoise = new NoiseGeneratorOctaves(rand, 16);
            maxLimitNoise = new NoiseGeneratorOctaves(rand, 16);
            mainNoise = new NoiseGeneratorOctaves(rand, 8);
            beachNoise = new NoiseGeneratorOctaves(rand, 4);
            surfaceHeightNoise = new NoiseGeneratorOctaves(rand, 4);
            scaleNoise = new NoiseGeneratorOctaves(rand, 10);
            depthNoise = new NoiseGeneratorOctaves(rand, 16);
            treeDensityNoise = new NoiseGeneratorOctaves(rand, 8);
        }

        private void generateTerrain(int chunkX, int chunkZ, ref byte[] blocks)
        {
            byte horSamples = 4;
            int verSamples = worldObj.wHeight / 8;
            int seaLevel = worldObj.wHeight / 2;
            int xSamples = horSamples + 1;
            int ySamples = verSamples + 1;
            int zSamples = horSamples + 1;
            noiseArray = initializeNoiseField(noiseArray, chunkX * horSamples, 0, chunkZ * horSamples, xSamples, ySamples, zSamples);

            for(int xS = 0; xS < horSamples; ++xS) 
            {
                for(int zS = 0; zS < horSamples; ++zS) 
                {
                    for(int yS = 0; yS < verSamples; ++yS) 
                    {
                        double x0y0z0 = noiseArray[( xS      * zSamples + zS    ) * ySamples + yS    ];
                        double x0y0z1 = noiseArray[( xS      * zSamples + zS + 1) * ySamples + yS    ];
                        double x1y0z0 = noiseArray[((xS + 1) * zSamples + zS    ) * ySamples + yS    ];
                        double x1y0z1 = noiseArray[((xS + 1) * zSamples + zS + 1) * ySamples + yS    ];
                        double x0y1z0 = noiseArray[( xS      * zSamples + zS    ) * ySamples + yS + 1];
                        double x0y1z1 = noiseArray[( xS      * zSamples + zS + 1) * ySamples + yS + 1];
                        double x1y1z0 = noiseArray[((xS + 1) * zSamples + zS    ) * ySamples + yS + 1];
                        double x1y1z1 = noiseArray[((xS + 1) * zSamples + zS + 1) * ySamples + yS + 1];


                        for(int yP = 0; yP < 8; ++yP) 
                        {
                            double yLerp = yP / 8.0D;

                            double x0z0 = x0y0z0 + (x0y1z0 - x0y0z0) * yLerp;
                            double x0z1 = x0y0z1 + (x0y1z1 - x0y0z1) * yLerp;
                            double x1z0 = x1y0z0 + (x1y1z0 - x1y0z0) * yLerp;
                            double x1z1 = x1y0z1 + (x1y1z1 - x1y0z1) * yLerp;


                            for(int xP = 0; xP < 4; ++xP) 
                            {
                                double xLerp = xP / 4.0D;

                                double z0 = x0z0 + (x1z0 - x0z0) * xLerp;
                                double z1 = x0z1 + (x1z1 - x0z1) * xLerp;

                                for(int zP = 0; zP < 4; ++zP) 
                                {
                                    double zLerp = zP / 4.0D;

                                    double lerpedValue = z0 + (z1 - z0) * zLerp;

                                    int xR = xS << 2 | xP;
                                    int yR = yS << 3 | yP;
                                    int zR = zS << 2 | zP;
                                    int idx = yR << 8 | zR << 4 | xR;

                                    byte b = 0;

                                    if (lerpedValue > 0.0D) 
                                    {
                                        b = worldObj.theme.Cliff;
                                    } 
                                    else if (yR < seaLevel) 
                                    {
                                        b = worldObj.theme.Water;
                                    } 

                                    blocks[idx] = b;
                                }
                            }
                        }
                    }
                }
            }
        }

        private void replaceSurfaceBlocks(int chunkX, int chunkZ, ref byte[] blocks)
        {
            int seaLevel = worldObj.wHeight / 2;
            double surfaceCoordScale = 8.0D / 256D;

            for (int x = 0; x < 16; ++x)
            {
                for (int z = 0; z < 16; ++z)
                {
                    double xW = (chunkX << 4) + x;
                    double zW = (chunkZ << 4) + z;
                    bool generateSandBeach = beachNoise.generateNoise(xW * surfaceCoordScale, zW * surfaceCoordScale, 0.0D) + rand.NextDouble() * 0.2D > 0D; //sand
                    bool generateGravelBeach = beachNoise.generateNoise(zW * surfaceCoordScale, surfaceCoordScale, xW * surfaceCoordScale) + rand.NextDouble() * 0.2D > 3D; //gravel
                    int heightLevel = (int)(surfaceHeightNoise.generateNoise(xW * surfaceCoordScale * 2D, zW * surfaceCoordScale * 2D) / 3D + 3D + rand.NextDouble() * 0.25D);
                    int height = -1;
                    byte surface = worldObj.theme.Surface;
                    byte ground = worldObj.theme.Ground;
                    byte cliff = worldObj.theme.Cliff;
                    byte water = worldObj.theme.Water;
                    if (water == 0) seaLevel = 0;

                    for (int y = worldObj.wHeight - 1; y >= 0; --y)
                    {
                        int index = y << 8 | z << 4 | x;
                        byte block = blocks[index];
                        if (block == 0)
                        {
                            height = -1;
                        }
                        else if (block == cliff)
                        {
                            if (height == -1)
                            {
                                if (heightLevel <= 0)
                                {
                                    surface = 0;
                                    ground = cliff;
                                }
                                else if (y >= seaLevel - 4 && y <= seaLevel + 1)
                                {
                                    if (generateGravelBeach)
                                    {
                                        surface = 0;
                                        ground = worldObj.theme.BeachRocky;
                                    }

                                    if (generateSandBeach)
                                    {
                                        surface = worldObj.theme.BeachSandy;
                                        ground = worldObj.theme.BeachSandy;
                                    }
                                }

                                if (y < seaLevel && surface == 0)
                                {
                                    surface = water;
                                }

                                height = heightLevel;
                                if (y >= seaLevel - 1)
                                {
                                    blocks[index] = surface;
                                }
                                else
                                {
                                    blocks[index] = ground;
                                }
                            }
                            else if (height > 0)
                            {
                                --height;
                                blocks[index] = ground;
                            }
                        }
                    }
                }
            }

        }

        public GenChunk generateChunk(int i1, int i2)
        {
            if (worldObj.chunks[(i1 * worldObj.zChTotal) + i2] != null) return worldObj.chunks[(i1 * worldObj.zChTotal) + i2];

            byte[] chunkBlocks = new byte[256 * worldObj.wHeight];

            rand.SetSeed((i1 + worldObj.genArgs.xChunkOffset) * 341873128712L + (i2 + worldObj.genArgs.zChunkOffset) * 132897987541L);

            generateTerrain(i1 + worldObj.genArgs.xChunkOffset, i2 + worldObj.genArgs.zChunkOffset, ref chunkBlocks);
            replaceSurfaceBlocks(i1 + worldObj.genArgs.xChunkOffset, i2 + worldObj.genArgs.zChunkOffset, ref chunkBlocks);
            if (worldObj.genArgs.GenCaves) 
                caveGenerator.generate(worldObj, i1 + worldObj.genArgs.xChunkOffset, i2 + worldObj.genArgs.zChunkOffset, ref chunkBlocks);

            GenChunk chunk = new GenChunk(worldObj, i1, i2, chunkBlocks);

            return chunk;
        }
        private double[] initializeNoiseField(double[] densityMap, int xStart, int yStart, int zStart, int xSamples, int ySamples, int zSamples)
        {
            if (densityMap == null)
            {
                densityMap = new double[xSamples * ySamples * zSamples];
            }

            double horCoordScale = 684.412D;
            double vertCoordScale = 684.412D;
            scaleNoiseSample = scaleNoise.generateNoiseOctaves(scaleNoiseSample, xStart, yStart, zStart, xSamples, 1, zSamples, 1.0D, 0.0D, 1.0D);
            depthNoiseSample = depthNoise.generateNoiseOctaves(depthNoiseSample, xStart, yStart, zStart, xSamples, 1, zSamples, 100.0D, 0.0D, 100.0D);
            mainNoiseSample = mainNoise.generateNoiseOctaves(mainNoiseSample, xStart, yStart, zStart, xSamples, ySamples, zSamples, horCoordScale / 80.0D, vertCoordScale / 160.0D, horCoordScale / 80.0D);
            minLimitNoiseSample = minLimitNoise.generateNoiseOctaves(minLimitNoiseSample, xStart, yStart, zStart, xSamples, ySamples, zSamples, horCoordScale, vertCoordScale, horCoordScale);
            maxLimitNoiseSample = maxLimitNoise.generateNoiseOctaves(maxLimitNoiseSample, xStart, yStart, zStart, xSamples, ySamples, zSamples, horCoordScale, vertCoordScale, horCoordScale);
            int index = 0;
            int scaleDepthIndex = 0;

            for (int x = 0; x < xSamples; ++x)
            {
                for (int z = 0; z < zSamples; ++z)
                {
                    double islandOffset = GetIslandOffset(x + xStart, z + zStart);

                    double scale = (scaleNoiseSample[scaleDepthIndex] + 256.0D) / 512.0D;
                    if (scale > 1.0D)
                    {
                        scale = 1.0D;
                    }

                    double d18 = 0.0D;
                    double depth = depthNoiseSample[scaleDepthIndex] / 8000.0D;
                    if (depth < 0.0D)
                    {
                        depth = -depth;
                    }

                    depth = depth * 3.0D - 3.0D;
                    if (depth < 0.0D)
                    {
                        depth /= 2.0D;
                        if (depth < -1.0D)
                        {
                            depth = -1.0D;
                        }

                        depth /= 1.4D;
                        depth /= 2.0D;
                        scale = 0.0D;
                    }
                    else
                    {
                        if (depth > 1.0D)
                        {
                            depth = 1.0D;
                        }

                        depth /= 6.0D;
                    }

                    scale += 0.5D;
                    depth = depth * ySamples / 16.0D;
                    double d22 = ySamples / 2.0D + depth * 4.0D;
                    ++scaleDepthIndex;

                    for (int y = 0; y < ySamples; ++y)
                    {
                        double offset = (y - d22) * 12.0D / scale;
                        if (offset < 0.0D)
                        {
                            offset *= 4.0D;
                        }

                        double min = minLimitNoiseSample[index] / 512.0D;
                        double max = maxLimitNoiseSample[index] / 512.0D;
                        double main = (mainNoiseSample[index] / 10.0D + 1.0D) / 2.0D;
                        double density;
                        if (main < 0.0D)
                        {
                            density = min;
                        }
                        else if (main > 1.0D)
                        {
                            density = max;
                        }
                        else
                        {
                            density = min + (max - min) * main;
                        }

                        density -= offset;
                        density += islandOffset;
                        double d35;
                        if (y > ySamples - 4)
                        {
                            d35 = (y - (ySamples - 4)) / 3.0D;
                            density = density * (1.0D - d35) + -10.0D * d35;
                        }

                        if (y < d18)
                        {
                            d35 = (d18 - y) / 4.0D;
                            if (d35 < 0.0D)
                            {
                                d35 = 0.0D;
                            }

                            if (d35 > 1.0D)
                            {
                                d35 = 1.0D;
                            }

                            density = density * (1.0D - d35) + -10.0D * d35;
                        }

                        densityMap[index] = density;
                        ++index;
                    }
                }
            }

            return densityMap;
        }

        protected double GetIslandOffset(int noiseX, int noiseZ) 
        {
            MapGenArgsHack.GenArgs genArgs = worldObj.genArgs;

            if (!genArgs.DoIslands) 
            {
                return 0.0D;
            }

            double distance = genArgs.IslandProvider(noiseX, noiseZ);
            double oceanSlideTarget = genArgs.IslandOceanSlideTarget;

            int centerIslandRadius = genArgs.IslandRadius * 4;
            int centerIslandFalloffDistance = genArgs.IslandFalloffDistance * 4;
            
            double islandDelta = (distance - centerIslandRadius) / centerIslandFalloffDistance;
            double islandOffset = MathHelper.clampedLerp(0.0, oceanSlideTarget, islandDelta);

            return islandOffset;
        }

        public void Populate(int chunkX, int chunkZ)
        {
            GenChunk chunk = worldObj.chunks[(chunkX * worldObj.zChTotal) + chunkZ];
            if (chunk.isTerrainPopulated) return;

            chunk.isTerrainPopulated = true;

            int i4 = chunkX * 16;
            int i5 = chunkZ * 16;
            rand.SetSeed(worldObj.genArgs.LongSeed);
            long j6 = rand.NextLong() / 2L * 2L + 1L;
            long j8 = rand.NextLong() / 2L * 2L + 1L;
            rand.SetSeed((chunkX + worldObj.genArgs.xChunkOffset) * j6 + (chunkZ + worldObj.genArgs.zChunkOffset) * j8 ^ worldObj.genArgs.LongSeed);
            int i12;
            int i13;
            int i14;
            int i15;

            for (i12 = 0; i12 < 20; ++i12)
            {
                i13 = i4 + rand.NextInt(16);
                i14 = rand.NextInt(worldObj.wHeight);
                i15 = i5 + rand.NextInt(16);
                new WorldGenMinable(Block.Dirt, 32).generate(worldObj, rand, i13, i14, i15);
            }

            for (i12 = 0; i12 < 10; ++i12)
            {
                i13 = i4 + rand.NextInt(16);
                i14 = rand.NextInt(worldObj.wHeight);
                i15 = i5 + rand.NextInt(16);
                new WorldGenMinable(Block.Gravel, 32).generate(worldObj, rand, i13, i14, i15);
            }

            for (i12 = 0; i12 < 20; ++i12)
            {
                i13 = i4 + rand.NextInt(16);
                i14 = rand.NextInt(worldObj.wHeight);
                i15 = i5 + rand.NextInt(16);
                new WorldGenMinable(Block.CoalOre, 16).generate(worldObj, rand, i13, i14, i15);
            }

            for (i12 = 0; i12 < 20; ++i12)
            {
                i13 = i4 + rand.NextInt(16);
                i14 = rand.NextInt(worldObj.wHeight / 2);
                i15 = i5 + rand.NextInt(16);
                new WorldGenMinable(Block.CoalOre, 8).generate(worldObj, rand, i13, i14, i15);
            }

            for (i12 = 0; i12 < 2; ++i12)
            {
                i13 = i4 + rand.NextInt(16);
                i14 = rand.NextInt(worldObj.wHeight / 4);
                i15 = i5 + rand.NextInt(16);
                new WorldGenMinable(Block.GoldOre, 8).generate(worldObj, rand, i13, i14, i15);
            }

            double d10 = 0.5D;
            i12 = (int)((treeDensityNoise.generateNoise(i4 * d10, i5 * d10) / 8.0D + rand.NextDouble() * 4.0D + 4.0D) / 3.0D);
            if (i12 < 0)
            {
                i12 = 0;
            }

            if (rand.NextInt(10) == 0)
            {
                ++i12;
            }

            WorldGenerator object18 = null;

            if (worldObj.theme.TreeType == "") object18 = new WorldGenTrees();
            else if (worldObj.theme.TreeType != null) object18 = new WorldGenMCGTreeWrapper(Tree.TreeTypes[worldObj.theme.TreeType]());

            //big trees failed, no idea why.
            // if(this.rand.NextInt(10) == 0) {
            // 	object18 = new WorldGenBigTree();
            // }

            int i16;
            if (object18 != null)
            {
                for (i14 = 0; i14 < i12; ++i14)
                {
                    i15 = i4 + rand.NextInt(16) + 8;
                    i16 = i5 + rand.NextInt(16) + 8;
                    object18.setScale(1.0D, 1.0D, 1.0D);
                    object18.generate(worldObj, rand, i15, worldObj.GetHeightValue(i15, i16), i16);
                }
            }

            int i17;
            for (i14 = 0; i14 < 2; ++i14)
            {
                i15 = i4 + rand.NextInt(16) + 8;
                i16 = rand.NextInt(worldObj.wHeight);
                i17 = i5 + rand.NextInt(16) + 8;
                new WorldGenFlowers(Block.Dandelion).generate(worldObj, rand, i15, i16, i17);
            }

            if (rand.NextInt(2) == 0)
            {
                i14 = i4 + rand.NextInt(16) + 8;
                i15 = rand.NextInt(worldObj.wHeight);
                i16 = i5 + rand.NextInt(16) + 8;
                new WorldGenFlowers(Block.Rose).generate(worldObj, rand, i14, i15, i16);
            }

            if (rand.NextInt(4) == 0)
            {
                i14 = i4 + rand.NextInt(16) + 8;
                i15 = rand.NextInt(worldObj.wHeight);
                i16 = i5 + rand.NextInt(16) + 8;
                new WorldGenFlowers(Block.Mushroom).generate(worldObj, rand, i14, i15, i16);
            }

            if (rand.NextInt(8) == 0)
            {
                i14 = i4 + rand.NextInt(16) + 8;
                i15 = rand.NextInt(worldObj.wHeight);
                i16 = i5 + rand.NextInt(16) + 8;
                new WorldGenFlowers(Block.RedMushroom).generate(worldObj, rand, i14, i15, i16);
            }
        }
    }

    public class MapGenBase
    {
        protected int range = 8;
        protected JavaRandom rand = new JavaRandom();
        protected GenWorld worldObj;

        public void generate(GenWorld world, int chunkX, int chunkZ, ref byte[] chunkData)
        {
            int i6 = range;
            worldObj = world;
            rand.SetSeed(world.genArgs.LongSeed);
            long j7 = rand.NextLong();
            long j9 = rand.NextLong();

            for (int i11 = chunkX - i6; i11 <= chunkX + i6; ++i11)
            {
                for (int i12 = chunkZ - i6; i12 <= chunkZ + i6; ++i12)
                {
                    long j13 = i11 * j7;
                    long j15 = i12 * j9;
                    rand.SetSeed(j13 ^ j15 ^ world.genArgs.LongSeed);
                    recursiveGenerate(world, i11, i12, chunkX, chunkZ, ref chunkData);
                }
            }

        }

        protected virtual void recursiveGenerate(GenWorld world, int chunkX, int chunkZ, int originalX, int originalZ, ref byte[] chunkData)
        {
        }
    }

    public class MapGenCaves : MapGenBase
    {
        protected void generateLargeCaveNode(long randomSeed, int originalX, int originalZ, byte[] chunkData, double posX, double posY, double posZ)
        {
            generateCaveNode(randomSeed, originalX, originalZ, chunkData, posX, posY, posZ, 1.0F + rand.NextFloat() * 6.0F, 0.0F, 0.0F, -1, -1, 0.5D);
        }

        protected void generateCaveNode(long randomSeed, int originalX, int originalZ, byte[] chunkData, double posX, double posY, double posZ, float f12, float f13, float f14, int i15, int i16, double d17)
        {
            double d19 = originalX * 16 + 8;
            double d21 = originalZ * 16 + 8;
            float f23 = 0.0F;
            float f24 = 0.0F;
            JavaRandom random25 = new JavaRandom(randomSeed);
            if (i16 <= 0)
            {
                int i26 = range * 16 - 16;
                i16 = i26 - random25.NextInt(i26 / 4);
            }

            bool z54 = false;
            if (i15 == -1)
            {
                i15 = i16 / 2;
                z54 = true;
            }

            int i27 = random25.NextInt(i16 / 2) + i16 / 4;

            for (bool z28 = random25.NextInt(6) == 0; i15 < i16; ++i15)
            {
                double d29 = 1.5D + (double)(MathHelper.sin(i15 * (float)Math.PI / i16) * f12 * 1.0F);
                double d31 = d29 * d17;
                float f33 = MathHelper.cos(f14);
                float f34 = MathHelper.sin(f14);
                posX += (double)(MathHelper.cos(f13) * f33);
                posY += (double)f34;
                posZ += (double)(MathHelper.sin(f13) * f33);
                if (z28)
                {
                    f14 *= 0.92F;
                }
                else
                {
                    f14 *= 0.7F;
                }

                f14 += f24 * 0.1F;
                f13 += f23 * 0.1F;
                f24 *= 0.9F;
                f23 *= 0.75F;
                f24 += (random25.NextFloat() - random25.NextFloat()) * random25.NextFloat() * 2.0F;
                f23 += (random25.NextFloat() - random25.NextFloat()) * random25.NextFloat() * 4.0F;
                if (!z54 && i15 == i27 && f12 > 1.0F && i16 > 0)
                {
                    generateCaveNode(random25.NextLong(), originalX, originalZ, chunkData, posX, posY, posZ, random25.NextFloat() * 0.5F + 0.5F, f13 - (float)Math.PI / 2F, f14 / 3.0F, i15, i16, 1.0D);
                    generateCaveNode(random25.NextLong(), originalX, originalZ, chunkData, posX, posY, posZ, random25.NextFloat() * 0.5F + 0.5F, f13 + (float)Math.PI / 2F, f14 / 3.0F, i15, i16, 1.0D);
                    return;
                }

                if (z54 || random25.NextInt(4) != 0)
                {
                    double d35 = posX - d19;
                    double d37 = posZ - d21;
                    double d39 = i16 - i15;
                    double d41 = (double)(f12 + 2.0F + 16.0F);
                    if (d35 * d35 + d37 * d37 - d39 * d39 > d41 * d41)
                    {
                        return;
                    }

                    if (posX >= d19 - 16.0D - d29 * 2.0D && posZ >= d21 - 16.0D - d29 * 2.0D && posX <= d19 + 16.0D + d29 * 2.0D && posZ <= d21 + 16.0D + d29 * 2.0D)
                    {
                        int i55 = MathHelper.floor(posX - d29) - originalX * 16 - 1;
                        int i36 = MathHelper.floor(posX + d29) - originalX * 16 + 1;
                        int i56 = MathHelper.floor(posY - d31) - 1;
                        int i38 = MathHelper.floor(posY + d31) + 1;
                        int i57 = MathHelper.floor(posZ - d29) - originalZ * 16 - 1;
                        int i40 = MathHelper.floor(posZ + d29) - originalZ * 16 + 1;
                        if (i55 < 0)
                        {
                            i55 = 0;
                        }

                        if (i36 > 16)
                        {
                            i36 = 16;
                        }

                        if (i56 < 1)
                        {
                            i56 = 1;
                        }

                        if (i38 > worldObj.wHeight - 8)
                        {
                            i38 = worldObj.wHeight - 8;
                        }

                        if (i57 < 0)
                        {
                            i57 = 0;
                        }

                        if (i40 > 16)
                        {
                            i40 = 16;
                        }

                        bool z58 = false;
                        int x;
                        int i45;
                        for (x = i55; !z58 && x < i36; ++x)
                        {
                            for (int z = i57; !z58 && z < i40; ++z)
                            {
                                for (int y = i38 + 1; !z58 && y >= i56 - 1; --y)
                                {
                                    i45 = y << 8 | z << 4 | x;
                                    if (y >= 0)
                                    {
                                        if (y < worldObj.wHeight)
                                        {
                                            if (chunkData[i45] == worldObj.theme.Water && worldObj.theme.Water != Block.Air)
                                            {
                                                z58 = true;
                                            }

                                            if (y != i56 - 1 && x != i55 && x != i36 - 1 && z != i57 && z != i40 - 1)
                                            {
                                                y = i56;
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        if (!z58)
                        {
                            for (x = i55; x < i36; ++x)
                            {
                                double d59 = (x + originalX * 16 + 0.5D - posX) / d29;

                                for (i45 = i57; i45 < i40; ++i45)
                                {
                                    double d46 = (i45 + originalZ * 16 + 0.5D - posZ) / d29;
                                    int i48 = i38 << 8 | i45 << 4 | x;
                                    bool z49 = false;
                                    if (d59 * d59 + d46 * d46 < 1.0D)
                                    {
                                        for (int i50 = i38 - 1; i50 >= i56; --i50)
                                        {
                                            double d51 = (i50 + 0.5D - posY) / d31;
                                            if (d51 > -0.7D && d59 * d59 + d51 * d51 + d46 * d46 < 1.0D)
                                            {
                                                byte b53 = chunkData[i48];
                                                if (b53 == worldObj.theme.Surface)
                                                {
                                                    z49 = true;
                                                }

                                                if (b53 == worldObj.theme.Cliff || b53 == worldObj.theme.Ground || b53 == worldObj.theme.Surface)
                                                {
                                                    if (i50 < 10)
                                                    {
                                                        chunkData[i48] = Block.Lava;
                                                    }
                                                    else
                                                    {
                                                        chunkData[i48] = 0;
                                                        if (z49 && chunkData[i48 - 256] == worldObj.theme.Ground)
                                                        {
                                                            chunkData[i48 - 256] = worldObj.theme.Surface;
                                                        }
                                                    }
                                                }
                                            }

                                            i48 -= 256;
                                        }
                                    }
                                }
                            }

                            if (z54)
                            {
                                break;
                            }
                        }
                    }
                }
            }

        }

        protected override void recursiveGenerate(GenWorld world, int chunkX, int chunkZ, int originalX, int originalZ, ref byte[] chunkData)
        {
            int i7 = rand.NextInt(rand.NextInt(rand.NextInt(40) + 1) + 1);
            if (rand.NextInt(15) != 0)
            {
                i7 = 0;
            }

            for (int i8 = 0; i8 < i7; ++i8)
            {
                double d9 = chunkX * 16 + rand.NextInt(16);
                double d11 = rand.NextInt(rand.NextInt(world.wHeight - 8) + 8);
                double d13 = chunkZ * 16 + rand.NextInt(16);
                int i15 = 1;
                if (rand.NextInt(4) == 0)
                {
                    generateLargeCaveNode(rand.NextLong(), originalX, originalZ, chunkData, d9, d11, d13);
                    i15 += rand.NextInt(4);
                }

                for (int i16 = 0; i16 < i15; ++i16)
                {
                    float f17 = rand.NextFloat() * (float)Math.PI * 2.0F;
                    float f18 = (rand.NextFloat() - 0.5F) * 2.0F / 8.0F;
                    float f19 = rand.NextFloat() * 2.0F + rand.NextFloat();
                    if (rand.NextInt(10) == 0)
                    {
                        f19 *= rand.NextFloat() * rand.NextFloat() * 3.0F + 1.0F;
                    }

                    generateCaveNode(rand.NextLong(), originalX, originalZ, chunkData, d9, d11, d13, f19, f17, f18, 0, 0, 1.0D);
                }
            }

        }
    }



    public abstract class WorldGenerator
    {
        public abstract bool generate(GenWorld world1, JavaRandom random2, int i3, int i4, int i5);

        public virtual void setScale(double d1, double d3, double d5)
        {
        }
    }

    public class WorldGenMinable : WorldGenerator
    {
        private int minableBlockId;
        private int numberOfBlocks;

        public WorldGenMinable(int i1, int i2)
        {
            minableBlockId = i1;
            numberOfBlocks = i2;
        }

        public override bool generate(GenWorld world1, JavaRandom random2, int i3, int i4, int i5)
        {
            float f6 = random2.NextFloat() * (float)Math.PI;
            double d7 = (double)(i3 + 8 + MathHelper.sin(f6) * numberOfBlocks / 8.0F);
            double d9 = (double)(i3 + 8 - MathHelper.sin(f6) * numberOfBlocks / 8.0F);
            double d11 = (double)(i5 + 8 + MathHelper.cos(f6) * numberOfBlocks / 8.0F);
            double d13 = (double)(i5 + 8 - MathHelper.cos(f6) * numberOfBlocks / 8.0F);
            double d15 = i4 + random2.NextInt(3) + 2;
            double d17 = i4 + random2.NextInt(3) + 2;

            for (int i19 = 0; i19 <= numberOfBlocks; ++i19)
            {
                double d20 = d7 + (d9 - d7) * i19 / numberOfBlocks;
                double d22 = d15 + (d17 - d15) * i19 / numberOfBlocks;
                double d24 = d11 + (d13 - d11) * i19 / numberOfBlocks;
                double d26 = random2.NextDouble() * numberOfBlocks / 16.0D;
                double d28 = (double)(MathHelper.sin(i19 * (float)Math.PI / numberOfBlocks) + 1.0F) * d26 + 1.0D;
                double d30 = (double)(MathHelper.sin(i19 * (float)Math.PI / numberOfBlocks) + 1.0F) * d26 + 1.0D;

                for (int i32 = (int)(d20 - d28 / 2.0D); i32 <= (int)(d20 + d28 / 2.0D); ++i32)
                {
                    for (int i33 = (int)(d22 - d30 / 2.0D); i33 <= (int)(d22 + d30 / 2.0D); ++i33)
                    {
                        for (int i34 = (int)(d24 - d28 / 2.0D); i34 <= (int)(d24 + d28 / 2.0D); ++i34)
                        {
                            double d35 = (i32 + 0.5D - d20) / (d28 / 2.0D);
                            double d37 = (i33 + 0.5D - d22) / (d30 / 2.0D);
                            double d39 = (i34 + 0.5D - d24) / (d28 / 2.0D);
                            if (d35 * d35 + d37 * d37 + d39 * d39 < 1.0D && world1.GetBlock(i32, i33, i34) == world1.theme.Cliff)
                            {
                                world1.SetBlock(i32, i33, i34, minableBlockId);
                            }
                        }
                    }
                }
            }

            return true;
        }
    }

    public class WorldGenFlowers : WorldGenerator
    {
        private int plantBlockId;

        public WorldGenFlowers(int i1)
        {
            plantBlockId = i1;
        }

        public override bool generate(GenWorld world1, JavaRandom random2, int i3, int i4, int i5)
        {
            for (int i6 = 0; i6 < 64; ++i6)
            {
                int i7 = i3 + random2.NextInt(8) - random2.NextInt(8);
                int i8 = i4 + random2.NextInt(4) - random2.NextInt(4);
                int i9 = i5 + random2.NextInt(8) - random2.NextInt(8);

                int block = world1.GetBlock(i7, i8 - 1, i9);
                if (world1.GetBlock(i7, i8, i9) == Block.Air && (block == world1.theme.Surface || block == world1.theme.Ground))
                {
                    world1.SetBlock(i7, i8, i9, plantBlockId);
                }
            }

            return true;
        }
    }

    public class WorldGenTrees : WorldGenerator
    {
        public override bool generate(GenWorld world1, JavaRandom random2, int i3, int i4, int i5)
        {
            int i6 = random2.NextInt(3) + 4;
            bool z7 = true;
            if (i4 >= 1 && i4 + i6 + 1 <= world1.wHeight)
            {
                for (int i8 = i4; i8 <= i4 + 1 + i6; ++i8)
                {
                    byte b9 = 1;
                    if (i8 == i4)
                    {
                        b9 = 0;
                    }

                    if (i8 >= i4 + 1 + i6 - 2)
                    {
                        b9 = 2;
                    }

                    for (int i10 = i3 - b9; i10 <= i3 + b9 && z7; ++i10)
                    {
                        for (int i11 = i5 - b9; i11 <= i5 + b9 && z7; ++i11)
                        {
                            if (i8 >= 0 && i8 < world1.wHeight)
                            {
                                int i12 = world1.GetBlock(i10, i8, i11);
                                if (i12 != Block.Air && i12 != Block.Leaves)
                                {
                                    z7 = false;
                                }
                            }
                            else
                            {
                                z7 = false;
                            }
                        }
                    }
                }

                if (!z7)
                {
                    return false;
                }
                else
                {
                    int i8 = world1.GetBlock(i3, i4 - 1, i5);
                    if ((i8 == world1.theme.Surface || i8 == world1.theme.Ground) && i4 < world1.wHeight - i6 - 1)
                    {
                        world1.SetBlock(i3, i4 - 1, i5, world1.theme.Ground);

                        int i16;
                        for (i16 = i4 - 3 + i6; i16 <= i4 + i6; ++i16)
                        {
                            int i10 = i16 - (i4 + i6);
                            int i11 = 1 - i10 / 2;

                            for (int i12 = i3 - i11; i12 <= i3 + i11; ++i12)
                            {
                                int i13 = i12 - i3;

                                for (int i14 = i5 - i11; i14 <= i5 + i11; ++i14)
                                {
                                    int i15 = i14 - i5;
                                    int id = world1.GetBlock(i12, i16, i14);
                                    if ((Math.Abs(i13) != i11 || Math.Abs(i15) != i11 || random2.NextInt(2) != 0 && i10 != 0) && world1.mcgLevel.LightPasses((ushort)id))
                                    {
                                        world1.SetBlock(i12, i16, i14, Block.Leaves);
                                    }
                                }
                            }
                        }

                        for (i16 = 0; i16 < i6; ++i16)
                        {
                            int i10 = world1.GetBlock(i3, i4 + i16, i5);
                            if (i10 == Block.Air || i10 == Block.Leaves)
                            {
                                world1.SetBlock(i3, i4 + i16, i5, Block.Log);
                            }
                        }

                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            else
            {
                return false;
            }
        }
    }

    public class WorldGenMCGTreeWrapper : WorldGenerator
    {
        private Tree tree;

        public WorldGenMCGTreeWrapper(Tree tree)
        {
            this.tree = tree;
        }

        public override bool generate(GenWorld world1, JavaRandom random2, int i3, int i4, int i5)
        {
            Random R = new Random();
            int treeHeight = tree.DefaultSize(R);

            if (world1.GetBlock(i3, i4 - 1, i5) == world1.theme.Surface && !TreeCheck(world1, i3, i4, i5, treeHeight))
            {
                tree.SetData(R, treeHeight);

                tree.Generate((ushort)i3, (ushort)i4, (ushort)i5, (xT, yT, zT, bT) =>
                    {
                        // don't place leafs over trunk
                        if (bT == Block.Leaves && world1.GetBlock(xT, yT, zT) == Block.Log) return;
                        world1.SetBlock(xT, yT, zT, bT);
                    });
            }

            return true;
        }

        //Copied from Tree.cs
        bool TreeCheck(GenWorld world, int x, int y, int z, int size)
        {
            for (int dy = -size; dy <= size; ++dy)
                for (int dz = -size; dz <= size; ++dz)
                    for (int dx = -size; dx <= size; ++dx)
                    {
                        int block = world.GetBlock((ushort)(x + dx), (ushort)(y + dy), (ushort)(z + dz));
                        if (block == Block.Log || block == Block.Green) return true;
                    }
            return false;
        }
    }

    public class NoiseGeneratorOctaves
    {
        private NoiseGeneratorPerlin[] generatorCollection;
        private int octaves;

        public NoiseGeneratorOctaves(JavaRandom random, int octaves)
        {
            this.octaves = octaves;
            generatorCollection = new NoiseGeneratorPerlin[octaves];

            for (int i3 = 0; i3 < octaves; ++i3)
            {
                generatorCollection[i3] = new NoiseGeneratorPerlin(random);
            }

        }

        public double generateNoise(double var1, double var5)
        {
            double var7 = 0.0D;
            double var9 = 1.0D;

            for (int var11 = 0; var11 < octaves; ++var11)
            {
                var7 += generatorCollection[var11].generateNoise(var1 * var9, var5 * var9) / var9;
                var9 /= 2.0D;
            }

            return var7;
        }

        public double generateNoise(double var1, double var3, double var5)
        {
            double var7 = 0.0D;
            double var9 = 1.0D;

            for (int var11 = 0; var11 < octaves; ++var11)
            {
                var7 += generatorCollection[var11].generateNoise(var1 * var9, var3 * var9, var5 * var9) / var9;
                var9 /= 2.0D;
            }

            return var7;
        }

        public double[] generateNoiseOctaves(double[] d1, double d2, double d4, double d6, int i8, int i9, int i10, double d11, double d13, double d15)
        {
            if (d1 == null)
            {
                d1 = new double[i8 * i9 * i10];
            }
            else
            {
                for (int i17 = 0; i17 < d1.Length; ++i17)
                {
                    d1[i17] = 0.0D;
                }
            }

            double d20 = 1.0D;

            for (int i19 = 0; i19 < octaves; ++i19)
            {
                generatorCollection[i19].populateNoiseArray(d1, d2, d4, d6, i8, i9, i10, d11 * d20, d13 * d20, d15 * d20, d20);
                d20 /= 2.0D;
            }

            return d1;
        }
    }

    public class NoiseGeneratorPerlin
    {
        private int[] permutations;
        public double xCoord;
        public double yCoord;
        public double zCoord;


        public NoiseGeneratorPerlin(JavaRandom random)
        {
            permutations = new int[512];
            xCoord = random.NextDouble() * 256.0D;
            yCoord = random.NextDouble() * 256.0D;
            zCoord = random.NextDouble() * 256.0D;

            int i2;
            for (i2 = 0; i2 < 256; permutations[i2] = i2++)
            {
            }

            for (i2 = 0; i2 < 256; ++i2)
            {
                int i3 = random.NextInt(256 - i2) + i2;
                int i4 = permutations[i2];
                permutations[i2] = permutations[i3];
                permutations[i3] = i4;
                permutations[i2 + 256] = permutations[i2];
            }

        }

        public double generateNoise(double var1, double var3)
        {
            return generateNoise(var1, var3, 0.0D);
        }

        public double generateNoise(double d1, double d3, double d5)
        {
            double d7 = d1 + xCoord;
            double d9 = d3 + yCoord;
            double d11 = d5 + zCoord;
            int i13 = (int)d7;
            int i14 = (int)d9;
            int i15 = (int)d11;
            if (d7 < i13)
            {
                --i13;
            }

            if (d9 < i14)
            {
                --i14;
            }

            if (d11 < i15)
            {
                --i15;
            }

            int i16 = i13 & 255;
            int i17 = i14 & 255;
            int i18 = i15 & 255;
            d7 -= i13;
            d9 -= i14;
            d11 -= i15;
            double d19 = d7 * d7 * d7 * (d7 * (d7 * 6.0D - 15.0D) + 10.0D);
            double d21 = d9 * d9 * d9 * (d9 * (d9 * 6.0D - 15.0D) + 10.0D);
            double d23 = d11 * d11 * d11 * (d11 * (d11 * 6.0D - 15.0D) + 10.0D);
            int i25 = permutations[i16] + i17;
            int i26 = permutations[i25] + i18;
            int i27 = permutations[i25 + 1] + i18;
            int i28 = permutations[i16 + 1] + i17;
            int i29 = permutations[i28] + i18;
            int i30 = permutations[i28 + 1] + i18;
            return lerp(d23, lerp(d21, lerp(d19, grad(permutations[i26], d7, d9, d11), grad(permutations[i29], d7 - 1.0D, d9, d11)), lerp(d19, grad(permutations[i27], d7, d9 - 1.0D, d11), grad(permutations[i30], d7 - 1.0D, d9 - 1.0D, d11))), lerp(d21, lerp(d19, grad(permutations[i26 + 1], d7, d9, d11 - 1.0D), grad(permutations[i29 + 1], d7 - 1.0D, d9, d11 - 1.0D)), lerp(d19, grad(permutations[i27 + 1], d7, d9 - 1.0D, d11 - 1.0D), grad(permutations[i30 + 1], d7 - 1.0D, d9 - 1.0D, d11 - 1.0D))));
        }

        public double lerp(double d1, double d3, double d5)
        {
            return d3 + d1 * (d5 - d3);
        }

        public double grad(int i1, double d2, double d4, double d6)
        {
            int i8 = i1 & 15;
            double d9 = i8 < 8 ? d2 : d4;
            double d11 = i8 < 4 ? d4 : (i8 != 12 && i8 != 14 ? d6 : d2);
            return ((i8 & 1) == 0 ? d9 : -d9) + ((i8 & 2) == 0 ? d11 : -d11);
        }

        public void populateNoiseArray(double[] ad, double d, double d1, double d2, int i, int j, int k, double d3, double d4, double d5, double d6)
        {
            int i1 = 0;
            double d7 = 1.0 / d6;
            int i2 = -1;
            double d13 = 0.0;
            double d15 = 0.0;
            double d16 = 0.0;
            double d18 = 0.0;
            for (int i5 = 0; i5 < i; ++i5)
            {
                double d20 = (d + i5) * d3 + xCoord;
                int k5 = (int)d20;
                if (d20 < k5)
                {
                    --k5;
                }
                int i6 = k5 & 0xFF;
                double d22 = (d20 -= k5) * d20 * d20 * (d20 * (d20 * 6.0 - 15.0) + 10.0);
                for (int j6 = 0; j6 < k; ++j6)
                {
                    double d24 = (d2 + j6) * d5 + zCoord;
                    int k6 = (int)d24;
                    if (d24 < k6)
                    {
                        --k6;
                    }
                    int l6 = (int)(k6 & 0xFFL);
                    double d25 = (d24 -= k6) * d24 * d24 * (d24 * (d24 * 6.0 - 15.0) + 10.0);
                    for (int i7 = 0; i7 < j; ++i7)
                    {
                        double d26 = (d1 + i7) * d4 + yCoord;
                        int j7 = (int)d26;
                        if (d26 < j7)
                        {
                            --j7;
                        }
                        int k7 = j7 & 0xFF;
                        double d27 = (d26 -= j7) * d26 * d26 * (d26 * (d26 * 6.0 - 15.0) + 10.0);
                        if (i7 == 0 || k7 != i2)
                        {
                            i2 = k7;
                            int j2 = permutations[i6] + k7;
                            int k2 = permutations[j2] + l6;
                            int l2 = permutations[j2 + 1] + l6;
                            int i3 = permutations[i6 + 1] + k7;
                            int k3 = permutations[i3] + l6;
                            int l3 = permutations[i3 + 1] + l6;
                            d13 = lerp(d22, grad(permutations[k2], d20, d26, d24), grad(permutations[k3], d20 - 1.0, d26, d24));
                            d15 = lerp(d22, grad(permutations[l2], d20, d26 - 1.0, d24), grad(permutations[l3], d20 - 1.0, d26 - 1.0, d24));
                            d16 = lerp(d22, grad(permutations[k2 + 1], d20, d26, d24 - 1.0), grad(permutations[k3 + 1], d20 - 1.0, d26, d24 - 1.0));
                            d18 = lerp(d22, grad(permutations[l2 + 1], d20, d26 - 1.0, d24 - 1.0), grad(permutations[l3 + 1], d20 - 1.0, d26 - 1.0, d24 - 1.0));
                        }
                        double d28 = lerp(d27, d13, d15);
                        double d29 = lerp(d27, d16, d18);
                        double d30 = lerp(d25, d28, d29);
                        int n = i1++;
                        ad[n] = ad[n] + d30 * d7;
                    }
                }
            }
        }
    }

    public class GenChunk
    {
        public GenWorld world;
        public byte[] blocks;
        public int[] heightMap;
        public int x, z;
        public bool isTerrainPopulated = false;

        public GenChunk(GenWorld world, int x, int z, byte[] blocks)
        {
            this.world = world;
            this.x = x;
            this.z = z;

            this.blocks = blocks;
            heightMap = new int[256];

            if (blocks != null) GenerateHeightMap();
        }

        public void SetBlock(int x, int y, int z, int block)
        {
            blocks[y << 8 | z << 4 | x] = (byte)block;
        }

        public int GetBlock(int x, int y, int z)
        {
            return blocks[y << 8 | z << 4 | x];
        }

        public void GenerateHeightMap()
        {
            int lowest = world.wHeight;

            for (int z = 0; z < 16; ++z)
            {
                for (int x = 0; x < 16; ++x)
                {
                    int y = world.wHeight - 1;

                    int idx = z << 4 | x;
                    int id;
                    do
                    {
                        id = blocks[(y - 1) << 8 | idx];
                        y--;
                    }
                    while (y > 0 && world.mcgLevel.LightPasses((ushort)id));

                    heightMap[idx] = y + 1; //FIXME: this is probably not how it should be.
                    if (y < lowest)
                    {
                        lowest = y;
                    }
                }
            }

            // this.height = i1;
            // this.isModified = true;
        }

        public int GetHeightValue(int x, int z)
        {
            return heightMap[z << 4 | x];
        }
    }

    public class GenWorld
    {
        public int xChTotal, zChTotal, wHeight;
        public MapGenBiome theme;
        public GenChunk[] chunks;
        public MapGenArgsHack.GenArgs genArgs;
        public Level mcgLevel;
        public ChunkBasedOctaveGenerator chunkGenerator { private get; set; }

        public GenWorld(int xChTotal, int zChTotal, int wHeight, MapGenBiome theme, MapGenArgsHack.GenArgs genArgs, Level mcgLevel)
        {
            this.xChTotal = xChTotal;
            this.zChTotal = zChTotal;
            this.wHeight = wHeight;
            this.theme = theme;
            this.genArgs = genArgs;
            this.mcgLevel = mcgLevel;

            chunks = new GenChunk[xChTotal * zChTotal];
        }

        public void PopulateChunk(int x, int z)
        {
            if (!chunks[(x * zChTotal) + z].isTerrainPopulated && ChunkExists(x + 1, z + 1) && ChunkExists(x, z + 1) && ChunkExists(x + 1, z))
            {
                chunkGenerator.Populate(x, z);
            }

            if (ChunkExists(x - 1, z) && !GetChunkFromChunkCoords(x - 1, z).isTerrainPopulated && ChunkExists(x - 1, z + 1) && ChunkExists(x, z + 1) && ChunkExists(x - 1, z))
            {
                chunkGenerator.Populate(x - 1, z);
            }

            if (ChunkExists(x, z - 1) && !GetChunkFromChunkCoords(x, z - 1).isTerrainPopulated && ChunkExists(x + 1, z - 1) && ChunkExists(x, z - 1) && ChunkExists(x + 1, z))
            {
                chunkGenerator.Populate(x, z - 1);
            }

            if (ChunkExists(x - 1, z - 1) && !GetChunkFromBlockCoords(x - 1, z - 1).isTerrainPopulated && ChunkExists(x - 1, z - 1) && ChunkExists(x, z - 1) && ChunkExists(x - 1, z))
            {
                chunkGenerator.Populate(x - 1, z - 1);
            }
        }

        public bool ChunkExists(int x, int z)
        {
            return x < xChTotal && z < zChTotal && x > 0 && z > 0 && chunks[(x * zChTotal) + z] != null;
        }

        public GenChunk GetChunkFromChunkCoords(int x, int z)
        {
            if (x >= xChTotal || z >= zChTotal || x < 0 || z < 0)
            {
                return new GenChunk(this, x, z, new byte[256 * wHeight]);
            }
            else
            {
                if (chunks[(x * zChTotal) + z] == null)
                {
                    chunks[(x * zChTotal) + z] = chunkGenerator.generateChunk(x, z);
                    PopulateChunk(x, z);
                }
                return chunks[(x * zChTotal) + z];
            }
        }

        public GenChunk GetChunkFromBlockCoords(int x, int z)
        {
            int cX = x >> 4;
            int cZ = z >> 4;

            if (cX >= xChTotal || cZ >= zChTotal || cX < 0 || cZ < 0)
            {
                return new GenChunk(this, cX, cZ, new byte[256 * wHeight]);
            }
            else
            {
                if (chunks[(cX * zChTotal) + cZ] == null)
                {
                    chunks[(cX * zChTotal) + cZ] = chunkGenerator.generateChunk(cX, cZ);
                    PopulateChunk(cX, cZ);
                }
                return chunks[(cX * zChTotal) + cZ];
            }
        }

        public void SetBlock(int x, int y, int z, int block)
        {
            GenChunk chunk = GetChunkFromBlockCoords(x, z);

            if (chunk == null || y >= wHeight || y < 0) return;
            chunk.SetBlock(x & 15, y, z & 15, block);
        }

        public int GetBlock(int x, int y, int z)
        {
            GenChunk chunk = GetChunkFromBlockCoords(x, z);

            if (chunk == null || y >= wHeight || y < 0) return 0;
            return chunk.GetBlock(x & 15, y, z & 15);
        }

        public int GetHeightValue(int x, int z)
        {
            GenChunk chunk = GetChunkFromBlockCoords(x, z);

            if (chunk == null) return 0;
            return chunk.GetHeightValue(x & 15, z & 15);
        }
    }

    public static class Util
    {
        public static int JavaStringHashCode(this string str)
        {
            int h = 0;
            for (int i = 0; i < str.Length; i++)
            {
                h = 31 * h + str[i];
            }
            return h;
        }
    }

    //implementation of java's random class. we have to implement our own since 
    //mcgalaxy's JavaRandom does not support producing 64bit values and taking in 64bit seeds
    public sealed class JavaRandom
    {
        private long seed;
        private long value = 0x5DEECE66DL;
        private long mask = (1L << 48) - 1;

        public JavaRandom()
        {
            SetSeed(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }

        public JavaRandom(long seed)
        {
            SetSeed(seed);
        }

        public void SetSeed(long seed)
        {
            this.seed = (seed ^ value) & mask;
        }

        public int Next(int bits)
        {
            seed = (seed * value + 0xBL) & mask;
            return (int)(long)(((ulong)seed) >> (48 - bits));
        }

        public int NextInt()
        {
            return Next(32);
        }

        public int NextInt(int n)
        {
            if (n <= 0)
                throw new ArgumentOutOfRangeException("n must be positive");

            if ((n & -n) == n) // i.e., n is a power of 2
                return (int)((n * (long)Next(31)) >> 31);

            int bits, val;
            do
            {
                bits = Next(31);
                val = bits % n;
            }
            while (bits - val + (n - 1) < 0);
            return val;
        }

        public long NextLong()
        {
            return ((long)Next(32) << 32) + Next(32);
        }

        public double NextDouble()
        {
            return (((long)Next(26) << 27) + Next(27))
                / (double)(1L << 53);
        }

        public float NextFloat()
        {
            return Next(24) / ((float)(1 << 24));
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct FloatIntConverter
    {
        [FieldOffset(0)]
        public int IntValue;
        [FieldOffset(0)]
        public float FloatValue;
    }

    public static class MathHelper
    {
        private static int[] SINE_TABLE_INT = new int[16384 + 1];
        private static float SINE_TABLE_MIDPOINT;

        static MathHelper()
        {
            // Copy the sine table, covering to raw int bits
            FloatIntConverter converter = new FloatIntConverter();
            for (int i = 0; i < SINE_TABLE_INT.Length; i++)
            {
                converter.FloatValue = (float)Math.Sin(i * Math.PI * 2.0D / 65536.0D);
                SINE_TABLE_INT[i] = converter.IntValue;
            }

            SINE_TABLE_MIDPOINT = 1.2246469E-16F;
        }

        public static float sin(float f0)
        {
            return lookup((int)(f0 * 10430.378f) & 0xFFFF);
        }

        public static float cos(float f0)
        {
            return lookup((int)(f0 * 10430.378f + 16384.0f) & 0xFFFF);
        }

        private static float lookup(int index)
        {
            // A special case... Is there some way to eliminate this?
            if (index == 32768)
            {
                return SINE_TABLE_MIDPOINT;
            }

            // Trigonometric identity: sin(-x) = -sin(x)
            // Given a domain of 0 <= x <= 2*pi, just negate the value if x > pi.
            // This allows the sin table size to be halved.
            int neg = (index & 0x8000) << 16;

            // All bits set if (pi/2 <= x), none set otherwise
            // Extracts the 15th bit from 'half'
            int mask = (index << 17) >> 31;

            // Trigonometric identity: sin(x) = sin(pi/2 - x)
            int pos = (0x8001 & mask) + (index ^ mask);

            // ap the position in the table. Moving this down to immediately before the array access
            // seems to help the Hotspot compiler optimize the bit math better.
            pos &= 0x7fff;

            // Fetch the corresponding value from the LUT and invert the sign bit as needed
            // This directly manipulate the sign bit on the float bits to simplify logic
            FloatIntConverter converter = new FloatIntConverter();
            converter.IntValue = SINE_TABLE_INT[pos] ^ neg;

            return converter.FloatValue;
        }

        public static int floor(double num)
        {
            return (int)Math.Floor(num);
        }

        public static double clampedLerp(double lowerBnd, double upperBnd, double slide) {
            if (slide < 0.0D) 
            {
                return lowerBnd;
            } 
            else 
            {
                return slide > 1.0D ? upperBnd : lerp(slide, lowerBnd, upperBnd);
            }
        }

        public static double lerp(double pct, double start, double end) 
        {
            return start + pct * (end - start);
        }
    }
}

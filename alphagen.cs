using System;
using System.Runtime.InteropServices;
using MCGalaxy.Generator;
using MCGalaxy.Generator.Foliage;

namespace MCGalaxy
{
    public sealed class PluginAlphaGen : Plugin
    {
        public override string name { get { return "PluginAlphaGen"; } }
        public override string MCGalaxy_Version { get { return "1.9.4.7"; } }
        public override string creator { get { return ""; } }


        public override void Load(bool startup)
        {
            MapGen.Register("alphaGen", GenType.Advanced, AlphaGen.Gen, "hello?");
        }
        public override void Unload(bool shutdown)
        {

        }
    }

    public static class AlphaGen
    {
        public static bool Gen(Player p, Level lvl, MapGenArgs mgArgs)
        {
            MapGenBiomeName theme = MapGenBiomeName.Forest;

            if (!mgArgs.ParseArgs(p)) return false;
            theme = mgArgs.Biome;
            int rng_seed = mgArgs.Seed;

            MapGenBiome theme2 = MapGenBiome.Get(theme);
            theme2.ApplyEnv(lvl.Config);

            int width = (int)Math.Ceiling(lvl.Width / 16.0D);
            int length = (int)Math.Ceiling(lvl.Length / 16.0D);

            GenWorld world = new GenWorld(width, length, lvl.Height, theme2, rng_seed, lvl);

            ChunkBasedOctaveGenerator generator = new ChunkBasedOctaveGenerator(world, rng_seed);
            world.chunkGenerator = generator;

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
            this.worldObj = world;
            this.rand = new JavaRandom(seed);
            this.minLimitNoise = new NoiseGeneratorOctaves(this.rand, 16);
            this.maxLimitNoise = new NoiseGeneratorOctaves(this.rand, 16);
            this.mainNoise = new NoiseGeneratorOctaves(this.rand, 8);
            this.beachNoise = new NoiseGeneratorOctaves(this.rand, 4);
            this.surfaceHeightNoise = new NoiseGeneratorOctaves(this.rand, 4);
            this.scaleNoise = new NoiseGeneratorOctaves(this.rand, 10);
            this.depthNoise = new NoiseGeneratorOctaves(this.rand, 16);
            this.treeDensityNoise = new NoiseGeneratorOctaves(this.rand, 8);
        }

        private void generateTerrain(int chunkX, int chunkZ, ref byte[] blocks)
        {
            byte b4 = 4;
            int i5 = worldObj.wHeight / 8;
            int b5 = worldObj.wHeight / 2;
            int i6 = b4 + 1;
            int b7 = (worldObj.wHeight / 8) + 1;
            int i8 = b4 + 1;
            this.noiseArray = this.initializeNoiseField(this.noiseArray, chunkX * b4, 0, chunkZ * b4, i6, b7, i8);

            for (int blobX = 0; blobX < b4; ++blobX)
            {
                for (int blobZ = 0; blobZ < b4; ++blobZ)
                {
                    for (int blobY = 0; blobY < i5; ++blobY)
                    {
                        double d12 = 0.125D;
                        double d14 = this.noiseArray[((blobX + 0) * i8 + blobZ + 0) * b7 + blobY + 0];
                        double d16 = this.noiseArray[((blobX + 0) * i8 + blobZ + 1) * b7 + blobY + 0];
                        double d18 = this.noiseArray[((blobX + 1) * i8 + blobZ + 0) * b7 + blobY + 0];
                        double d20 = this.noiseArray[((blobX + 1) * i8 + blobZ + 1) * b7 + blobY + 0];
                        double d22 = (this.noiseArray[((blobX + 0) * i8 + blobZ + 0) * b7 + blobY + 1] - d14) * d12;
                        double d24 = (this.noiseArray[((blobX + 0) * i8 + blobZ + 1) * b7 + blobY + 1] - d16) * d12;
                        double d26 = (this.noiseArray[((blobX + 1) * i8 + blobZ + 0) * b7 + blobY + 1] - d18) * d12;
                        double d28 = (this.noiseArray[((blobX + 1) * i8 + blobZ + 1) * b7 + blobY + 1] - d20) * d12;

                        for (int blobPosY = 0; blobPosY < 8; ++blobPosY)
                        {
                            double d31 = 0.25D;
                            double d33 = d14;
                            double d35 = d16;
                            double d37 = (d18 - d14) * d31;
                            double d39 = (d20 - d16) * d31;

                            for (int blobPosX = 0; blobPosX < 4; ++blobPosX)
                            {
                                double d44 = 0.25D;
                                double d46 = d33;
                                double d48 = (d35 - d33) * d44;

                                for (int blobPosZ = 0; blobPosZ < 4; ++blobPosZ)
                                {
                                    int blockX = blobX << 2 | blobPosX;
                                    int blockY = blobY << 3 | blobPosY;
                                    int blockZ = blobZ << 2 | blobPosZ;

                                    int index = blockY << 8 | blockZ << 4 | blockX;

                                    int i51 = 0;
                                    if (blockY < b5)
                                    {
                                        i51 = worldObj.theme.Water;
                                    }

                                    if (d46 > 0.0D)
                                    {
                                        i51 = worldObj.theme.Cliff;
                                    }

                                    blocks[index] = (byte)i51;
                                    d46 += d48;
                                }

                                d33 += d37;
                                d35 += d39;
                            }

                            d14 += d22;
                            d16 += d24;
                            d18 += d26;
                            d20 += d28;
                        }
                    }
                }
            }

        }

        private void replaceSurfaceBlocks(int chunkX, int chunkZ, ref byte[] blocks)
        {
            int seaLevel = worldObj.wHeight / 2;
            double d5 = 8.0D / 256D;

            for (int x = 0; x < 16; ++x)
            {
                for (int z = 0; z < 16; ++z)
                {
                    double dd2 = (chunkX << 4) + x;
                    double dd4 = (chunkZ << 4) + z;
                    bool generateSandBeach = beachNoise.generateNoise(dd2 * d5, dd4 * d5, 0.0D) + rand.NextDouble() * 0.2D > 0D; //sand
                    bool generateGravelBeach = beachNoise.generateNoise(dd4 * d5, d5, dd2 * d5) + rand.NextDouble() * 0.2D > 3D; //gravel
                    int heightLevel = (int)(surfaceHeightNoise.generateNoise(dd2 * d5 * 2D, dd4 * d5 * 2D) / 3D + 3D + rand.NextDouble() * 0.25D);
                    int height = -1;
                    byte surface = worldObj.theme.Surface;
                    byte ground = worldObj.theme.Ground;
                    byte cliff = worldObj.theme.Cliff;
                    byte water = worldObj.theme.Water;
                    if (water == 0) seaLevel = 0;

                    for (int y = this.worldObj.wHeight - 1; y >= 0; --y)
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

            this.rand.SetSeed((long)i1 * 341873128712L + (long)i2 * 132897987541L);

            this.generateTerrain(i1, i2, ref chunkBlocks);
            this.replaceSurfaceBlocks(i1, i2, ref chunkBlocks);
            this.caveGenerator.generate(this.worldObj, i1, i2, ref chunkBlocks);

            // for (int x = 0; x < 16; x++)
            // {
            // 	for (int z = 0; z < 16; z++)
            // 	{
            // 		chunkBlocks[z << 4 | x] = (byte)(((rand.NextInt()) & 15) + 21);
            // 	}
            // }

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
            this.scaleNoiseSample = this.scaleNoise.generateNoiseOctaves(this.scaleNoiseSample, (double)xStart, (double)yStart, (double)zStart, xSamples, 1, zSamples, 1.0D, 0.0D, 1.0D);
            this.depthNoiseSample = this.depthNoise.generateNoiseOctaves(this.depthNoiseSample, (double)xStart, (double)yStart, (double)zStart, xSamples, 1, zSamples, 100.0D, 0.0D, 100.0D);
            this.mainNoiseSample = this.mainNoise.generateNoiseOctaves(this.mainNoiseSample, (double)xStart, (double)yStart, (double)zStart, xSamples, ySamples, zSamples, horCoordScale / 80.0D, vertCoordScale / 160.0D, horCoordScale / 80.0D);
            this.minLimitNoiseSample = this.minLimitNoise.generateNoiseOctaves(this.minLimitNoiseSample, (double)xStart, (double)yStart, (double)zStart, xSamples, ySamples, zSamples, horCoordScale, vertCoordScale, horCoordScale);
            this.maxLimitNoiseSample = this.maxLimitNoise.generateNoiseOctaves(this.maxLimitNoiseSample, (double)xStart, (double)yStart, (double)zStart, xSamples, ySamples, zSamples, horCoordScale, vertCoordScale, horCoordScale);
            int index = 0;
            int scaleDepthIndex = 0;

            for (int x = 0; x < xSamples; ++x)
            {
                for (int z = 0; z < zSamples; ++z)
                {
                    double scale = (this.scaleNoiseSample[scaleDepthIndex] + 256.0D) / 512.0D;
                    if (scale > 1.0D)
                    {
                        scale = 1.0D;
                    }

                    double d18 = 0.0D;
                    double depth = this.depthNoiseSample[scaleDepthIndex] / 8000.0D;
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
                    depth = depth * (double)ySamples / 16.0D;
                    double d22 = (double)ySamples / 2.0D + depth * 4.0D;
                    ++scaleDepthIndex;

                    for (int y = 0; y < ySamples; ++y)
                    {
                        double density = 0.0D;
                        double offset = ((double)y - d22) * 12.0D / scale;
                        if (offset < 0.0D)
                        {
                            offset *= 4.0D;
                        }

                        double min = this.minLimitNoiseSample[index] / 512.0D;
                        double max = this.maxLimitNoiseSample[index] / 512.0D;
                        double main = (this.mainNoiseSample[index] / 10.0D + 1.0D) / 2.0D;
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
                        double d35;
                        if (y > ySamples - 4)
                        {
                            d35 = (y - (ySamples - 4)) / 3.0D;
                            density = density * (1.0D - d35) + -10.0D * d35;
                        }

                        if ((double)y < d18)
                        {
                            d35 = (d18 - (double)y) / 4.0D;
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

        public void Populate(int chunkX, int chunkZ)
        {
            GenChunk chunk = worldObj.chunks[(chunkX * worldObj.zChTotal) + chunkZ];
            if (chunk.isTerrainPopulated) return;

            chunk.isTerrainPopulated = true;

            int i4 = chunkX * 16;
            int i5 = chunkZ * 16;
            this.rand.SetSeed(this.worldObj.seed);
            long j6 = this.rand.NextLong() / 2L * 2L + 1L;
            long j8 = this.rand.NextLong() / 2L * 2L + 1L;
            this.rand.SetSeed((long)chunkX * j6 + (long)chunkZ * j8 ^ this.worldObj.seed);
            double d10 = 0.25D;

            int i12;
            int i13;
            int i14;
            int i15;

            for (i12 = 0; i12 < 20; ++i12)
            {
                i13 = i4 + this.rand.NextInt(16);
                i14 = this.rand.NextInt(worldObj.wHeight);
                i15 = i5 + this.rand.NextInt(16);
                (new WorldGenMinable(Block.Dirt, 32)).generate(this.worldObj, this.rand, i13, i14, i15);
            }

            for (i12 = 0; i12 < 10; ++i12)
            {
                i13 = i4 + this.rand.NextInt(16);
                i14 = this.rand.NextInt(worldObj.wHeight);
                i15 = i5 + this.rand.NextInt(16);
                (new WorldGenMinable(Block.Gravel, 32)).generate(this.worldObj, this.rand, i13, i14, i15);
            }

            for (i12 = 0; i12 < 20; ++i12)
            {
                i13 = i4 + this.rand.NextInt(16);
                i14 = this.rand.NextInt(worldObj.wHeight);
                i15 = i5 + this.rand.NextInt(16);
                (new WorldGenMinable(Block.CoalOre, 16)).generate(this.worldObj, this.rand, i13, i14, i15);
            }

            for (i12 = 0; i12 < 20; ++i12)
            {
                i13 = i4 + this.rand.NextInt(16);
                i14 = this.rand.NextInt(64);
                i15 = i5 + this.rand.NextInt(16);
                (new WorldGenMinable(Block.CoalOre, 8)).generate(this.worldObj, this.rand, i13, i14, i15);
            }

            for (i12 = 0; i12 < 2; ++i12)
            {
                i13 = i4 + this.rand.NextInt(16);
                i14 = this.rand.NextInt(32);
                i15 = i5 + this.rand.NextInt(16);
                (new WorldGenMinable(Block.GoldOre, 8)).generate(this.worldObj, this.rand, i13, i14, i15);
            }

            d10 = 0.5D;
            i12 = (int)((this.treeDensityNoise.generateNoise((double)i4 * d10, (double)i5 * d10) / 8.0D + this.rand.NextDouble() * 4.0D + 4.0D) / 3.0D);
            if (i12 < 0)
            {
                i12 = 0;
            }

            if (this.rand.NextInt(10) == 0)
            {
                ++i12;
            }

            //TODO: implement thing to implement world theme trees within WorldGenerator
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
                    i15 = i4 + this.rand.NextInt(16) + 8;
                    i16 = i5 + this.rand.NextInt(16) + 8;
                    object18.setScale(1.0D, 1.0D, 1.0D);
                    object18.generate(this.worldObj, this.rand, i15, this.worldObj.GetHeightValue(i15, i16), i16);
                }
            }

            int i17;
            for (i14 = 0; i14 < 2; ++i14)
            {
                i15 = i4 + this.rand.NextInt(16) + 8;
                i16 = this.rand.NextInt(worldObj.wHeight);
                i17 = i5 + this.rand.NextInt(16) + 8;
                (new WorldGenFlowers(Block.Dandelion)).generate(this.worldObj, this.rand, i15, i16, i17);
            }

            if (this.rand.NextInt(2) == 0)
            {
                i14 = i4 + this.rand.NextInt(16) + 8;
                i15 = this.rand.NextInt(worldObj.wHeight);
                i16 = i5 + this.rand.NextInt(16) + 8;
                (new WorldGenFlowers(Block.Rose)).generate(this.worldObj, this.rand, i14, i15, i16);
            }

            if (this.rand.NextInt(4) == 0)
            {
                i14 = i4 + this.rand.NextInt(16) + 8;
                i15 = this.rand.NextInt(worldObj.wHeight);
                i16 = i5 + this.rand.NextInt(16) + 8;
                (new WorldGenFlowers(Block.Mushroom)).generate(this.worldObj, this.rand, i14, i15, i16);
            }

            if (this.rand.NextInt(8) == 0)
            {
                i14 = i4 + this.rand.NextInt(16) + 8;
                i15 = this.rand.NextInt(worldObj.wHeight);
                i16 = i5 + this.rand.NextInt(16) + 8;
                (new WorldGenFlowers(Block.RedMushroom)).generate(this.worldObj, this.rand, i14, i15, i16);
            }
        }
    }

    public class MapGenBase
    {
        protected int range = 8;
        protected JavaRandom rand = new JavaRandom();
        protected GenWorld? worldObj;

        public void generate(GenWorld world, int chunkX, int chunkZ, ref byte[] chunkData)
        {
            int i6 = this.range;
            this.worldObj = world;
            this.rand.SetSeed(world.seed);
            long j7 = this.rand.NextLong();
            long j9 = this.rand.NextLong();

            for (int i11 = chunkX - i6; i11 <= chunkX + i6; ++i11)
            {
                for (int i12 = chunkZ - i6; i12 <= chunkZ + i6; ++i12)
                {
                    long j13 = (long)i11 * j7;
                    long j15 = (long)i12 * j9;
                    this.rand.SetSeed(j13 ^ j15 ^ world.seed);
                    this.recursiveGenerate(world, i11, i12, chunkX, chunkZ, ref chunkData);
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
            this.generateCaveNode(randomSeed, originalX, originalZ, chunkData, posX, posY, posZ, 1.0F + this.rand.NextFloat() * 6.0F, 0.0F, 0.0F, -1, -1, 0.5D);
        }

        protected void generateCaveNode(long randomSeed, int originalX, int originalZ, byte[] chunkData, double posX, double posY, double posZ, float f12, float f13, float f14, int i15, int i16, double d17)
        {
            double d19 = (double)(originalX * 16 + 8);
            double d21 = (double)(originalZ * 16 + 8);
            float f23 = 0.0F;
            float f24 = 0.0F;
            JavaRandom random25 = new JavaRandom(randomSeed);
            if (i16 <= 0)
            {
                int i26 = this.range * 16 - 16;
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
                double d29 = 1.5D + (double)(MathHelper.sin((float)i15 * (float)Math.PI / (float)i16) * f12 * 1.0F);
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
                    this.generateCaveNode(random25.NextLong(), originalX, originalZ, chunkData, posX, posY, posZ, random25.NextFloat() * 0.5F + 0.5F, f13 - (float)Math.PI / 2F, f14 / 3.0F, i15, i16, 1.0D);
                    this.generateCaveNode(random25.NextLong(), originalX, originalZ, chunkData, posX, posY, posZ, random25.NextFloat() * 0.5F + 0.5F, f13 + (float)Math.PI / 2F, f14 / 3.0F, i15, i16, 1.0D);
                    return;
                }

                if (z54 || random25.NextInt(4) != 0)
                {
                    double d35 = posX - d19;
                    double d37 = posZ - d21;
                    double d39 = (double)(i16 - i15);
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
                                double d59 = ((double)(x + originalX * 16) + 0.5D - posX) / d29;

                                for (i45 = i57; i45 < i40; ++i45)
                                {
                                    double d46 = ((double)(i45 + originalZ * 16) + 0.5D - posZ) / d29;
                                    int i48 = i38 << 8 | i45 << 4 | x;
                                    bool z49 = false;
                                    if (d59 * d59 + d46 * d46 < 1.0D)
                                    {
                                        for (int i50 = i38 - 1; i50 >= i56; --i50)
                                        {
                                            double d51 = ((double)i50 + 0.5D - posY) / d31;
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
                                                        chunkData[i48] = (byte)Block.Lava;
                                                    }
                                                    else
                                                    {
                                                        chunkData[i48] = 0;
                                                        if (z49 && chunkData[i48 - 256] == worldObj.theme.Ground)
                                                        {
                                                            chunkData[i48 - 256] = (byte)worldObj.theme.Surface;
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
            int i7 = this.rand.NextInt(this.rand.NextInt(this.rand.NextInt(40) + 1) + 1);
            if (this.rand.NextInt(15) != 0)
            {
                i7 = 0;
            }

            for (int i8 = 0; i8 < i7; ++i8)
            {
                double d9 = (double)(chunkX * 16 + this.rand.NextInt(16));
                double d11 = (double)this.rand.NextInt(this.rand.NextInt(world.wHeight - 8) + 8);
                double d13 = (double)(chunkZ * 16 + this.rand.NextInt(16));
                int i15 = 1;
                if (this.rand.NextInt(4) == 0)
                {
                    this.generateLargeCaveNode(this.rand.NextLong(), originalX, originalZ, chunkData, d9, d11, d13);
                    i15 += this.rand.NextInt(4);
                }

                for (int i16 = 0; i16 < i15; ++i16)
                {
                    float f17 = this.rand.NextFloat() * (float)Math.PI * 2.0F;
                    float f18 = (this.rand.NextFloat() - 0.5F) * 2.0F / 8.0F;
                    float f19 = this.rand.NextFloat() * 2.0F + this.rand.NextFloat();
                    if (this.rand.NextInt(10) == 0)
                    {
                        f19 *= this.rand.NextFloat() * this.rand.NextFloat() * 3.0F + 1.0F;
                    }

                    this.generateCaveNode(this.rand.NextLong(), originalX, originalZ, chunkData, d9, d11, d13, f19, f17, f18, 0, 0, 1.0D);
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
            this.minableBlockId = i1;
            this.numberOfBlocks = i2;
        }

        public override bool generate(GenWorld world1, JavaRandom random2, int i3, int i4, int i5)
        {
            float f6 = random2.NextFloat() * (float)Math.PI;
            double d7 = (double)((float)(i3 + 8) + MathHelper.sin(f6) * (float)this.numberOfBlocks / 8.0F);
            double d9 = (double)((float)(i3 + 8) - MathHelper.sin(f6) * (float)this.numberOfBlocks / 8.0F);
            double d11 = (double)((float)(i5 + 8) + MathHelper.cos(f6) * (float)this.numberOfBlocks / 8.0F);
            double d13 = (double)((float)(i5 + 8) - MathHelper.cos(f6) * (float)this.numberOfBlocks / 8.0F);
            double d15 = (double)(i4 + random2.NextInt(3) + 2);
            double d17 = (double)(i4 + random2.NextInt(3) + 2);

            for (int i19 = 0; i19 <= this.numberOfBlocks; ++i19)
            {
                double d20 = d7 + (d9 - d7) * (double)i19 / (double)this.numberOfBlocks;
                double d22 = d15 + (d17 - d15) * (double)i19 / (double)this.numberOfBlocks;
                double d24 = d11 + (d13 - d11) * (double)i19 / (double)this.numberOfBlocks;
                double d26 = random2.NextDouble() * (double)this.numberOfBlocks / 16.0D;
                double d28 = (double)(MathHelper.sin((float)i19 * (float)Math.PI / (float)this.numberOfBlocks) + 1.0F) * d26 + 1.0D;
                double d30 = (double)(MathHelper.sin((float)i19 * (float)Math.PI / (float)this.numberOfBlocks) + 1.0F) * d26 + 1.0D;

                for (int i32 = (int)(d20 - d28 / 2.0D); i32 <= (int)(d20 + d28 / 2.0D); ++i32)
                {
                    for (int i33 = (int)(d22 - d30 / 2.0D); i33 <= (int)(d22 + d30 / 2.0D); ++i33)
                    {
                        for (int i34 = (int)(d24 - d28 / 2.0D); i34 <= (int)(d24 + d28 / 2.0D); ++i34)
                        {
                            double d35 = ((double)i32 + 0.5D - d20) / (d28 / 2.0D);
                            double d37 = ((double)i33 + 0.5D - d22) / (d30 / 2.0D);
                            double d39 = ((double)i34 + 0.5D - d24) / (d28 / 2.0D);
                            if (d35 * d35 + d37 * d37 + d39 * d39 < 1.0D && world1.GetBlock(i32, i33, i34) == world1.theme.Cliff)
                            {
                                world1.SetBlock(i32, i33, i34, this.minableBlockId);
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
            this.plantBlockId = i1;
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
                    world1.SetBlock(i7, i8, i9, this.plantBlockId);
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
                                //No idea why this code doesn't work
                                // int i12 = world1.GetBlock(i10, i8, i11);
                                // if(i12 != Block.Air && i12 != Block.Leaves) {
                                // 	z7 = false;
                                // }
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
            this.generatorCollection = new NoiseGeneratorPerlin[octaves];

            for (int i3 = 0; i3 < octaves; ++i3)
            {
                this.generatorCollection[i3] = new NoiseGeneratorPerlin(random);
            }

        }

        public double generateNoise(double var1, double var5)
        {
            double var7 = 0.0D;
            double var9 = 1.0D;

            for (int var11 = 0; var11 < this.octaves; ++var11)
            {
                var7 += this.generatorCollection[var11].generateNoise(var1 * var9, var5 * var9) / var9;
                var9 /= 2.0D;
            }

            return var7;
        }

        public double generateNoise(double var1, double var3, double var5)
        {
            double var7 = 0.0D;
            double var9 = 1.0D;

            for (int var11 = 0; var11 < this.octaves; ++var11)
            {
                var7 += this.generatorCollection[var11].generateNoise(var1 * var9, var3 * var9, var5 * var9) / var9;
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

            for (int i19 = 0; i19 < this.octaves; ++i19)
            {
                this.generatorCollection[i19].populateNoiseArray(d1, d2, d4, d6, i8, i9, i10, d11 * d20, d13 * d20, d15 * d20, d20);
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
            this.permutations = new int[512];
            this.xCoord = random.NextDouble() * 256.0D;
            this.yCoord = random.NextDouble() * 256.0D;
            this.zCoord = random.NextDouble() * 256.0D;

            int i2;
            for (i2 = 0; i2 < 256; this.permutations[i2] = i2++)
            {
            }

            for (i2 = 0; i2 < 256; ++i2)
            {
                int i3 = random.NextInt(256 - i2) + i2;
                int i4 = this.permutations[i2];
                this.permutations[i2] = this.permutations[i3];
                this.permutations[i3] = i4;
                this.permutations[i2 + 256] = this.permutations[i2];
            }

        }

        public double generateNoise(double var1, double var3)
        {
            return this.generateNoise(var1, var3, 0.0D);
        }

        public double generateNoise(double d1, double d3, double d5)
        {
            double d7 = d1 + this.xCoord;
            double d9 = d3 + this.yCoord;
            double d11 = d5 + this.zCoord;
            int i13 = (int)d7;
            int i14 = (int)d9;
            int i15 = (int)d11;
            if (d7 < (double)i13)
            {
                --i13;
            }

            if (d9 < (double)i14)
            {
                --i14;
            }

            if (d11 < (double)i15)
            {
                --i15;
            }

            int i16 = i13 & 255;
            int i17 = i14 & 255;
            int i18 = i15 & 255;
            d7 -= (double)i13;
            d9 -= (double)i14;
            d11 -= (double)i15;
            double d19 = d7 * d7 * d7 * (d7 * (d7 * 6.0D - 15.0D) + 10.0D);
            double d21 = d9 * d9 * d9 * (d9 * (d9 * 6.0D - 15.0D) + 10.0D);
            double d23 = d11 * d11 * d11 * (d11 * (d11 * 6.0D - 15.0D) + 10.0D);
            int i25 = this.permutations[i16] + i17;
            int i26 = this.permutations[i25] + i18;
            int i27 = this.permutations[i25 + 1] + i18;
            int i28 = this.permutations[i16 + 1] + i17;
            int i29 = this.permutations[i28] + i18;
            int i30 = this.permutations[i28 + 1] + i18;
            return this.lerp(d23, this.lerp(d21, this.lerp(d19, this.grad(this.permutations[i26], d7, d9, d11), this.grad(this.permutations[i29], d7 - 1.0D, d9, d11)), this.lerp(d19, this.grad(this.permutations[i27], d7, d9 - 1.0D, d11), this.grad(this.permutations[i30], d7 - 1.0D, d9 - 1.0D, d11))), this.lerp(d21, this.lerp(d19, this.grad(this.permutations[i26 + 1], d7, d9, d11 - 1.0D), this.grad(this.permutations[i29 + 1], d7 - 1.0D, d9, d11 - 1.0D)), this.lerp(d19, this.grad(this.permutations[i27 + 1], d7, d9 - 1.0D, d11 - 1.0D), this.grad(this.permutations[i30 + 1], d7 - 1.0D, d9 - 1.0D, d11 - 1.0D))));
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

        public double a(int i, double d, double d1)
        {
            int j = i & 0xF;
            double d2 = (double)(1 - ((j & 8) >> 3)) * d;
            double d3 = j >= 4 ? (j != 12 && j != 14 ? d1 : d) : 0.0;
            return ((j & 1) != 0 ? -d2 : d2) + ((j & 2) != 0 ? -d3 : d3);
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
                double d20 = (d + (double)i5) * d3 + this.xCoord;
                long k5 = (long)d20;
                if (d20 < (double)k5)
                {
                    --k5;
                }
                int i6 = (int)(k5 & 0xFFL);
                double d22 = (d20 -= (double)k5) * d20 * d20 * (d20 * (d20 * 6.0 - 15.0) + 10.0);
                for (int j6 = 0; j6 < k; ++j6)
                {
                    double d24 = (d2 + (double)j6) * d5 + this.zCoord;
                    long k6 = (long)d24;
                    if (d24 < (double)k6)
                    {
                        --k6;
                    }
                    int l6 = (int)(k6 & 0xFFL);
                    double d25 = (d24 -= (double)k6) * d24 * d24 * (d24 * (d24 * 6.0 - 15.0) + 10.0);
                    for (int i7 = 0; i7 < j; ++i7)
                    {
                        double d26 = (d1 + (double)i7) * d4 + this.yCoord;
                        int j7 = (int)d26;
                        if (d26 < (double)j7)
                        {
                            --j7;
                        }
                        int k7 = j7 & 0xFF;
                        double d27 = (d26 -= (double)j7) * d26 * d26 * (d26 * (d26 * 6.0 - 15.0) + 10.0);
                        if (i7 == 0 || k7 != i2)
                        {
                            i2 = k7;
                            int j2 = this.permutations[i6] + k7;
                            int k2 = this.permutations[j2] + l6;
                            int l2 = this.permutations[j2 + 1] + l6;
                            int i3 = this.permutations[i6 + 1] + k7;
                            int k3 = this.permutations[i3] + l6;
                            int l3 = this.permutations[i3 + 1] + l6;
                            d13 = this.lerp(d22, this.grad(this.permutations[k2], d20, d26, d24), this.grad(this.permutations[k3], d20 - 1.0, d26, d24));
                            d15 = this.lerp(d22, this.grad(this.permutations[l2], d20, d26 - 1.0, d24), this.grad(this.permutations[l3], d20 - 1.0, d26 - 1.0, d24));
                            d16 = this.lerp(d22, this.grad(this.permutations[k2 + 1], d20, d26, d24 - 1.0), this.grad(this.permutations[k3 + 1], d20 - 1.0, d26, d24 - 1.0));
                            d18 = this.lerp(d22, this.grad(this.permutations[l2 + 1], d20, d26 - 1.0, d24 - 1.0), this.grad(this.permutations[l3 + 1], d20 - 1.0, d26 - 1.0, d24 - 1.0));
                        }
                        double d28 = this.lerp(d27, d13, d15);
                        double d29 = this.lerp(d27, d16, d18);
                        double d30 = this.lerp(d25, d28, d29);
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
            this.heightMap = new int[256];

            if (blocks != null) this.GenerateHeightMap();
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
            int i1 = world.wHeight;

            for (int z = 0; z < 16; ++z)
            {
                for (int x = 0; x < 16; ++x)
                {
                    int i4 = world.wHeight - 1;

                    int i5 = z << 4 | x;
                    int id;
                    do
                    {
                        id = this.blocks[(i4 - 1) << 8 | i5];
                        i4--;
                    }
                    while (i4 > 0 && world.mcgLevel.LightPasses((ushort)id));

                    this.heightMap[i5] = i4;
                    if (i4 < i1)
                    {
                        i1 = i4;
                    }
                }
            }

            // this.height = i1;
            // this.isModified = true;
        }

        public int GetHeightValue(int x, int z)
        {
            return this.heightMap[z << 4 | x];
        }
    }

    public class GenWorld
    {
        public int xChTotal, zChTotal, wHeight;
        public MapGenBiome theme;
        public GenChunk[] chunks;
        public long seed;
        public Level mcgLevel;
        public ChunkBasedOctaveGenerator chunkGenerator { private get; set; }

        public GenWorld(int xChTotal, int zChTotal, int wHeight, MapGenBiome theme, long seed, Level mcgLevel)
        {
            this.xChTotal = xChTotal;
            this.zChTotal = zChTotal;
            this.wHeight = wHeight;
            this.theme = theme;
            this.seed = seed;
            this.mcgLevel = mcgLevel;

            chunks = new GenChunk[xChTotal * zChTotal];
        }

        public void PopulateChunk(int x, int z)
        {
            if (!this.chunks[(x * zChTotal) + z].isTerrainPopulated && this.ChunkExists(x + 1, z + 1) && ChunkExists(x, z + 1) && ChunkExists(x + 1, z))
            {
                chunkGenerator.Populate(x, z);
            }

            if (ChunkExists(x - 1, z) && !GetChunkFromChunkCoords(x - 1, z).isTerrainPopulated && ChunkExists(x - 1, z + 1) && ChunkExists(x, z + 1) && this.ChunkExists(x - 1, z))
            {
                chunkGenerator.Populate(x - 1, z);
            }

            if (ChunkExists(x, z - 1) && !GetChunkFromChunkCoords(x, z - 1).isTerrainPopulated && ChunkExists(x + 1, z - 1) && ChunkExists(x, z - 1) && this.ChunkExists(x + 1, z))
            {
                chunkGenerator.Populate(x, z - 1);
            }

            if (ChunkExists(x - 1, z - 1) && !GetChunkFromBlockCoords(x - 1, z - 1).isTerrainPopulated && ChunkExists(x - 1, z - 1) && ChunkExists(x, z - 1) && this.ChunkExists(x - 1, z))
            {
                chunkGenerator.Populate(x - 1, z - 1);
            }
        }

        public bool ChunkExists(int x, int z)
        {
            return x < xChTotal && z < zChTotal && x > 0 && z > 0 && this.chunks[(x * zChTotal) + z] != null;
        }

        public GenChunk GetChunkFromChunkCoords(int x, int z)
        {
            if (x >= xChTotal || z >= zChTotal || x < 0 || z < 0)
            {
                return new GenChunk(this, x, z, new byte[256 * wHeight]);
            }
            else
            {
                if (this.chunks[(x * zChTotal) + z] == null)
                {
                    this.chunks[(x * zChTotal) + z] = chunkGenerator.generateChunk(x, z);
                    this.PopulateChunk(x, z);
                }
                return this.chunks[(x * zChTotal) + z];
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
                if (this.chunks[(cX * zChTotal) + cZ] == null)
                {
                    this.chunks[(cX * zChTotal) + cZ] = chunkGenerator.generateChunk(cX, cZ);
                    this.PopulateChunk(cX, cZ);
                }
                return this.chunks[(cX * zChTotal) + cZ];
            }
        }

        public void SetBlock(int x, int y, int z, int block)
        {
            GenChunk chunk = this.GetChunkFromBlockCoords(x, z);

            if (chunk == null || y >= wHeight || y < 0) return;
            chunk.SetBlock(x & 15, y, z & 15, block);
        }

        public int GetBlock(int x, int y, int z)
        {
            GenChunk chunk = this.GetChunkFromBlockCoords(x, z);

            if (chunk == null || y >= wHeight || y < 0) return 0;
            return chunk.GetBlock(x & 15, y, z & 15);
        }

        public int GetHeightValue(int x, int z)
        {
            GenChunk chunk = this.GetChunkFromBlockCoords(x, z);

            if (chunk == null) return 0;
            return chunk.GetHeightValue(x & 15, z & 15);
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
    }
}

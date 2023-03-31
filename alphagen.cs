using MCGalaxy.Generator;
using MCGalaxy.Generator.Classic;
using MCGalaxy.Generator.Foliage;
using System;

using BlockID = System.UInt16;

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
		private static byte getBlockInArray(byte[] array, int x, int y, int z)
		{
			return array[x << 11 | z << 7 | y];
		}

		public static bool Gen(Player p, Level lvl, MapGenArgs mgArgs)
		{
			MapGenBiomeName theme = MapGenBiomeName.Forest;
            
			if (!mgArgs.ParseArgs(p)) return false;
            theme = mgArgs.Biome;
            int rng_seed = mgArgs.Seed;
            
			MapGenBiome theme2 = MapGenBiome.Get(theme);
			theme2.ApplyEnv(lvl.Config);
			ChunkBasedOctaveGenerator generator = new ChunkBasedOctaveGenerator(rng_seed, theme2);

			int totalChunks = (lvl.Length / 16) * (lvl.Width / 16);
			int chunksGenerated = 1;
			for (ushort blockY = 0; blockY < lvl.Height; blockY++)
			{
				for (ushort blockZ = 0; blockZ < lvl.Length; blockZ++)
				{
					for (ushort blockX = 0; blockX < lvl.Width; blockX++)
					{
						if (blockY <= ((lvl.Height / 2) - 64))
						{
							lvl.SetBlock(blockX, blockY, blockZ, theme2.Cliff);
						}
					}
				}
			}

			for (ushort chunkZ = 0; chunkZ < lvl.Length / 16; chunkZ++)
			{
				for (ushort chunkX = 0; chunkX < lvl.Width / 16; chunkX++)
				{
					p.Message(String.Format("Generating chunk {0} out of {1}", chunksGenerated, totalChunks));
					byte[] chunk = generator.generateChunk(chunkX, chunkZ);
					chunksGenerated++;

					for (ushort blockY = 0; blockY < 127; blockY++)
					{
						for (ushort blockZ = 0; blockZ < 16; blockZ++)
						{
							for (ushort blockX = 0; blockX < 16; blockX++)
							{
								ushort globalBlockX = (ushort)(chunkX * 16 + blockX);
								ushort globalBlockZ = (ushort)(chunkZ * 16 + blockZ);
								byte block = getBlockInArray(chunk, blockX, blockY, blockZ);
								lvl.SetBlock(globalBlockX, (ushort)(blockY + ((lvl.Height / 2) - 64)), globalBlockZ, block);
							}
						}
					}
				}
			}

			p.Message("Now creating trees.");
			GenPlants(lvl, rng_seed, theme2);
			return true;
		}

		private static void GenPlants(Level lvl, int seed, MapGenBiome biome)
		{
			JavaRandom rand = new JavaRandom(seed);
			NoiseGeneratorPerlin treeGen = new NoiseGeneratorPerlin(rand);

			for (int y = 0; y < (ushort)(lvl.Height - 1); y++)
				for (int z = 0; z < lvl.Length; ++z)
					for (int x = 0; x < lvl.Width; ++x)
					{
						if (lvl.FastGetBlock((ushort)x, (ushort)y, (ushort)z) == biome.Surface &&
							lvl.FastGetBlock((ushort)x, (ushort)(y + 1), (ushort)z) == Block.Air)
						{
							bool nope = false;
							int maybenot = 0;
							if (rand.Next(0, 50) == 0)
							{
								Tree tree = GetTreeGen(biome, rand);
								if (tree == null) continue;
								for (int x1 = 0; x1 < 5; x1++)
								{
									for (int y1 = 0; y1 < 5; y1++)
									{
										for (int z1 = 0; z1 < 5; z1++)
										{
											if (!lvl.IsAirAt((ushort)(x + x1), (ushort)(y + y1), (ushort)(z + z1))) maybenot++;
											if (maybenot > 63) nope = true;
										}
									}
								}
								
								if (nope) continue;
								double xVal = (double)x / 200, yVal = (double)y / 130, zVal = z / 200;
								const double adj = 1;
								xVal += adj;
								yVal += adj;
								zVal += adj;
								double value = treeGen.generateNoise(xVal, yVal, zVal);
								if (value > rand.NextFloat())
								{
									GenTree((ushort)x, (ushort)(y + 1), (ushort)z, rand, lvl, seed, tree);
									lvl.SetBlock((ushort)x, (ushort)(y), (ushort)z, biome.Ground);
								}
								else if (rand.Next(0, 20) == 0)
								{
									GenTree((ushort)x, (ushort)(y + 1), (ushort)z, rand, lvl, seed, tree);
									lvl.SetBlock((ushort)x, (ushort)(y), (ushort)z, biome.Ground);
								}
							}
						}
					}
		}

        static Tree GetTreeGen(MapGenBiome biome, JavaRandom rnd)
		{
			if (biome.TreeType == null) return null;
			if (biome.TreeType == "") 
			{
				if (rnd.Next(0, 20) == 0)
				{
					return new OakTree();
				}
				else
                {
					return new ClassicTree() { rng = rnd };
				}
			}

			return Tree.TreeTypes[biome.TreeType]();
		}

		static void GenTree(ushort x, ushort y, ushort z, JavaRandom random, Level lvl, int seed, Tree tree)
		{
            //new random every time a tree is generated? Trees generated close-in-time to each other will be the same, is that intentional?
			tree.SetData(new Random(seed), random.Next(0, 8));
			PlaceBlocks(lvl, tree, x, y, z);
		}

		private static void PlaceBlocks(Level lvl, Tree tree, int x, int y, int z)
		{
			tree.Generate((ushort)x, (ushort)(y), (ushort)z, (X, Y, Z, raw) => {
				BlockID here = lvl.GetBlock(X, Y, Z);
				if (here == Block.Air || here == Block.Leaves)
				{
					lvl.SetTile(X, Y, Z, (byte)raw);
				}
			});
		}
	}

	public class ChunkBasedOctaveGenerator
	{
		private JavaRandom rand;
		private MapGenBiome biome;
		private NoiseGeneratorOctaves noiseGen1;
		private NoiseGeneratorOctaves noiseGen2;
		private NoiseGeneratorOctaves noiseGen3;
		private NoiseGeneratorOctaves noiseGen4;
		private NoiseGeneratorOctaves noiseGen5;
		private NoiseGeneratorOctaves noiseGen6;
		private NoiseGeneratorOctaves noiseGen7;
		private double[] noiseArray;
		private double[] sandNoise = new double[256];
		private double[] gravelNoise = new double[256];
		private double[] stoneNoise = new double[256];
		double[] noise3;
		double[] noise1;
		double[] noise2;
		double[] noise6;
		double[] noise7;

		public ChunkBasedOctaveGenerator(int seed, MapGenBiome theme)
		{
			this.rand = new JavaRandom(seed);
			biome = theme;
			this.noiseGen1 = new NoiseGeneratorOctaves(this.rand, 16);
			this.noiseGen2 = new NoiseGeneratorOctaves(this.rand, 16);
			this.noiseGen3 = new NoiseGeneratorOctaves(this.rand, 8);
			this.noiseGen4 = new NoiseGeneratorOctaves(this.rand, 4);
			this.noiseGen5 = new NoiseGeneratorOctaves(this.rand, 4);
			this.noiseGen6 = new NoiseGeneratorOctaves(this.rand, 10);
			this.noiseGen7 = new NoiseGeneratorOctaves(this.rand, 16);
		}

		private void generateTerrain(int chunkX, int chunkZ, byte[] blocks)
		{
			byte b4 = 4;
			byte b5 = 64;
			int i6 = b4 + 1;
			byte b7 = 17;
			int i8 = b4 + 1;
			this.noiseArray = this.initializeNoiseField(this.noiseArray, chunkX * b4, 0, chunkZ * b4, i6, b7, i8);

			for (int i9 = 0; i9 < b4; ++i9)
			{
				for (int i10 = 0; i10 < b4; ++i10)
				{
					for (int i11 = 0; i11 < 16; ++i11)
					{
						double d12 = 0.125D;
						double d14 = this.noiseArray[((i9 + 0) * i8 + i10 + 0) * b7 + i11 + 0];
						double d16 = this.noiseArray[((i9 + 0) * i8 + i10 + 1) * b7 + i11 + 0];
						double d18 = this.noiseArray[((i9 + 1) * i8 + i10 + 0) * b7 + i11 + 0];
						double d20 = this.noiseArray[((i9 + 1) * i8 + i10 + 1) * b7 + i11 + 0];
						double d22 = (this.noiseArray[((i9 + 0) * i8 + i10 + 0) * b7 + i11 + 1] - d14) * d12;
						double d24 = (this.noiseArray[((i9 + 0) * i8 + i10 + 1) * b7 + i11 + 1] - d16) * d12;
						double d26 = (this.noiseArray[((i9 + 1) * i8 + i10 + 0) * b7 + i11 + 1] - d18) * d12;
						double d28 = (this.noiseArray[((i9 + 1) * i8 + i10 + 1) * b7 + i11 + 1] - d20) * d12;

						for (int i30 = 0; i30 < 8; ++i30)
						{
							double d31 = 0.25D;
							double d33 = d14;
							double d35 = d16;
							double d37 = (d18 - d14) * d31;
							double d39 = (d20 - d16) * d31;

							for (int i41 = 0; i41 < 4; ++i41)
							{
								int i42 = i41 + i9 * 4 << 11 | 0 + i10 * 4 << 7 | i11 * 8 + i30;
								short s43 = 128;
								double d44 = 0.25D;
								double d46 = d33;
								double d48 = (d35 - d33) * d44;

								for (int i50 = 0; i50 < 4; ++i50)
								{
									int i51 = 0;
									if (i11 * 8 + i30 < b5)
									{
										i51 = biome.Water;
									}

									if (d46 > 0.0D)
									{
										i51 = biome.Cliff;
									}

									blocks[i42] = (byte)i51;
									i42 += s43;
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

		private void replaceSurfaceBlocks(int chunkX, int chunkZ, byte[] blocks)
		{
			byte seaLevel = 64;
			double d5 = 8.0D / 256D;
			this.sandNoise = this.noiseGen4.generateNoiseOctaves(this.sandNoise, (double)(chunkX * 16), (double)(chunkZ * 16), 0.0D, 16, 16, 1, d5, d5, 1.0D);
			this.gravelNoise = this.noiseGen4.generateNoiseOctaves(this.gravelNoise, (double)(chunkZ * 16), 109.0134D, (double)(chunkX * 16), 16, 1, 16, d5, 1.0D, d5);
			this.stoneNoise = this.noiseGen5.generateNoiseOctaves(this.stoneNoise, (double)(chunkX * 16), (double)(chunkZ * 16), 0.0D, 16, 16, 1, d5 * 2.0D, d5 * 2.0D, d5 * 2.0D);

			for (int i7 = 0; i7 < 16; ++i7)
			{
				for (int i8 = 0; i8 < 16; ++i8)
				{
					bool generateSandBeach = this.sandNoise[i7 * 16 + i8] + this.rand.NextFloat() * 0.2D > 0.0D;
					bool generateGravelBeach = this.gravelNoise[i7 + i8 * 16] + this.rand.NextFloat() * 0.2D > 3.0D;
					int exposedStone = (int)(this.stoneNoise[i7 * 16 + i8] / 3.0D + 3.0D + this.rand.NextFloat() * 0.25D);
					int i12 = -1;
					byte surface = biome.Surface;
					byte ground = biome.Ground;
					byte cliff = biome.Cliff;
					byte water = biome.Water;
					if (water == 0) seaLevel = 0;

					for (int i15 = 127; i15 >= 0; --i15)
					{
						int i16 = (i7 * 16 + i8) * 128 + i15;
						byte block = blocks[i16];
						if (block == 0)
						{
							i12 = -1;
						}
						else if (block == cliff)
						{
							if (i12 == -1)
							{
								if (exposedStone <= 0)
								{
									surface = 0;
									ground = cliff;
								}
								else if (i15 >= seaLevel - 4 && i15 <= seaLevel + 1)
								{
									if (generateGravelBeach)
									{
										surface = 0;
										ground = biome.BeachRocky;
									}

									if (generateSandBeach)
									{
										surface = biome.BeachSandy;
										ground = biome.BeachSandy;
									}
								}

								if (i15 < seaLevel && surface == 0)
								{
									surface = water;
								}

								i12 = exposedStone;
								if (i15 >= seaLevel - 1)
								{
									blocks[i16] = surface;
								}
								else
								{
									blocks[i16] = ground;
								}
							}
							else if (i12 > 0)
							{
								--i12;
								blocks[i16] = ground;
							}
						}
					}
				}
			}

		}

		public byte[] generateChunk(int i1, int i2)
		{
			this.rand.SetSeed((int)(i1 * 341873128712L + i2 * 132897987541L));
			byte[] chunkBlocks = new byte[32768];

			this.generateTerrain(i1, i2, chunkBlocks);
			this.replaceSurfaceBlocks(i1, i2, chunkBlocks);

			return chunkBlocks;
		}
		private double[] initializeNoiseField(double[] d1, int i2, int i3, int i4, int i5, int i6, int i7)
		{
			if (d1 == null)
			{
				d1 = new double[i5 * i6 * i7];
			}

			double d8 = 684.412D;
			double d10 = 684.412D;
			this.noise6 = this.noiseGen6.generateNoiseOctaves(this.noise6, (double)i2, (double)i3, (double)i4, i5, 1, i7, 1.0D, 0.0D, 1.0D);
			this.noise7 = this.noiseGen7.generateNoiseOctaves(this.noise7, (double)i2, (double)i3, (double)i4, i5, 1, i7, 100.0D, 0.0D, 100.0D);
			this.noise3 = this.noiseGen3.generateNoiseOctaves(this.noise3, (double)i2, (double)i3, (double)i4, i5, i6, i7, d8 / 80.0D, d10 / 160.0D, d8 / 80.0D);
			this.noise1 = this.noiseGen1.generateNoiseOctaves(this.noise1, (double)i2, (double)i3, (double)i4, i5, i6, i7, d8, d10, d8);
			this.noise2 = this.noiseGen2.generateNoiseOctaves(this.noise2, (double)i2, (double)i3, (double)i4, i5, i6, i7, d8, d10, d8);
			int i12 = 0;
			int i13 = 0;

			for (int i14 = 0; i14 < i5; ++i14)
			{
				for (int i15 = 0; i15 < i7; ++i15)
				{
					double d16 = (this.noise6[i13] + 256.0D) / 512.0D;
					if (d16 > 1.0D)
					{
						d16 = 1.0D;
					}

					double d18 = 0.0D;
					double d20 = this.noise7[i13] / 8000.0D;
					if (d20 < 0.0D)
					{
						d20 = -d20;
					}

					d20 = d20 * 3.0D - 3.0D;
					if (d20 < 0.0D)
					{
						d20 /= 2.0D;
						if (d20 < -1.0D)
						{
							d20 = -1.0D;
						}

						d20 /= 1.4D;
						d20 /= 2.0D;
						d16 = 0.0D;
					}
					else
					{
						if (d20 > 1.0D)
						{
							d20 = 1.0D;
						}

						d20 /= 6.0D;
					}

					d16 += 0.5D;
					d20 = d20 * (double)i6 / 16.0D;
					double d22 = (double)i6 / 2.0D + d20 * 4.0D;
					++i13;

					for (int i24 = 0; i24 < i6; ++i24)
					{
						double d25 = 0.0D;
						double d27 = ((double)i24 - d22) * 12.0D / d16;
						if (d27 < 0.0D)
						{
							d27 *= 4.0D;
						}

						double d29 = this.noise1[i12] / 512.0D;
						double d31 = this.noise2[i12] / 512.0D;
						double d33 = (this.noise3[i12] / 10.0D + 1.0D) / 2.0D;
						if (d33 < 0.0D)
						{
							d25 = d29;
						}
						else if (d33 > 1.0D)
						{
							d25 = d31;
						}
						else
						{
							d25 = d29 + (d31 - d29) * d33;
						}

						d25 -= d27;
						double d35;
						if (i24 > i6 - 4)
						{
							d35 = (double)((float)(i24 - (i6 - 4)) / 3.0F);
							d25 = d25 * (1.0D - d35) + -10.0D * d35;
						}

						if ((double)i24 < d18)
						{
							d35 = (d18 - (double)i24) / 4.0D;
							if (d35 < 0.0D)
							{
								d35 = 0.0D;
							}

							if (d35 > 1.0D)
							{
								d35 = 1.0D;
							}

							d25 = d25 * (1.0D - d35) + -10.0D * d35;
						}

						d1[i12] = d25;
						++i12;
					}
				}
			}

			return d1;
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

		public NoiseGeneratorPerlin()
		{
			new NoiseGeneratorPerlin(new JavaRandom(new Random().Next()));
		}

		public NoiseGeneratorPerlin(JavaRandom random)
		{
			this.permutations = new int[512];
			this.xCoord = random.NextFloat() * 256.0D;
			this.yCoord = random.NextFloat() * 256.0D;
			this.zCoord = random.NextFloat() * 256.0D;

			int i2;
			for (i2 = 0; i2 < 256; this.permutations[i2] = i2++)
			{
			}

			for (i2 = 0; i2 < 256; ++i2)
			{
				int i3 = random.Next(256 - i2) + i2;
				int i4 = this.permutations[i2];
				this.permutations[i2] = this.permutations[i3];
				this.permutations[i3] = i4;
				this.permutations[i2 + 256] = this.permutations[i2];
			}

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
			if (j == 1 || k == 1)
			{
				bool flag = false;
				bool flag1 = false;
				bool flag2 = false;
				bool flag3 = false;
				double d8 = 0.0;
				double d10 = 0.0;
				int j3 = 0;
				double d12 = 1.0 / d6;
				for (int i4 = 0; i4 < i; ++i4)
				{
					double d14 = (d + (double)i4) * d3 + this.xCoord;
					long j4 = (long)d14;
					if (d14 < (double)j4)
					{
						--j4;
					}
					int k4 = (int)(j4 & 0xFFL);
					double d17 = (d14 -= (double)j4) * d14 * d14 * (d14 * (d14 * 6.0 - 15.0) + 10.0);
					for (int l4 = 0; l4 < (k == 1 ? j : k); ++l4)
					{
						double d19 = k == 1 ? ((d1 + (double)l4) * d4 + zCoord) : ((d2 + (double)l4) * d5 + zCoord);
						long j5 = (long)d19;
						if (d19 < (double)j5)
						{
							--j5;
						}
						int l5 = (int)(j5 & 0xFFL);
						double d21 = (d19 -= (double)j5) * d19 * d19 * (d19 * (d19 * 6.0 - 15.0) + 10.0);
						int l = this.permutations[k4] + 0;
						int j1 = this.permutations[l] + l5;
						int k1 = this.permutations[k4 + 1] + 0;
						int l1 = this.permutations[k1] + l5;
						double d9 = this.lerp(d17, this.a(this.permutations[j1], d14, d19), this.grad(this.permutations[l1], d14 - 1.0, 0.0, d19));
						double d11 = this.lerp(d17, this.grad(this.permutations[j1 + 1], d14, 0.0, d19 - 1.0), this.grad(this.permutations[l1 + 1], d14 - 1.0, 0.0, d19 - 1.0));
						double d23 = this.lerp(d21, d9, d11);
						int n = j3++;
						ad[n] = ad[n] + d23 * d12;
					}
				}
				return;
			}

			int i1 = 0;
			double d7 = 1.0 / d6;
			int i2 = -1;
			bool flag4 = false;
			bool flag5 = false;
			bool flag6 = false;
			bool flag7 = false;
			bool flag8 = false;
			bool flag9 = false;
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
}

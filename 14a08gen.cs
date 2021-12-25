// Copyright 2014-2017 ClassicalSharp | Licensed under BSD-3
// Based on: https://github.com/UnknownShadow200/ClassiCube/wiki/Minecraft-Classic-map-generation-algorithm
using System;
using System.Collections.Generic;
using MCGalaxy;
using MCGalaxy.Generator;
using ClassicalSharp.Generator;

namespace MCGalaxy  {
	
	public sealed class PluginFourteenaOEightGen : Plugin {
		public override string name { get { return "Plugin14a08Gen"; } }
		public override string MCGalaxy_Version { get { return "1.9.3.7"; } }
		public override string creator { get { return "icanttellyou"; } }


		public override void Load(bool startup) {
			MapGen.Register("Classic14a08", GenType.Advanced, FourteenaOEightGenerator.Gen, "&cThis is not accurate by any means! &HSeed affects how terrain is generated. If seed is the same, the generated level will be the same.");
		}
		public override void Unload(bool shutdown) {
			
		}
	}
    
    public partial class FourteenaOEightGenerator {
        
        int waterLevel, oneY, Width, Length, Height;
        byte[] blocks;
        short[] heightmap;
        JavaRandom rnd;
        int minHeight;
        string CurrentState;
		
        public static bool Gen(Player p, Level lvl, string seed) {      
            int seed_ = MCGalaxy.Generator.MapGen.MakeInt(seed);
            new FourteenaOEightGenerator().Generate(lvl, seed_);
            return true;
        }
        
        public byte[] Generate(Level lvl, int seed) {
            blocks = lvl.blocks;
            Width  = lvl.Width;
            Height = lvl.Height;
            Length = lvl.Length;
            
            rnd = new JavaRandom(seed);
            oneY = Width * Length;
            waterLevel = Height / 2;
            minHeight  = Height;
            
            CreateHeightmap();
            CreateStrata();
            CarveCaves();
            CarveOreVeins(0.9f, "coal ore", Block.CoalOre);
            CarveOreVeins(0.7f, "iron ore", Block.IronOre);
            CarveOreVeins(0.5f, "gold ore", Block.GoldOre);
            
            FloodFillWaterBorders();
            FloodFillWater();
            FloodFillLava();

            CreateSurfaceLayer();
            PlantTrees();
            return blocks;
        }
        
        void CreateHeightmap() {
            CombinedNoise n1 = new CombinedNoise(
                new OctaveNoise(8, rnd), new OctaveNoise(8, rnd));
            CombinedNoise n2 = new CombinedNoise(
                new OctaveNoise(8, rnd), new OctaveNoise(8, rnd));
            OctaveNoise n3 = new OctaveNoise(8, rnd);
            int index = 0;
            short[] hMap = new short[Width * Length];
            CurrentState = "Building heightmap";
            
            for (int z = 0; z < Length; z++) {
                for (int x = 0; x < Width; x++) {
                    double hLow = n1.Compute(x * 1.3f, z * 1.3f) / 8 - 8, height = hLow;
					double hHigh = n2.Compute(x * 1.3f, z * 1.3f) / 6 + 6;
                    
                    if (n3.Compute(x, z) / 8 > 0) {
                        hHigh = hLow;
                    }
                    
                    //height *= 0.5;
                    if ((height = Math.Max(hLow, hHigh) / 2.0D) < 0.0D) height /= 2.0f;
                    
                    int adjHeight = (int)(height + waterLevel);
                    minHeight = adjHeight < minHeight ? adjHeight : minHeight;
                    hMap[index++] = (short)adjHeight;
                }
            }
            heightmap = hMap;
        }
        
        void CreateStrata() {
            OctaveNoise n = new OctaveNoise(8, rnd);
            CurrentState = "Creating strata";            
            int hMapIndex = 0, hMapIndex2 = 0, maxY = Height - 1, mapIndex = 0;
            // Try to bulk fill bottom of the map if possible
            int minStoneY = CreateStrataFast();
			int[] heightmap2 = Array.ConvertAll<short, int>(heightmap,
			delegate(short i)
			{
				return (int)i;
			});

            for (int z = 0; z < Length; z++) {
                for (int x = 0; x < Width; x++) {
                    int dirtThickness = (int)(n.Compute(x, z) / 24 - 4);
                    int dirtHeight = heightmap[hMapIndex++];
                    int stoneHeight = dirtHeight + dirtThickness;    
					heightmap[hMapIndex2++] = (short)Math.Max(dirtHeight, stoneHeight);
                    
                    //stoneHeight = Math.Max(stoneHeight, maxY);
                    //dirtHeight  = Math.Max(dirtHeight,  maxY);
					
					if (stoneHeight > Height) stoneHeight = Height - 1;
					if (dirtHeight > Height) dirtHeight = Height - 1;
                    
                    mapIndex = minStoneY * oneY + z * Width + x;
                    for (int y = minStoneY; y <= stoneHeight; y++) {
						//if (mapIndex > blocks.Length) mapIndex = blocks.Length - 1;
						//if (mapIndex > blocks.Length) Logger.Log(LogType.Debug, blocks.Length + " " + mapIndex);
						if (mapIndex > blocks.Length) mapIndex = blocks.Length - 1;
                        blocks[mapIndex] = Block.Stone; mapIndex += oneY;
                    }
                    
                    stoneHeight = Math.Max(stoneHeight, 0);
                    mapIndex = (stoneHeight + 1) * oneY + z * Width + x;
					//Logger.Log(LogType.Debug, stoneHeight + " " + dirtHeight);
                    for (int y = stoneHeight + 1; y <= dirtHeight; y++) {
						//if (mapIndex > blocks.Length) mapIndex = blocks.Length - 1;
						//if (mapIndex > blocks.Length) Logger.Log(LogType.Debug, blocks.Length + " " + mapIndex);
						if (mapIndex > blocks.Length) mapIndex = blocks.Length - 1;
                        blocks[mapIndex] = Block.Dirt; mapIndex += oneY;
                    }
                }
            }
        }
        
        int CreateStrataFast() {
            // Make lava layer at bottom
            int mapIndex = 0;
            for (int z = 0; z < Length; z++)
                for (int x = 0; x < Width; x++)
            {
                blocks[mapIndex++] = Block.Lava;
            }
            
            // Invariant: the lowest value dirtThickness can possible be is -14
            int stoneHeight = minHeight - 14;
            if (stoneHeight <= 0) return 1; // no layer is fully stone
            
            // We can quickly fill in bottom solid layers
            for (int y = 1; y <= stoneHeight; y++)
                for (int z = 0; z < Length; z++)
                    for (int x = 0; x < Width; x++)
            {
                blocks[mapIndex++] = Block.Stone;
            }
            return stoneHeight;
        }
        
        void CarveCaves() {
            int cavesCount = blocks.Length / 8192;
            CurrentState = "Carving caves";
            
            for (int i = 0; i < cavesCount; i++) {
                double caveX = rnd.NextFloat() * (float)Width;
                double caveY = rnd.NextFloat() * (float)Height;
                double caveZ = rnd.NextFloat() * (float)Length;
                
                int caveLen  = (int)(rnd.NextFloat() * rnd.NextFloat() * 75);
                double theta = rnd.NextFloat() * 2 * Math.PI, deltaTheta = 0;
                double phi   = rnd.NextFloat() * 2 * Math.PI, deltaPhi = 0;
                double caveRadius = rnd.NextFloat() * rnd.NextFloat();
                
                for (int j = 0; j < caveLen; j++) {
                    caveX += Math.Sin(theta) * Math.Cos(phi);
                    caveZ += Math.Cos(theta) * Math.Cos(phi);
                    caveY += Math.Sin(phi);
                    
                    theta = theta + deltaTheta * 0.2;
                    deltaTheta = deltaTheta * 0.9 + rnd.NextFloat() - rnd.NextFloat();
                    phi = phi * 0.5 + deltaPhi * 0.5;
                    deltaPhi = deltaPhi * 0.9 + rnd.NextFloat() - rnd.NextFloat();
                    //if (rnd.NextFloat() < 0.25) continue;
                    
                    int cenX = (int)(caveX + (rnd.Next(4) - 2) * 0.2);
                    int cenY = (int)(caveY + (rnd.Next(4) - 2) * 0.2);
                    int cenZ = (int)(caveZ + (rnd.Next(4) - 2) * 0.2);
                    
                    double radius = Math.Sin(j * Math.PI / caveLen) * 2.5 + 1;
                    FillOblateSpheroid(cenX, cenY, cenZ, (float)radius, Block.Air);
                }
            }
        }
        
        void CarveOreVeins(float abundance, string blockName, byte block) {
            int numVeins = (int)(blocks.Length * abundance / 16384);
            CurrentState = "Carving " + blockName;
            
            for (int i = 0; i < numVeins; i++) {
                double veinX = rnd.Next(Width);
                double veinY = rnd.Next(Height);
                double veinZ = rnd.Next(Length);
                
                int veinLen = (int)(rnd.NextFloat() * rnd.NextFloat() * 75 * abundance);
                double theta = rnd.NextFloat() * 2 * Math.PI, deltaTheta = 0;
                double phi = rnd.NextFloat() * 2 * Math.PI, deltaPhi = 0;
                
                for (int j = 0; j < veinLen; j++) {
                    veinX += Math.Sin(theta) * Math.Cos(phi);
                    veinZ += Math.Cos(theta) * Math.Cos(phi);
                    veinY += Math.Sin(phi);
                    
                    theta = deltaTheta * 0.2;
                    deltaTheta = deltaTheta * 0.9 + rnd.NextFloat() - rnd.NextFloat();
                    phi = phi / 2 + deltaPhi / 4;
                    deltaPhi = deltaPhi * 0.9 + rnd.NextFloat() - rnd.NextFloat();
                    
                    float radius = abundance * (float)Math.Sin(j * Math.PI / veinLen) + 1;
                    FillOblateSpheroid((int)veinX, (int)veinY, (int)veinZ, radius, block);
                }
            }
        }
        
        void FloodFillWaterBorders() {
            int waterY = waterLevel - 1;
            int index1 = (waterY * Length + 0) * Width + 0;
            int index2 = (waterY * Length + (Length - 1)) * Width + 0;
            CurrentState = "Flooding edge water";
            
            for (int x = 0; x < Width; x++) {
                FloodFill(index1, Block.Water);
                FloodFill(index2, Block.Water);
                index1++; index2++;
            }
            
            index1 = (waterY * Length + 0) * Width + 0;
            index2 = (waterY * Length + 0) * Width + (Width - 1);
            for (int z = 0; z < Length; z++) {
                FloodFill(index1, Block.Water);
                FloodFill(index2, Block.Water);
                index1 += Width; index2 += Width;
            }
        }
        
        void FloodFillWater() {
            int numSources = Width * Length / 200;
            CurrentState = "Flooding water";
            
            for (int i = 0; i < numSources; i++) {
                int x = rnd.Next(Width), z = rnd.Next(Length);
                int y = waterLevel - rnd.Next(1, 3);
                FloodFill((y * Length + z) * Width + x, Block.Water);
            }
        }
        
        void FloodFillLava() {
            int numSources = Width * Length / 10000;
            CurrentState = "Flooding lava";
            
            for (int i = 0; i < numSources; i++) {
                int x = rnd.Next(Width), z = rnd.Next(Length);
                int y = (int)((waterLevel - 3) * rnd.NextFloat() * rnd.NextFloat());
                FloodFill((y * Length + z) * Width + x, Block.Lava);
            }
        }
        
        void CreateSurfaceLayer() {
            OctaveNoise n1 = new OctaveNoise(8, rnd), n2 = new OctaveNoise(8, rnd);
            CurrentState = "Creating surface";
            // TODO: update heightmap
            
            int hMapIndex = 0;
            for (int z = 0; z < Length; z++) {
                for (int x = 0; x < Width; x++) {
                    int y = heightmap[hMapIndex++];
                    if (y < 0 || y >= Height) continue;
                    
                    int index = (y * Length + z) * Width + x;
                    byte blockAbove = y >= (Height - 1) ? Block.Air : blocks[index + oneY];
					if(blockAbove == Block.Air) {
						if (y <= waterLevel - 1 && n2.Compute(x, z) > 12) {
							blocks[index] = Block.Gravel;
						} else if (y <= waterLevel - 1 && n1.Compute(x, z) > 8) {
							blocks[index] = Block.Sand;
						} else {
							blocks[index] = Block.Grass;
						}
					}

                }
            }
        }
        
        void PlantTrees() {
            int numPatches = Width * Length / 4000;
            CurrentState = "Planting trees";
            
            for (int i = 0; i < numPatches; i++) {
                int patchX = rnd.Next(Width), patchZ = rnd.Next(Length);
                
                for (int j = 0; j < 20; j++) {
                    int treeX = patchX, treeZ = patchZ;
                    for (int k = 0; k < 20; k++) {
                        treeX += rnd.Next(6) - rnd.Next(6);
                        treeZ += rnd.Next(6) - rnd.Next(6);
                        if (treeX < 0 || treeZ < 0 || treeX >= Width ||
                            treeZ >= Length || rnd.NextFloat() >= 0.25)
                            continue;
                        
                        int treeY = heightmap[treeZ * Width + treeX] + 1;
                        if (treeY >= Height) continue;
                        int treeHeight = 5 + rnd.Next(3);
                        
                        int index = (treeY * Length + treeZ) * Width + treeX;
                        byte blockUnder = treeY > 0 ? blocks[index - oneY] : Block.Air;
                        
                        if (blockUnder == Block.Grass && CanGrowTree(treeX, treeY, treeZ, treeHeight)) {
                            GrowTree(treeX, treeY, treeZ, treeHeight);
                        }
                    }
                }
            }
        }
        
        bool CanGrowTree(int treeX, int treeY, int treeZ, int treeHeight) {
            // check tree base
            int baseHeight = treeHeight - 4;
            for (int y = treeY; y < treeY + baseHeight; y++)
                for (int z = treeZ - 1; z <= treeZ + 1; z++)
                    for (int x = treeX - 1; x <= treeX + 1; x++)
            {
                if (x < 0 || y < 0 || z < 0 || x >= Width || y >= Height || z >= Length)
                    return false;
                int index = (y * Length + z) * Width + x;
                if (blocks[index] != 0) return false;
            }
            
            // and also check canopy
            for (int y = treeY + baseHeight; y < treeY + treeHeight; y++)
                for (int z = treeZ - 2; z <= treeZ + 2; z++)
                    for (int x = treeX - 2; x <= treeX + 2; x++)
            {
                if (x < 0 || y < 0 || z < 0 || x >= Width || y >= Height || z >= Length)
                    return false;
                int index = (y * Length + z) * Width + x;
                if (blocks[index] != 0) return false;
            }
            return true;
        }
        
        void GrowTree(int treeX, int treeY, int treeZ, int height) {
            int baseHeight = height - 4;
            int index = 0;
            
            // // leaves bottom layer
            // for (int y = treeY + baseHeight; y < treeY + baseHeight + 2; y++)
                // for (int zz = -2; zz <= 2; zz++)
                    // for (int xx = -2; xx <= 2; xx++)
            // {
                // int x = xx + treeX, z = zz + treeZ;
                // index = (y * Length + z) * Width + x;
                
                // if (Math.Abs(xx) == 2 && Math.Abs(zz) == 2) {
                    // if (rnd.NextFloat() >= 0.5)
                        // blocks[index] = Block.Leaves;
                // } else {
                    // blocks[index] = Block.Leaves;
                // }
            // }
            
            // leaves top layer
            int bottomY = treeY + baseHeight + 2;
            for (int y = treeY + baseHeight + 1; y < treeY + height; y++)
                for (int zz = -1; zz <= 1; zz++)
                    for (int xx = -1; xx <= 1; xx++)
            {
                int x = xx + treeX, z = zz + treeZ;
                index = (y * Length + z) * Width + x;

                if (xx == 0 || zz == 0) {
                    blocks[index] = Block.Leaves;
                } else if (y <= bottomY /*&& rnd.NextFloat() >= 0.5*/) {
                    blocks[index] = Block.Leaves;
                }
            }
            
            // then place trunk
            index = (treeY * Length + treeZ) * Width + treeX;
            for (int y = 0; y < height - 1; y++) {
                blocks[index] = Block.Log;
                index += oneY;
            }
        }
		
		        static int Floor(float value) {
            int valueI = (int)value;
            return value < valueI ? valueI - 1 : valueI;
        }
        
        void FillOblateSpheroid(int x, int y, int z, float radius, byte block) {
            int xBeg = Floor(Math.Max(x - radius, 0));
            int xEnd = Floor(Math.Min(x + radius, Width - 1));
            int yBeg = Floor(Math.Max(y - radius, 0));
            int yEnd = Floor(Math.Min(y + radius, Height - 1));
            int zBeg = Floor(Math.Max(z - radius, 0));
            int zEnd = Floor(Math.Min(z + radius, Length - 1));
            float radiusSq = radius * radius;
            
            for (int yy = yBeg; yy <= yEnd; yy++)
                for (int zz = zBeg; zz <= zEnd; zz++)
                    for (int xx = xBeg; xx <= xEnd; xx++)
            {
                int dx = xx - x, dy = yy - y, dz = zz - z;
                if ((dx * dx + 2 * dy * dy + dz * dz) < radiusSq) {
                    int index = (yy * Length + zz) * Width + xx;
                    if (blocks[index] == Block.Stone)
                        blocks[index] = block;
                }
            }
        }
        
        void FloodFill(int startIndex, byte block) {
            if (startIndex < 0) return; // y below map, immediately ignore
            FastIntStack stack = new FastIntStack(4);
            stack.Push(startIndex);    
            
            while (stack.Size > 0) {
                int index = stack.Pop();
                if (blocks[index] != Block.Air) continue;
                blocks[index] = block;
                
                int x = index % Width;
                int y = index / oneY;
                int z = (index / Width) % Length;
                
                if (x > 0) stack.Push(index - 1);
                if (x < Width - 1) stack.Push(index + 1);
                if (z > 0) stack.Push(index - Width);
                if (z < Length - 1) stack.Push(index + Width);
                if (y > 0) stack.Push(index - oneY);
            }
        }
        
        sealed class FastIntStack {
            public int[] Values;
            public int Size;
            
            public FastIntStack(int capacity) {
                Values = new int[capacity];
                Size = 0;
            }
            
            public int Pop() {
                return Values[--Size];
            }
            
            public void Push(int item) {
                if (Size == Values.Length) {
                    int[] array = new int[Values.Length * 2];
                    Buffer.BlockCopy(Values, 0, array, 0, Size * sizeof(int));
                    Values = array;
                }
                Values[Size++] = item;
            }
        }
    }
	
	public sealed class OctaveNoise {
        
        readonly ImprovedNoise[] baseNoise;
        public OctaveNoise(int octaves, JavaRandom rnd) {
            baseNoise = new ImprovedNoise[octaves];
            for (int i = 0; i < octaves; i++)
                baseNoise[i] = new ImprovedNoise(rnd);
        }
        
        public double Compute(double x, double y) {
            double amplitude = 1;
            double sum = 0;
            for (int i = 0; i < baseNoise.Length; i++) {
                sum += baseNoise[i].Compute(x / amplitude, y / amplitude) * amplitude;
                amplitude *= 2.0;
            }
            return sum;
        }
    }
	
	    
    public sealed class CombinedNoise {
        
        readonly OctaveNoise noise1, noise2;
        public CombinedNoise(OctaveNoise noise1, OctaveNoise noise2) {
            this.noise1 = noise1;
            this.noise2 = noise2;
        }
        
        public double Compute(double x, double y) {
            double offset = noise2.Compute(x, y);
            return noise1.Compute(x + offset, y);
        }
    }
	
	    public sealed class ImprovedNoise {
        
        public ImprovedNoise(JavaRandom rnd) {
            // shuffle randomly using fisher-yates
            for (int i = 0; i < 256; i++)
                p[i] = (byte)i;
            
            for (int i = 0; i < 256; i++) {
                int j = rnd.Next(i, 256);
                byte temp = p[i]; p[i] = p[j]; p[j] = temp;
            }
            for (int i = 0; i < 256; i++)
                p[i + 256] = p[i];
        }
        
        public double Compute(double x, double y) {
            int xFloor = x >= 0 ? (int)x : (int)x - 1;
            int yFloor = y >= 0 ? (int)y : (int)y - 1;
            int X = xFloor & 0xFF, Y = yFloor & 0xFF;
            x -= xFloor; y -= yFloor;
            
            double u = x * x * x * (x * (x * 6 - 15) + 10); // Fade(x)
            double v = y * y * y * (y * (y * 6 - 15) + 10); // Fade(y)
            int A = p[X] + Y, B = p[X + 1] + Y;
            
            // Normally, calculating Grad involves a function call. However, we can directly pack this table
            // (since each value indicates either -1, 0 1) into a set of bit flags. This way we avoid needing 
            // to call another function that performs branching
            const int xFlags = 0x46552222, yFlags = 0x2222550A;
            
            int hash = (p[p[A]] & 0xF) << 1; 
            double g22 = (((xFlags >> hash) & 3) - 1) * x + (((yFlags >> hash) & 3) - 1) * y; // Grad(p[p[A], x, y)
            hash = (p[p[B]] & 0xF) << 1; 
            double g12 = (((xFlags >> hash) & 3) - 1) * (x - 1) + (((yFlags >> hash) & 3) - 1) * y; // Grad(p[p[B], x - 1, y)
            double c1 = g22 + u * (g12 - g22);
            
            hash = (p[p[A + 1]] & 0xF) << 1; 
            double g21 = (((xFlags >> hash) & 3) - 1) * x + (((yFlags >> hash) & 3) - 1) * (y - 1); // Grad(p[p[A + 1], x, y - 1)
            hash = (p[p[B + 1]] & 0xF) << 1; 
            double g11 = (((xFlags >> hash) & 3) - 1) * (x - 1) + (((yFlags >> hash) & 3) - 1) * (y - 1); // Grad(p[p[B + 1], x - 1, y - 1)
            double c2 = g21 + u * (g11 - g21);
            
            return c1 + v * (c2 - c1);
        }
        
        byte[] p = new byte[512];
    }
}
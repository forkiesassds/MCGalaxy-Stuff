using System;
using System.Collections.Generic;
using System.IO;
using fNbt;
using MCGalaxy;
using MCGalaxy.Levels.IO;
using MCGalaxy.Maths;
using BlockID = System.UInt16;

namespace VeryPlugins
{
    public sealed class SchemImporter : IMapImporter
    {
        public override string Extension { get { return ".schem"; } }
        public override string Description { get { return "Sponge Specification Schematic"; } }

        public override Vec3U16 ReadDimensions(Stream src)
        {
            throw new NotSupportedException();
        }

        public override Level Read(Stream src, string name, bool metadata)
        {
            NbtFile file = new NbtFile();
            file.LoadFromStream(src);

            if (!file.RootTag.Contains("Schematic")) return null;

            Level lvl;
            ReadData((NbtCompound)file.RootTag["Schematic"], name, out lvl);

            return lvl;
        }

        static void ReadData(NbtCompound root, string name, out Level lvl)
        {
            ushort width  = (ushort)root["Width"].ShortValue;
            ushort height = (ushort)root["Height"].ShortValue;
            ushort length = (ushort)root["Length"].ShortValue;

            lvl = new Level(name, width, height, length);
            ReadBlocks((NbtCompound)root["Blocks"], width, height, length, ref lvl);
        }

        static void ReadBlocks(NbtCompound bData, ushort width, ushort height, ushort length, ref Level lvl)
        {
            Dictionary<int, BlockID> palette = new Dictionary<int, BlockID>();
            ReadPalette((NbtCompound)bData["Palette"], ref palette);

            byte[] data = bData["Data"].ByteArrayValue;
            int index = 0;
            for (ushort y = 0; y < height; y++)
                for (ushort z = 0; z < length; z++)
                    for (ushort x = 0; x < width; x++)
            {
                int pEntry = ReadVarIntFromArray(data, ref index);
                lvl.SetBlock(x, y, z, palette[pEntry]);
            }
        }

        static void ReadPalette(NbtCompound pData, ref Dictionary<int, BlockID> palette)
        {
            foreach (NbtTag tag in pData)
            {
                string state = tag.Name;
                int index = tag.IntValue;

                if (state.Split(':', 2).Length == 1)
                    state = "minecraft:" + state;

                BlockID value;
                //TODO: should probably just parse the blockstate instead of it being a string based lookup
                if (PaletteConfig.BlockstateToIDDict.TryGetValue(state, out value))
                    palette[index] = value;
                else
                {
                    //TODO: make this make new blockdefs if option is enabled.
                    Console.WriteLine("detected unknown blockstate " + state + "... replacing with bedrock");
                    palette[index] = 7;
                }
            }
        }

        private const int SEGMENT_BITS = 0x7F;
        private const int CONTINUE_BIT = 0x80;

        public static int ReadVarIntFromArray(byte[] array, ref int index) 
        {
            int value = 0;
            int position = 0;
            byte currentByte;

            while (true) 
            {
                currentByte = array[index++];
                value |= (currentByte & SEGMENT_BITS) << position;

                if ((currentByte & CONTINUE_BIT) == 0) break;

                position += 7;

                if (position >= 32) throw new Exception("VarInt is too big");
            }

            return value;
        }
    }
}
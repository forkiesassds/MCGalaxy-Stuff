using MCGalaxy;
using MCGalaxy.Levels.IO;

namespace VeryPlugins
{
    public class SchemImporterPlugin : Plugin
    {
        public override string name { get { return "SchemImporterPlugin"; } }

        readonly IMapImporter format = new SchemImporter();

        public override void Load(bool auto)
        {
            IMapImporter.Formats.Add(format);
        }

        public override void Unload(bool auto)
        {
            IMapImporter.Formats.Remove(format);
        }
    }
}
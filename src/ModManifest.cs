using System.Collections.Generic;

namespace Modding
{
    public class ModManifest
    {
        public string Name { get; set; }
        public string Author { get; set; }
        public string Version { get; set; }
        public string Url { get; set; }
        public string UniqueIdentifier { get; set; }
        public string PathToDLL { get; set; }
        public string[] DialoguePaths { get; set; }
        public string[] CustomEquipment { get; set; }
        public string ModPath { get; set; }
        public ModSpriteResolver SpriteResolver { get; set; }

        /// <summary>Controls whether the loader will load this mod. Defaults to true when omitted.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Dependency map, package.json-style: UniqueIdentifier of the required mod -> minimum version (optional, may be null/empty).</summary>
        public Dictionary<string, string> Requires { get; set; }

        /// <summary>Soft ordering hint: UniqueIdentifiers this mod should load after, if present and enabled. Missing or disabled entries are ignored silently and never fail the load.</summary>
        public string[] LoadAfter { get; set; }
    }
}

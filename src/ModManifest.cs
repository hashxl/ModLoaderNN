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
    }
}

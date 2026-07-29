using System.IO;
using System.Reflection;

namespace TCModLoader
{
    internal static class StandalonePaths
    {
        internal static readonly string RuntimeDirectory =
            Path.GetDirectoryName(typeof(StandalonePaths).Assembly.Location);

        internal static readonly string LoaderDirectory =
            Directory.GetParent(RuntimeDirectory).FullName;

        internal static readonly string GameDirectory =
            Directory.GetParent(LoaderDirectory).FullName;

        internal static readonly string ManagedDirectory =
            Path.Combine(GameDirectory, "Third Crisis Neon Nights_Data", "Managed");

        internal static readonly string ModsDirectory =
            Path.Combine(GameDirectory, "Mods");

        internal static readonly string CacheDirectory =
            Path.Combine(LoaderDirectory, "Cache");

        internal static readonly string LogFile =
            Path.Combine(LoaderDirectory, "Logs", "TCModLoader.log");
    }
}

using System;
using System.IO;
using System.Text;

namespace TCModLoader
{
    internal sealed class LoaderLog
    {
        private readonly object _sync = new object();
        private readonly string _path;

        internal LoaderLog(string path)
        {
            _path = path;
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            lock (_sync)
            {
                File.WriteAllText(_path,
                    $"TCModLoader standalone log - {DateTime.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}",
                    new UTF8Encoding(false));
            }
        }

        internal void LogInfo(object message) => Write("INFO", message);
        internal void LogWarning(object message) => Write("WARN", message);
        internal void LogError(object message) => Write("ERROR", message);

        private void Write(string level, object message)
        {
            var line = $"[{DateTime.Now:HH:mm:ss.fff}] [{level}] {message}{Environment.NewLine}";

            lock (_sync)
            {
                try
                {
                    File.AppendAllText(_path, line, new UTF8Encoding(false));
                }
                catch
                {
                    // Logging must never prevent the game from starting.
                }
            }
        }
    }
}

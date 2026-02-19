using System.IO;

namespace ClaudeUsageWin.Services;

public static class Logger
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ClaudeUsageWin", "logs", "debug.log");

    private static readonly object _lock = new();

    public static void Log(string message) => Write("INF", message);

    public static void LogError(string message, Exception? ex = null)
        => Write("ERR", ex is null ? message : $"{message}: {ex.GetType().Name}: {ex.Message}");

    private static void Write(string level, string message)
    {
        try
        {
            lock (_lock)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);

                // Roll log at 2 MB
                var fi = new FileInfo(LogPath);
                if (fi.Exists && fi.Length > 2 * 1024 * 1024)
                    File.Move(LogPath, LogPath + ".old", overwrite: true);

                File.AppendAllText(LogPath,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}\n");
            }
        }
        catch { }
    }

    public static string LogsFolder => Path.GetDirectoryName(LogPath)!;
}

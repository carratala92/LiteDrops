namespace LiteDrops;

/// <summary>Tiny append-only log file next to the exe, rotated at 512 KB.</summary>
public static class Log
{
    static readonly string FilePath = Path.Combine(AppContext.BaseDirectory, "litedrops.log");
    static readonly Lock Sync = new();
    const long MaxSize = 512 * 1024;

    public static event Action<string>? Written;

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);
    public static void Error(string message) => Write("ERROR", message);

    static void Write(string level, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {level,-5} {message}";
        lock (Sync)
        {
            try
            {
                var info = new FileInfo(FilePath);
                if (info.Exists && info.Length > MaxSize)
                    File.Move(FilePath, FilePath + ".old", overwrite: true);
                File.AppendAllText(FilePath, line + Environment.NewLine);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        Written?.Invoke(line);
    }
}

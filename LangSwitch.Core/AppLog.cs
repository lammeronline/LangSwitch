namespace LangSwitch;

public static class AppLog
{
    static readonly string LogPath = Path.Combine(AppContext.BaseDirectory, "LangSwitch.log");

    static readonly object Lock = new();

    public static void Info(string msg)
    {
        try
        {
            lock (Lock)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss.fff}] INFO  {msg}\n");
            }
        }
        catch { }
    }

    public static void Error(Exception ex)
    {
        try
        {
            lock (Lock)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\n\n");
            }
        }
        catch { }
    }
}

namespace LangSwitch;

static class AppLog
{
    static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "LangSwitch", "LangSwitch.log");

    static readonly object Lock = new();

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

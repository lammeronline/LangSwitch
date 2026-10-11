using System.Text.Json;

namespace LangSwitch.Core;

public sealed class Stats
{
    public int WordsCorrected    { get; set; }
    public int CharactersFixed   { get; set; }
    public int ManualConversions { get; set; }
}

public static class StatsStore
{
    static readonly string FilePath = Path.Combine(AppContext.BaseDirectory, "stats.json");

    static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static Stats Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new Stats();
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<Stats>(json, JsonOptions) ?? new Stats();
        }
        catch (Exception ex)
        {
            AppLog.Error(ex);
            return new Stats();
        }
    }

    public static void Save(Stats stats)
    {
        try
        {
            File.WriteAllText(FilePath, JsonSerializer.Serialize(stats, JsonOptions));
        }
        catch (Exception ex)
        {
            AppLog.Error(ex);
        }
    }
}

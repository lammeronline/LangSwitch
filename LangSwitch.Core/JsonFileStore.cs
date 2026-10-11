using System.Text.Json;

namespace LangSwitch.Core;

/// <summary>
/// Shared load/save skeleton for the app's various "store" classes (settings, dictionaries,
/// abbreviations, stats, undo-learning counts) — each one is just a JSON file next to the
/// executable with the same read-or-fallback / write-and-log-on-failure behavior, so there's
/// no reason for every store to hand-roll its own copy of this.
/// </summary>
public static class JsonFileStore
{
    static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static T Load<T>(string fileName, Func<T> fallback)
    {
        var path = Path.Combine(AppContext.BaseDirectory, fileName);
        try
        {
            if (!File.Exists(path)) return fallback();
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<T>(json, Options) ?? fallback();
        }
        catch (Exception ex)
        {
            AppLog.Error(ex);
            return fallback();
        }
    }

    public static void Save<T>(string fileName, T value)
    {
        var path = Path.Combine(AppContext.BaseDirectory, fileName);
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(value, Options));
        }
        catch (Exception ex)
        {
            AppLog.Error(ex);
        }
    }
}

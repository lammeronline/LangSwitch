using System.Text.Json;

namespace LangSwitch.Settings;

static class SettingsStore
{
    static readonly string FilePath = Path.Combine(AppContext.BaseDirectory, "settings.json");

    static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new AppSettings();
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch (Exception ex)
        {
            AppLog.Error(ex);
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, JsonOptions));
        }
        catch (Exception ex)
        {
            AppLog.Error(ex);
        }
    }
}

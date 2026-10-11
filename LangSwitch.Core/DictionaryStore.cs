using System.Text.Json;

namespace LangSwitch.Core;

public static class DictionaryStore
{
    static readonly string FilePath = Path.Combine(AppContext.BaseDirectory, "dictionaries.json");

    static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static List<UserDictionary> Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return Defaults();
            var json = File.ReadAllText(FilePath);
            var list = JsonSerializer.Deserialize<List<UserDictionary>>(json, JsonOptions) ?? [];
            return list.Count > 0 ? list : Defaults();
        }
        catch (Exception ex)
        {
            AppLog.Error(ex);
            return Defaults();
        }
    }

    public static void Save(List<UserDictionary> dicts)
    {
        try
        {
            File.WriteAllText(FilePath, JsonSerializer.Serialize(dicts, JsonOptions));
        }
        catch (Exception ex)
        {
            AppLog.Error(ex);
        }
    }

    static List<UserDictionary> Defaults() =>
    [
        new() { Name = "English",   Lang = "en", Enabled = true, Url = WordListDownloader.UrlFor("en") },
        new() { Name = "Russian",   Lang = "ru", Enabled = true, Url = WordListDownloader.UrlFor("ru") },
        new() { Name = "Ukrainian", Lang = "uk", Enabled = true, Url = WordListDownloader.UrlFor("uk") },
    ];
}

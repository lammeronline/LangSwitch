using System.Text.Json;

namespace LangSwitch.Autocorrect;

static class DictionaryStore
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
        catch { return Defaults(); }
    }

    public static void Save(List<UserDictionary> dicts)
    {
        try
        {
            File.WriteAllText(FilePath, JsonSerializer.Serialize(dicts, JsonOptions));
        }
        catch { }
    }

    static List<UserDictionary> Defaults() =>
    [
        new() { Name = "English",   Lang = "en", Enabled = true, Url = WordListDownloader.UrlFor("en") },
        new() { Name = "Russian",   Lang = "ru", Enabled = true, Url = WordListDownloader.UrlFor("ru") },
        new() { Name = "Ukrainian", Lang = "uk", Enabled = true, Url = WordListDownloader.UrlFor("uk") },
    ];
}

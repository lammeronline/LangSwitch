using System.Text.Json;

namespace LangSwitch.Core;

public static class DictionaryStore
{
    static readonly string FilePath = Path.Combine(AppContext.BaseDirectory, "dictionaries.json");

    static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <param name="seedLangs">
    /// Language codes to seed a default dictionary entry for when no
    /// dictionaries.json exists yet — normally the user's installed keyboard layouts,
    /// so a fresh install self-configures around whatever languages are actually in use.
    /// </param>
    public static List<UserDictionary> Load(IEnumerable<string> seedLangs)
    {
        try
        {
            if (!File.Exists(FilePath)) return Defaults(seedLangs);
            var json = File.ReadAllText(FilePath);
            var list = JsonSerializer.Deserialize<List<UserDictionary>>(json, JsonOptions) ?? [];
            return list.Count > 0 ? list : Defaults(seedLangs);
        }
        catch (Exception ex)
        {
            AppLog.Error(ex);
            return Defaults(seedLangs);
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

    static List<UserDictionary> Defaults(IEnumerable<string> seedLangs)
    {
        var langs = seedLangs.Select(l => l.ToLowerInvariant()).Distinct().ToList();
        if (langs.Count == 0) langs = ["en", "ru", "uk"]; // fallback if layout detection ever yields nothing

        return langs.Select(lang => new UserDictionary
        {
            Name    = CultureDisplayName(lang),
            Lang    = lang,
            Enabled = true,
            Url     = WordListDownloader.UrlFor(lang),
        }).ToList();
    }

    static string CultureDisplayName(string lang)
    {
        try { return new System.Globalization.CultureInfo(lang).EnglishName; }
        catch { return lang.ToUpperInvariant(); }
    }
}

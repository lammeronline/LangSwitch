namespace LangSwitch.Core;

public static class DictionaryStore
{
    const string FileName = "dictionaries.json";

    /// <param name="seedLangs">
    /// Language codes to seed a default dictionary entry for when no
    /// dictionaries.json exists yet — normally the user's installed keyboard layouts,
    /// so a fresh install self-configures around whatever languages are actually in use.
    /// </param>
    public static List<UserDictionary> Load(IEnumerable<string> seedLangs)
    {
        var list = JsonFileStore.Load<List<UserDictionary>>(FileName, () => []);
        if (list.Count == 0) return Defaults(seedLangs);

        RefreshStaleDefaultUrls(list);
        return list;
    }

    public static void Save(List<UserDictionary> dicts) => JsonFileStore.Save(FileName, dicts);

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

    /// <summary>
    /// A dictionary's Url is set to WordListDownloader.UrlFor(lang) when the entry is first
    /// created, then never touched again — so an older save on disk can carry a Url pointing
    /// at a source this version no longer prefers (e.g. hermitdave before the Hunspell
    /// migration). Re-resolve it on every load so Settings always shows, and downloads from,
    /// the current default — but only for URLs that are themselves one of our own defaults,
    /// never for a URL the user deliberately typed in to point at a different source.
    /// </summary>
    static void RefreshStaleDefaultUrls(List<UserDictionary> dicts)
    {
        var changed = false;
        foreach (var dict in dicts)
        {
            if (string.IsNullOrWhiteSpace(dict.Lang) || string.IsNullOrEmpty(dict.Url)) continue;
            if (!WordListDownloader.IsKnownDefaultUrl(dict.Url)) continue;

            var current = WordListDownloader.UrlFor(dict.Lang.Trim().ToLowerInvariant());
            if (dict.Url != current)
            {
                dict.Url = current;
                changed = true;
            }
        }
        if (changed) Save(dicts);
    }

    static string CultureDisplayName(string lang) =>
        CultureHelper.TryParse(lang)?.EnglishName ?? lang.ToUpperInvariant();
}

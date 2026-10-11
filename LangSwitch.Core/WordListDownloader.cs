namespace LangSwitch.Core;

public static class WordListDownloader
{
    static readonly HttpClient Http = new();

    // wooorm/dictionaries packages Hunspell dictionaries (stem list + affix rules) — recognizes
    // inflected forms (declensions, conjugations) a flat list would miss. But it only covers
    // ~60 languages, mostly European (plus many regional variants like es-AR/de-AT inflating
    // the headline count) — no Thai/Bengali/Malayalam/Arabic/Hindi/Chinese/Japanese/Indonesian.
    // For anything it doesn't have, fall back to hermitdave/FrequencyWords (flat top-50k list,
    // ~60 languages, different and wider non-European coverage).
    static readonly Dictionary<string, string> KnownYears = new()
    {
        ["en"] = "2016", ["ru"] = "2016", ["uk"] = "2016",
    };

    public static string UrlFor(string lang) =>
        $"https://raw.githubusercontent.com/wooorm/dictionaries/main/dictionaries/{lang}/index.dic";

    static string HermitdaveUrlFor(string lang)
    {
        var year = KnownYears.GetValueOrDefault(lang, "2018");
        return $"https://raw.githubusercontent.com/hermitdave/FrequencyWords/master/content/{year}/{lang}/{lang}_50k.txt";
    }

    public static Task<int> DownloadAsync(string lang, IProgress<string>? progress = null) =>
        DownloadAsync(lang, UrlFor(lang), progress);

    public static async Task<int> DownloadAsync(string lang, string dicUrl, IProgress<string>? progress = null)
    {
        var affUrl = dicUrl.EndsWith(".dic", StringComparison.OrdinalIgnoreCase)
            ? dicUrl[..^4] + ".aff"
            : dicUrl + ".aff";

        progress?.Report($"Downloading {lang.ToUpperInvariant()} dictionary…");
        try
        {
            var dicBytes = await Http.GetByteArrayAsync(dicUrl);
            var affBytes = await Http.GetByteArrayAsync(affUrl);

            Directory.CreateDirectory(LanguageDictionaries.WordListDir);
            await File.WriteAllBytesAsync(Path.Combine(LanguageDictionaries.WordListDir, lang + ".dic"), dicBytes);
            await File.WriteAllBytesAsync(Path.Combine(LanguageDictionaries.WordListDir, lang + ".aff"), affBytes);

            return LanguageDictionaries.ReadStemCount(dicBytes);
        }
        catch (HttpRequestException)
        {
            // No Hunspell dictionary for this language — fall back to a flat frequency list.
            progress?.Report($"No Hunspell dictionary for {lang.ToUpperInvariant()}, trying word-frequency list…");
            return await DownloadFrequencyListAsync(lang, HermitdaveUrlFor(lang), progress);
        }
    }

    static async Task<int> DownloadFrequencyListAsync(string lang, string url, IProgress<string>? progress)
    {
        var raw = await Http.GetStringAsync(url);

        progress?.Report("Parsing…");
        var words = ParseWords(raw);

        Directory.CreateDirectory(LanguageDictionaries.WordListDir);
        var path = Path.Combine(LanguageDictionaries.WordListDir, lang + ".txt");
        await File.WriteAllLinesAsync(path, words);

        return words.Count;
    }

    static List<string> ParseWords(string raw)
    {
        var result = new List<string>(50_000);
        foreach (var line in raw.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0) continue;
            var word = trimmed.Split(' ', 2)[0];
            if (word.Length > 0 && word.All(TextNormalization.IsWordChar))
                result.Add(word.ToLowerInvariant());
        }
        return result;
    }
}

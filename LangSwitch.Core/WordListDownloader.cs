namespace LangSwitch.Core;

public static class WordListDownloader
{
    static readonly HttpClient Http = new();

    // Same source (hermitdave/FrequencyWords) works for ~60 languages under this path
    // pattern; en/ru/uk are pinned to a verified year, everything else uses the common
    // "2018" layout. Best-effort — the URL field in Settings → Dictionaries is freely
    // editable if a language's actual path doesn't match.
    static readonly Dictionary<string, string> KnownYears = new()
    {
        ["en"] = "2016", ["ru"] = "2016", ["uk"] = "2016",
    };

    public static string UrlFor(string lang)
    {
        var year = KnownYears.GetValueOrDefault(lang, "2018");
        return $"https://raw.githubusercontent.com/hermitdave/FrequencyWords/master/content/{year}/{lang}/{lang}_50k.txt";
    }

    public static Task<int> DownloadAsync(string lang, IProgress<string>? progress = null) =>
        DownloadAsync(lang, UrlFor(lang), progress);

    public static async Task<int> DownloadAsync(string lang, string url, IProgress<string>? progress = null)
    {
        progress?.Report($"Downloading {lang.ToUpperInvariant()} word list…");
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

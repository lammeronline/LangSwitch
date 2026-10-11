namespace LangSwitch.Core;

public static class WordListDownloader
{
    static readonly HttpClient Http = new();

    static readonly Dictionary<string, string> Sources = new()
    {
        ["en"] = "https://raw.githubusercontent.com/hermitdave/FrequencyWords/master/content/2016/en/en_50k.txt",
        ["ru"] = "https://raw.githubusercontent.com/hermitdave/FrequencyWords/master/content/2016/ru/ru_50k.txt",
        ["uk"] = "https://raw.githubusercontent.com/hermitdave/FrequencyWords/master/content/2016/uk/uk_50k.txt",
    };

    public static string UrlFor(string lang) =>
        Sources.TryGetValue(lang, out var url) ? url : "";

    public static Task<int> DownloadAsync(string lang, IProgress<string>? progress = null) =>
        Sources.TryGetValue(lang, out var url)
            ? DownloadAsync(lang, url, progress)
            : throw new ArgumentException($"Unknown language: {lang}");

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
            if (word.Length > 0 && word.All(char.IsLetter))
                result.Add(word.ToLowerInvariant());
        }
        return result;
    }
}

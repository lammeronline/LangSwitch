namespace LangSwitch.Core;

/// <summary>
/// Holds per-language word sets used to decide which layout a typed word belongs to.
/// Word lists are stored in %AppData%\LangSwitch\WordLists\{lang}.txt (one word per line).
/// </summary>
sealed class LanguageDictionaries
{
    public static readonly string WordListDir = Path.Combine(AppContext.BaseDirectory, "WordLists");

    Dictionary<string, HashSet<string>> _sets = new(StringComparer.OrdinalIgnoreCase);

    public LanguageDictionaries() => Reload();

    public void Reload()
    {
        var sets = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        LoadLang(sets, "en", FallbackEn);
        LoadLang(sets, "ru", FallbackRu);
        LoadLang(sets, "uk", FallbackUk);
        _sets = sets;
    }

    public bool Contains(string lang, string word) =>
        _sets.TryGetValue(lang, out var set) && set.Contains(Normalize(word));

    public bool ContainsAny(string word)
    {
        var n = Normalize(word);
        return n.Length > 0 && _sets.Values.Any(s => s.Contains(n));
    }

    public int WordCount(string lang) =>
        _sets.TryGetValue(lang, out var s) ? s.Count : 0;

    /// <summary>
    /// From a set of layout-converted candidates, picks the one that scores best
    /// in the word lists. Returns original if no candidate scores higher.
    /// </summary>
    public string ChooseBest(string original, IEnumerable<(string Word, string Lang)> candidates)
    {
        int origScore = Score(original);
        var best = candidates
            .Select(c => (c.Word, c.Lang, Score: Score(c.Word)))
            .OrderByDescending(c => c.Score)
            .FirstOrDefault();

        return best.Score > origScore ? best.Word : original;
    }

    int Score(string word) => ContainsAny(word) ? 10 : 0;

    static void LoadLang(Dictionary<string, HashSet<string>> sets, string lang, IEnumerable<string> fallback)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var path = Path.Combine(WordListDir, lang + ".txt");

        if (File.Exists(path))
        {
            foreach (var line in File.ReadLines(path))
            {
                var w = Normalize(line.Split(' ')[0]); // handle "word count" format
                if (w.Length > 0) set.Add(w);
            }
        }

        foreach (var w in fallback)
        {
            var n = Normalize(w);
            if (n.Length > 0) set.Add(n);
        }

        sets[lang] = set;
    }

    static string Normalize(string w) =>
        new(w.Trim().ToLowerInvariant().Where(char.IsLetter).ToArray());

    // ── Fallback word lists (loaded even without downloaded files) ───────────

    static readonly string[] FallbackEn =
    [
        "hello", "world", "the", "and", "you", "yes", "no", "test", "text",
        "language", "keyboard", "layout", "today", "work", "project", "file",
        "please", "thanks", "sorry", "when", "what", "where", "how", "this",
        "that", "from", "with", "have", "will", "just", "not", "but", "for",
    ];

    static readonly string[] FallbackRu =
    [
        "привет", "мир", "как", "дела", "нет", "да", "я", "ты", "он", "она",
        "мы", "вы", "на", "что", "когда", "спасибо", "пожалуйста", "сейчас",
        "работа", "проект", "файл", "текст", "язык", "клавиатура", "привет",
    ];

    static readonly string[] FallbackUk =
    [
        "привіт", "світ", "як", "справи", "ні", "так", "я", "ти", "він", "вона",
        "ми", "ви", "на", "що", "коли", "дякую", "будь-ласка", "зараз",
        "робота", "проект", "файл", "текст", "мова", "клавіатура",
    ];
}

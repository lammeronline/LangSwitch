using System.Text;
using WeCantSpell.Hunspell;

namespace LangSwitch.Core;

/// <summary>
/// Holds per-language word knowledge used to decide which layout a typed word belongs to.
/// Languages loaded are driven entirely by the enabled <see cref="UserDictionary"/> entries
/// passed in (from <see cref="DictionaryStore"/>) — there is no hardcoded language list.
///
/// Backed by Hunspell dictionaries (%BaseDir%/WordLists/{lang}.dic + .aff) when downloaded —
/// a Hunspell stem list plus affix rules recognizes inflected forms (declensions,
/// conjugations) that a flat word list would miss. Languages without a downloaded dictionary
/// fall back to a tiny hardcoded word set (en/ru/uk only) so basic autocorrect still works
/// before the user downloads anything.
/// </summary>
public sealed class LanguageDictionaries
{
    public static readonly string WordListDir = Path.Combine(AppContext.BaseDirectory, "WordLists");

    Dictionary<string, WordList> _hunspell = new(StringComparer.OrdinalIgnoreCase);
    Dictionary<string, HashSet<string>> _fallback = new(StringComparer.OrdinalIgnoreCase);
    Dictionary<string, int> _stemCounts = new(StringComparer.OrdinalIgnoreCase);

    public LanguageDictionaries(IReadOnlyList<UserDictionary> dicts) => Reload(dicts);

    public void Reload(IReadOnlyList<UserDictionary> dicts)
    {
        var hunspell = new Dictionary<string, WordList>(StringComparer.OrdinalIgnoreCase);
        var fallback = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var stemCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var dict in dicts)
        {
            if (!dict.Enabled || string.IsNullOrWhiteSpace(dict.Lang)) continue;
            var lang = dict.Lang.Trim().ToLowerInvariant();

            var dicPath = Path.Combine(WordListDir, lang + ".dic");
            var affPath = Path.Combine(WordListDir, lang + ".aff");
            var flatPath = Path.Combine(WordListDir, lang + ".txt");

            if (File.Exists(dicPath) && File.Exists(affPath))
            {
                try
                {
                    var dicBytes = File.ReadAllBytes(dicPath);
                    using var dicStream = new MemoryStream(dicBytes);
                    using var affStream = File.OpenRead(affPath);
                    hunspell[lang] = WordList.CreateFromStreams(dicStream, affStream);
                    stemCounts[lang] = ReadStemCount(dicBytes);
                    continue;
                }
                catch (Exception ex)
                {
                    AppLog.Info($"Failed to load Hunspell dictionary for '{lang}': {ex.Message}");
                }
            }

            // No Hunspell dictionary (language not covered by the source, or load failed) —
            // fall back to a flat word list (downloaded frequency list, or a tiny built-in set).
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (File.Exists(flatPath))
            {
                foreach (var line in File.ReadLines(flatPath))
                {
                    var w = Normalize(line.Split(' ')[0]); // handle "word count" format
                    if (w.Length > 0) set.Add(w);
                }
            }
            else
            {
                foreach (var w in Fallbacks.GetValueOrDefault(lang, []))
                {
                    var n = Normalize(w);
                    if (n.Length > 0) set.Add(n);
                }
            }
            fallback[lang] = set;
            stemCounts[lang] = set.Count;
        }

        _hunspell = hunspell;
        _fallback = fallback;
        _stemCounts = stemCounts;
    }

    public bool Contains(string lang, string word) =>
        _hunspell.TryGetValue(lang, out var wl)
            ? wl.Check(word) || wl.Check(word.ToLowerInvariant())
            : _fallback.TryGetValue(lang, out var set) && set.Contains(Normalize(word));

    public bool ContainsAny(string word)
    {
        foreach (var lang in Languages())
            if (Contains(lang, word)) return true;
        return false;
    }

    public int WordCount(string lang) => _stemCounts.GetValueOrDefault(lang, 0);

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

    IEnumerable<string> Languages() => _hunspell.Keys.Concat(_fallback.Keys);

    static string Normalize(string w) =>
        new(w.Trim().ToLowerInvariant().Where(TextNormalization.IsWordChar).ToArray());

    /// <summary>
    /// A Hunspell .dic file starts with a line holding the approximate stem count
    /// (not an exact distinct-word count — affix expansion adds many more inflected
    /// forms per stem). Good enough for the "X words loaded" status label.
    /// </summary>
    internal static int ReadStemCount(byte[] dicBytes)
    {
        using var reader = new StreamReader(new MemoryStream(dicBytes), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var firstLine = reader.ReadLine();
        return int.TryParse(firstLine?.Trim(), out var count) ? count : 0;
    }

    // ── Fallback word lists (loaded even without downloaded dictionaries, en/ru/uk only) ──
    // Note: Fallbacks must be declared after the arrays it references — static field
    // initializers run in textual declaration order, so an earlier position here would
    // capture null for each array.

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

    static readonly IReadOnlyDictionary<string, string[]> Fallbacks = new Dictionary<string, string[]>
    {
        ["en"] = FallbackEn,
        ["ru"] = FallbackRu,
        ["uk"] = FallbackUk,
    };
}

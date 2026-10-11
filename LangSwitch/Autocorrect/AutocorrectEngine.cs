using LangSwitch.Core;

namespace LangSwitch.Autocorrect;

sealed class AutocorrectEngine
{
    readonly ITextInjector _injector;
    readonly ILayoutCandidateProvider _candidates;
    LanguageDictionaries _langDicts;
    IReadOnlyList<Abbreviation> _abbreviations;
    bool _fixCapsLock;
    HashSet<string> _neverCorrect;

    public AutocorrectEngine(
        LanguageDictionaries langDicts,
        ITextInjector injector,
        ILayoutCandidateProvider candidates,
        IReadOnlyList<Abbreviation> abbreviations,
        bool fixCapsLock,
        HashSet<string>? neverCorrect = null)
    {
        _langDicts      = langDicts;
        _injector       = injector;
        _candidates     = candidates;
        _abbreviations  = abbreviations;
        _fixCapsLock    = fixCapsLock;
        _neverCorrect   = new HashSet<string>(neverCorrect ?? [], StringComparer.OrdinalIgnoreCase);
    }

    public void ReloadWordLists(LanguageDictionaries l) => _langDicts = l;
    public void ReloadAbbreviations(IReadOnlyList<Abbreviation> a) => _abbreviations = a;
    public void SetFixCapsLock(bool enabled) => _fixCapsLock = enabled;

    /// <summary>Words that have been undone enough times that we stop touching them entirely.</summary>
    public void ReloadNeverCorrect(IEnumerable<string> words) =>
        _neverCorrect = new HashSet<string>(words, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Tries, in priority order, to expand a known abbreviation, fix an accidental
    /// Caps-Lock inversion, or correct a wrong-layout word. Returns true and injects the
    /// replacement if any of those applied; <paramref name="replacement"/> carries what was
    /// typed in, so the caller can offer an undo.
    /// </summary>
    public bool TryReplace(WordReadyEventArgs e, out string? replacement)
    {
        replacement = null;
        var word = e.Word;

        if (_neverCorrect.Contains(word)) return false;

        if (AbbreviationExpander.TryExpand(word, _abbreviations) is { } expansion)
            replacement = PreserveCase(word, expansion);
        else if (_fixCapsLock && CapsLockCorrector.TryFix(word, _langDicts) is { } capsFixed)
            replacement = capsFixed; // already correctly cased
        else if (LayoutAutoCorrector.TryCorrect(word, _candidates.GetAllCandidates(word), _langDicts) is { } layoutFixed)
            replacement = PreserveCase(word, layoutFixed);

        if (replacement is null) return false;

        e.HookArgs.Handled = true;
        _injector.Replace(word.Length, replacement + e.Boundary);
        return true;
    }

    internal static string PreserveCase(string typed, string replacement)
    {
        if (replacement.Length == 0) return replacement;

        if (typed.Length > 1 && typed == typed.ToUpperInvariant() && typed != typed.ToLowerInvariant())
            return replacement.ToUpperInvariant();

        if (char.IsUpper(typed[0]) && char.IsLower(replacement[0]))
            return char.ToUpperInvariant(replacement[0]) + replacement[1..];

        return replacement;
    }
}

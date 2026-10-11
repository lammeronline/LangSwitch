namespace LangSwitch.Core;

/// <summary>
/// Detects the classic "Caps Lock was accidentally on" typing pattern — first letter
/// lowercase, every subsequent letter uppercase (e.g. "hELLO" instead of "Hello") — and
/// fixes it by inverting the case of every letter, if the inverted form is a known word.
/// Layout-agnostic: works for whatever language the inverted word turns out to belong to.
/// </summary>
public static class CapsLockCorrector
{
    public static string? TryFix(string word, LanguageDictionaries dicts)
    {
        if (word.Length < 2) return null;
        if (!char.IsLower(word[0])) return null;

        bool hasUpper = false;
        for (int i = 1; i < word.Length; i++)
        {
            char c = word[i];
            if (!char.IsLetter(c)) continue;
            if (!char.IsUpper(c)) return null;
            hasUpper = true;
        }
        if (!hasUpper) return null;

        var inverted = Invert(word);
        return dicts.ContainsAny(inverted) ? inverted : null;
    }

    static string Invert(string word) => new(word.Select(c =>
        char.IsUpper(c) ? char.ToLowerInvariant(c) :
        char.IsLower(c) ? char.ToUpperInvariant(c) : c).ToArray());
}

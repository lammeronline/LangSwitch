using LangSwitch.Core;

namespace LangSwitch.Autocorrect;

sealed class AutocorrectEngine
{
    LanguageDictionaries _langDicts;

    public AutocorrectEngine(LanguageDictionaries langDicts) => _langDicts = langDicts;

    public void ReloadWordLists(LanguageDictionaries l) => _langDicts = l;

    public bool TryReplace(WordReadyEventArgs e)
    {
        var corrected = LayoutAutoCorrector.TryCorrect(e.Word, _langDicts);
        if (corrected is null) return false;

        string replacement = PreserveCase(e.Word, corrected);
        e.HookArgs.Handled = true;
        TextInjector.Replace(e.Word.Length, replacement + e.Boundary);
        return true;
    }

    static string PreserveCase(string typed, string replacement)
    {
        if (replacement.Length == 0) return replacement;
        if (char.IsUpper(typed[0]) && char.IsLower(replacement[0]))
            return char.ToUpperInvariant(replacement[0]) + replacement[1..];
        return replacement;
    }
}

namespace LangSwitch.Core;

/// <summary>Generates wrong-layout conversion candidates for a typed word.</summary>
interface ILayoutCandidateProvider
{
    /// <summary>All plausible reinterpretations of <paramref name="word"/> under every
    /// other installed layout, each tagged with the target layout's language code.</summary>
    IEnumerable<(string Word, string Lang)> GetAllCandidates(string word);

    /// <summary>The first candidate that differs from <paramref name="text"/>, or
    /// <paramref name="text"/> unchanged if none does.</summary>
    string ConvertToOppositeLayout(string text);

    /// <summary>
    /// Same as <see cref="ConvertToOppositeLayout(string)"/>, but with 3+ installed layouts
    /// prefers a candidate that's an actual known word in <paramref name="dicts"/> over
    /// arbitrarily taking whichever candidate comes first in layout-map iteration order.
    /// </summary>
    string ConvertToOppositeLayout(string text, LanguageDictionaries? dicts)
    {
        if (dicts is null) return ConvertToOppositeLayout(text);
        if (string.IsNullOrEmpty(text)) return text;

        var candidates = GetAllCandidates(text).ToList();
        if (candidates.Count == 0) return text;

        foreach (var (word, lang) in candidates)
            if (dicts.Contains(lang, word)) return word;

        return candidates[0].Word;
    }
}

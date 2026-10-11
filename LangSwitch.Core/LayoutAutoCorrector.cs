namespace LangSwitch.Core;

public static class LayoutAutoCorrector
{
    /// <summary>
    /// If the word looks like it was typed on the wrong layout, returns the corrected
    /// version. Returns null when no confident correction can be made.
    /// </summary>
    public static string? TryCorrect(string word, IEnumerable<(string Word, string Lang)> candidatesSource, LanguageDictionaries dicts)
    {
        if (word.Length < 2) return null;

        var candidates = candidatesSource.ToList();
        if (candidates.Count == 0) return null;

        bool originalIsKnown = dicts.ContainsAny(word);
        if (originalIsKnown) return null; // word is already correct

        // Find first candidate that exists in word lists
        foreach (var (candidate, lang) in candidates)
        {
            if (dicts.Contains(lang, candidate))
                return candidate;
        }

        // If no candidate found in lists but original is definitely wrong
        // (all chars unmapped or clearly wrong script), use ChooseBest
        var best = dicts.ChooseBest(word, candidates);
        return best != word ? best : null;
    }
}

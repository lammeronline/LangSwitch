namespace LangSwitch.Core;

public static class LayoutAutoCorrector
{
    /// <summary>
    /// If the word looks like it was typed on the wrong layout, returns the corrected
    /// version. Returns null when no confident correction can be made.
    /// </summary>
    public static string? TryCorrect(string word, IEnumerable<(string Word, string Lang)> candidatesSource, LanguageDictionaries dicts)
    {
        // 2-letter words collide too often with a real short word in another layout (e.g.
        // "yf" <-> "на") — the resulting "wrong layout?" suggestions read as noise more than
        // help, so require at least 3 characters before offering one.
        if (word.Length < 3) return null;

        var candidates = candidatesSource.ToList();
        if (candidates.Count == 0) return null;

        bool originalIsKnown = dicts.ContainsAny(word);
        if (originalIsKnown) return null; // word is already correct

        // A candidate only wins if it's the *only* one that matches a loaded dictionary.
        // If more than one does (e.g. the word is a plausible candidate in two installed,
        // closely related languages — classically RU/UK), stay conservative and don't guess.
        var matches = candidates.Where(c => dicts.Contains(c.Lang, c.Word)).ToList();
        if (matches.Count == 1) return matches[0].Word;
        if (matches.Count > 1) return null;

        // If no candidate found in lists but original is definitely wrong
        // (all chars unmapped or clearly wrong script), use ChooseBest
        var best = dicts.ChooseBest(word, candidates);
        return best != word ? best : null;
    }
}

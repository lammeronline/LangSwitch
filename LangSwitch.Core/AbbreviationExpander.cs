namespace LangSwitch.Core;

public static class AbbreviationExpander
{
    /// <summary>
    /// Returns the expansion text for a known, enabled abbreviation trigger
    /// (case-insensitive match), or null if the word matches none.
    /// </summary>
    public static string? TryExpand(string word, IReadOnlyList<Abbreviation> abbreviations)
    {
        foreach (var a in abbreviations)
        {
            if (a.Enabled && !string.IsNullOrEmpty(a.Trigger) &&
                string.Equals(a.Trigger, word, StringComparison.OrdinalIgnoreCase))
                return a.Expansion;
        }
        return null;
    }
}

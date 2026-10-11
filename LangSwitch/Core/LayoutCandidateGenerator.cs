namespace LangSwitch.Core;

/// <summary>
/// Generalized replacement for the old hardcoded EN/RU/UK <c>LayoutConverter</c>. Works for
/// any set of installed layouts: for each installed layout A, the typed word is interpreted
/// as if it had been typed under A (A's map inverted back to the physical-key baseline),
/// then re-expressed under every other installed layout B. With exactly EN+RU+UK installed
/// this reproduces the old candidate set exactly; with more layouts installed it covers every
/// pair. Depends only on <see cref="ILayoutCharMapSource"/>, not on Win32 directly, so this
/// composition logic is unit-testable with a fake source.
/// </summary>
sealed class LayoutCandidateGenerator(ILayoutCharMapSource source) : ILayoutCandidateProvider
{
    public IEnumerable<(string Word, string Lang)> GetAllCandidates(string word)
    {
        if (string.IsNullOrEmpty(word)) yield break;

        var maps = source.GetInstalledLayoutMaps();
        var seen = new HashSet<(string, string)>();

        foreach (var (langA, mapA) in maps)
        {
            var inverseA = Invert(mapA);
            var baseline = Apply(word, inverseA);

            foreach (var (langB, mapB) in maps)
            {
                if (langB == langA) continue;

                var candidate = Apply(baseline, mapB);
                if (candidate == word) continue;
                if (seen.Add((candidate, langB)))
                    yield return (candidate, langB);
            }
        }
    }

    public string ConvertToOppositeLayout(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        foreach (var (candidate, _) in GetAllCandidates(text))
            return candidate;
        return text;
    }

    static string Apply(string text, IReadOnlyDictionary<char, char> map) =>
        new(text.Select(c => map.GetValueOrDefault(c, c)).ToArray());

    static Dictionary<char, char> Invert(IReadOnlyDictionary<char, char> map)
    {
        var inverse = new Dictionary<char, char>();
        foreach (var (baseline, layoutChar) in map)
            inverse[layoutChar] = baseline;
        return inverse;
    }
}

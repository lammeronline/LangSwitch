using LangSwitch.Core;

namespace LangSwitch.Tests;

/// <summary>Synthetic layout maps, standing in for <see cref="WinLayoutCharMapSource"/> so the
/// composition algorithm in <see cref="LayoutCandidateGenerator"/> can be tested without a
/// real Windows keyboard-layout subsystem.</summary>
sealed class FakeLayoutCharMapSource(Dictionary<string, Dictionary<char, char>> maps) : ILayoutCharMapSource
{
    public IReadOnlyDictionary<string, IReadOnlyDictionary<char, char>> GetInstalledLayoutMaps() =>
        maps.ToDictionary(kv => kv.Key, kv => (IReadOnlyDictionary<char, char>)kv.Value);
}

public class LayoutCandidateGeneratorTests
{
    // Reconstructs the historical hardcoded EN/RU/UK tables as synthetic layout-source data,
    // so results here are directly comparable to the app's pre-dynamic-mapping behavior.
    static Dictionary<char, char> WithUppercase(Dictionary<char, char> map)
    {
        foreach (var (k, v) in map.ToArray())
            if (char.IsLetter(k))
                map[char.ToUpperInvariant(k)] = char.ToUpperInvariant(v);
        return map;
    }

    static Dictionary<char, char> Identity(string baseline) =>
        WithUppercase(baseline.ToDictionary(c => c, c => c));

    static readonly Dictionary<char, char> RuMap = WithUppercase(new()
    {
        ['`'] = 'ё', ['q'] = 'й', ['w'] = 'ц', ['e'] = 'у', ['r'] = 'к', ['t'] = 'е', ['y'] = 'н', ['u'] = 'г',
        ['i'] = 'ш', ['o'] = 'щ', ['p'] = 'з', ['['] = 'х', [']'] = 'ъ', ['a'] = 'ф', ['s'] = 'ы', ['d'] = 'в',
        ['f'] = 'а', ['g'] = 'п', ['h'] = 'р', ['j'] = 'о', ['k'] = 'л', ['l'] = 'д', [';'] = 'ж', ['\''] = 'э',
        ['z'] = 'я', ['x'] = 'ч', ['c'] = 'с', ['v'] = 'м', ['b'] = 'и', ['n'] = 'т', ['m'] = 'ь', [','] = 'б',
        ['.'] = 'ю',
    });

    static readonly Dictionary<char, char> UkMap = WithUppercase(new()
    {
        ['`'] = 'ґ', ['q'] = 'й', ['w'] = 'ц', ['e'] = 'у', ['r'] = 'к', ['t'] = 'е', ['y'] = 'н', ['u'] = 'г',
        ['i'] = 'ш', ['o'] = 'щ', ['p'] = 'з', ['['] = 'х', [']'] = 'ї', ['a'] = 'ф', ['s'] = 'і', ['d'] = 'в',
        ['f'] = 'а', ['g'] = 'п', ['h'] = 'р', ['j'] = 'о', ['k'] = 'л', ['l'] = 'д', [';'] = 'ж', ['\''] = 'є',
        ['z'] = 'я', ['x'] = 'ч', ['c'] = 'с', ['v'] = 'м', ['b'] = 'и', ['n'] = 'т', ['m'] = 'ь', [','] = 'б',
        ['.'] = 'ю',
    });

    static readonly Dictionary<char, char> EnMap = Identity("qwertyuiop[]asdfghjkl;'zxcvbnm,./`");

    static LayoutCandidateGenerator GeneratorWithEnRuUk() =>
        new(new FakeLayoutCharMapSource(new() { ["en"] = EnMap, ["ru"] = RuMap, ["uk"] = UkMap }));

    [Fact]
    public void GetAllCandidates_LatinTypedForRussian_ProducesRussianCandidate()
    {
        var gen = GeneratorWithEnRuUk();
        var candidates = gen.GetAllCandidates("ghbdtn").ToList();
        Assert.Contains(("привет", "ru"), candidates);
    }

    [Fact]
    public void GetAllCandidates_PreservesCase()
    {
        var gen = GeneratorWithEnRuUk();
        var candidates = gen.GetAllCandidates("Ghbdtn").ToList();
        Assert.Contains(("Привет", "ru"), candidates);
    }

    [Fact]
    public void GetAllCandidates_CyrillicTypedForEnglish_ProducesEnglishCandidate()
    {
        var gen = GeneratorWithEnRuUk();
        var candidates = gen.GetAllCandidates("привет").ToList();
        Assert.Contains(("ghbdtn", "en"), candidates);
    }

    [Fact]
    public void GetAllCandidates_NoIdenticalCandidates()
    {
        var gen = GeneratorWithEnRuUk();
        var candidates = gen.GetAllCandidates("ghbdtn").ToList();
        Assert.DoesNotContain(candidates, c => c.Word == "ghbdtn");
    }

    [Fact]
    public void ConvertToOppositeLayout_ReturnsFirstDifferingCandidate()
    {
        var gen = GeneratorWithEnRuUk();
        var result = gen.ConvertToOppositeLayout("ghbdtn");
        Assert.NotEqual("ghbdtn", result);
    }

    [Fact]
    public void ConvertToOppositeLayout_NoCandidatesDiffer_ReturnsInputUnchanged()
    {
        var gen = new LayoutCandidateGenerator(new FakeLayoutCharMapSource(new() { ["en"] = EnMap }));
        var result = gen.ConvertToOppositeLayout("hello");
        Assert.Equal("hello", result);
    }

    [Fact]
    public void GetAllCandidates_GeneralizesBeyondTwoLayouts_CoversEveryInstalledPair()
    {
        // A synthetic third layout (distinct from EN/RU/UK) installed alongside RU — the
        // generator should still produce a RU candidate for text typed under the third layout,
        // proving the algorithm isn't hardcoded to exactly EN/RU/UK.
        var thirdLayout = WithUppercase(new Dictionary<char, char> { ['g'] = 'x', ['h'] = 'y', ['b'] = 'z' });
        var gen = new LayoutCandidateGenerator(new FakeLayoutCharMapSource(new()
        {
            ["en"] = EnMap, ["ru"] = RuMap, ["xy"] = thirdLayout,
        }));

        // "xyz" typed under the third layout's physical keys corresponds to baseline "ghb".
        var candidates = gen.GetAllCandidates("xyz").ToList();
        Assert.Contains(candidates, c => c.Lang == "ru");
        Assert.Contains(candidates, c => c.Lang == "en");
    }

    [Fact]
    public void GetAllCandidates_EmptyWord_ReturnsNoCandidates()
    {
        var gen = GeneratorWithEnRuUk();
        Assert.Empty(gen.GetAllCandidates(""));
    }
}

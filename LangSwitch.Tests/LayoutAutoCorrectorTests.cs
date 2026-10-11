using LangSwitch.Core;

namespace LangSwitch.Tests;

public class LayoutAutoCorrectorTests
{
    static LanguageDictionaries Dicts(params string[] langs) =>
        new(langs.Select(l => new UserDictionary { Name = l, Lang = l, Enabled = true }).ToList());

    [Fact]
    public void TryCorrect_WrongLayoutGibberish_ReturnsCorrectWord()
    {
        var dicts = Dicts("en", "ru");
        // "ghbdtn" on an EN layout = "привет" typed on RU layout
        var candidates = new[] { ("привет", "ru") };
        var result = LayoutAutoCorrector.TryCorrect("ghbdtn", candidates, dicts);
        Assert.Equal("привет", result);
    }

    [Fact]
    public void TryCorrect_AlreadyValidWord_ReturnsNull()
    {
        var dicts = Dicts("en", "ru");
        var candidates = new[] { ("руддщ", "ru") }; // irrelevant — original is already known
        var result = LayoutAutoCorrector.TryCorrect("hello", candidates, dicts);
        Assert.Null(result);
    }

    [Fact]
    public void TryCorrect_UnknownGibberishInAnyLanguage_ReturnsNull()
    {
        var dicts = Dicts("en", "ru");
        // Neither "zzqxw" nor its candidates are real words in any loaded dictionary.
        var candidates = new[] { ("zzqxw", "ru"), ("zzqxw", "en") };
        var result = LayoutAutoCorrector.TryCorrect("zzqxw", candidates, dicts);
        Assert.Null(result);
    }

    [Fact]
    public void TryCorrect_TooShortWord_ReturnsNull()
    {
        var dicts = Dicts("en", "ru");
        var result = LayoutAutoCorrector.TryCorrect("a", [], dicts);
        Assert.Null(result);
    }

    [Fact]
    public void TryCorrect_NoCandidates_ReturnsNull()
    {
        var dicts = Dicts("en", "ru");
        var result = LayoutAutoCorrector.TryCorrect("hello", [], dicts);
        Assert.Null(result);
    }

    [Fact]
    public void TryCorrect_AmbiguousAcrossTwoLanguages_ReturnsNull()
    {
        var dicts = Dicts("ru", "uk");
        // Both candidates are known words in their respective dictionaries — ambiguous,
        // stay conservative rather than guessing one.
        var candidates = new[] { ("привет", "ru"), ("привіт", "uk") };
        var result = LayoutAutoCorrector.TryCorrect("ghbdtn", candidates, dicts);
        Assert.Null(result);
    }
}

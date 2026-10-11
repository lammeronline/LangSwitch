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
        var result = LayoutAutoCorrector.TryCorrect("ghbdtn", dicts);
        Assert.Equal("привет", result);
    }

    [Fact]
    public void TryCorrect_AlreadyValidWord_ReturnsNull()
    {
        var dicts = Dicts("en", "ru");
        var result = LayoutAutoCorrector.TryCorrect("hello", dicts);
        Assert.Null(result);
    }

    [Fact]
    public void TryCorrect_UnknownGibberishInAnyLanguage_ReturnsNull()
    {
        var dicts = Dicts("en", "ru");
        // Neither "zzqxw" nor its RU/UK candidates are real words.
        var result = LayoutAutoCorrector.TryCorrect("zzqxw", dicts);
        Assert.Null(result);
    }

    [Fact]
    public void TryCorrect_TooShortWord_ReturnsNull()
    {
        var dicts = Dicts("en", "ru");
        var result = LayoutAutoCorrector.TryCorrect("a", dicts);
        Assert.Null(result);
    }
}

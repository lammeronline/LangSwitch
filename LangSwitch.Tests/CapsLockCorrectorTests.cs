using LangSwitch.Core;

namespace LangSwitch.Tests;

public class CapsLockCorrectorTests
{
    static LanguageDictionaries Dicts(params string[] langs) =>
        new(langs.Select(l => new UserDictionary { Name = l, Lang = l, Enabled = true }).ToList());

    [Fact]
    public void TryFix_InvertedCaseKnownWord_ReturnsCorrectedWord()
    {
        var dicts = Dicts("en");
        Assert.Equal("Hello", CapsLockCorrector.TryFix("hELLO", dicts));
    }

    [Fact]
    public void TryFix_InvertedCaseUnknownWord_ReturnsNull()
    {
        var dicts = Dicts("en");
        Assert.Null(CapsLockCorrector.TryFix("qZQZQ", dicts));
    }

    [Fact]
    public void TryFix_PlainLowercaseWord_ReturnsNull()
    {
        var dicts = Dicts("en");
        Assert.Null(CapsLockCorrector.TryFix("hello", dicts));
    }

    [Fact]
    public void TryFix_PlainUppercaseWord_ReturnsNull()
    {
        var dicts = Dicts("en");
        Assert.Null(CapsLockCorrector.TryFix("HELLO", dicts));
    }

    [Fact]
    public void TryFix_TooShortWord_ReturnsNull()
    {
        var dicts = Dicts("en");
        Assert.Null(CapsLockCorrector.TryFix("h", dicts));
    }

    [Fact]
    public void TryFix_MixedCaseNotInvertedPattern_ReturnsNull()
    {
        var dicts = Dicts("en");
        // "HeLLo" — first letter upper, so doesn't match the inversion pattern at all.
        Assert.Null(CapsLockCorrector.TryFix("HeLLo", dicts));
    }
}

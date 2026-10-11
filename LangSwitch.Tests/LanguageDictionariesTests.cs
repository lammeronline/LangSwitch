using LangSwitch.Core;

namespace LangSwitch.Tests;

public class LanguageDictionariesTests
{
    static List<UserDictionary> Dicts(params (string Lang, bool Enabled)[] entries) =>
        entries.Select(e => new UserDictionary { Name = e.Lang, Lang = e.Lang, Enabled = e.Enabled }).ToList();

    [Fact]
    public void ContainsAny_FallbackWord_IsKnown()
    {
        var dicts = new LanguageDictionaries(Dicts(("en", true)));
        Assert.True(dicts.ContainsAny("Hello"));
    }

    [Fact]
    public void ContainsAny_UnknownWord_IsNotKnown()
    {
        var dicts = new LanguageDictionaries(Dicts(("en", true)));
        Assert.False(dicts.ContainsAny("zzqxw"));
    }

    [Fact]
    public void DisabledDictionary_WordsAreNotLoaded()
    {
        var dicts = new LanguageDictionaries(Dicts(("en", false)));
        Assert.False(dicts.ContainsAny("hello"));
        Assert.Equal(0, dicts.WordCount("en"));
    }

    [Fact]
    public void Reload_TogglingEnabled_ChangesContainsAnyResult()
    {
        var dicts = new LanguageDictionaries(Dicts(("ru", true)));
        Assert.True(dicts.ContainsAny("привет"));

        dicts.Reload(Dicts(("ru", false)));
        Assert.False(dicts.ContainsAny("привет"));
    }

    [Fact]
    public void CustomLanguage_WithNoWordListFile_LoadsEmptySetButDoesNotThrow()
    {
        var dicts = new LanguageDictionaries(Dicts(("xx", true)));
        Assert.Equal(0, dicts.WordCount("xx"));
        Assert.False(dicts.Contains("xx", "anything"));
    }

    [Fact]
    public void ChooseBest_PicksHigherScoringCandidate()
    {
        var dicts = new LanguageDictionaries(Dicts(("ru", true)));
        var candidates = new[] { ("привет", "ru"), ("zzqxw", "en") };
        var best = dicts.ChooseBest("ghbdtn", candidates);
        Assert.Equal("привет", best);
    }

    [Fact]
    public void ChooseBest_NoCandidateBeatsOriginal_ReturnsOriginal()
    {
        var dicts = new LanguageDictionaries(Dicts(("en", true)));
        var candidates = new[] { ("zzqxw", "en"), ("qxxzz", "ru") };
        var best = dicts.ChooseBest("original", candidates);
        Assert.Equal("original", best);
    }

    [Fact]
    public void SourceOf_BuiltinFallbackLanguage_ReportsBuiltin()
    {
        var dicts = new LanguageDictionaries(Dicts(("en", true)));
        Assert.Equal(DictionarySource.Builtin, dicts.SourceOf("en"));
    }

    [Fact]
    public void SourceOf_UnknownLanguage_ReportsNone()
    {
        var dicts = new LanguageDictionaries(Dicts(("xx", true)));
        Assert.Equal(DictionarySource.None, dicts.SourceOf("xx"));
    }

    [Fact]
    public void SourceOf_DisabledLanguage_ReportsNone()
    {
        var dicts = new LanguageDictionaries(Dicts(("en", false)));
        Assert.Equal(DictionarySource.None, dicts.SourceOf("en"));
    }
}

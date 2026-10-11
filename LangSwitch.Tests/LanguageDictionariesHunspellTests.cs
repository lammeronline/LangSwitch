using LangSwitch.Core;

namespace LangSwitch.Tests;

/// <summary>
/// Exercises the Hunspell-backed path of <see cref="LanguageDictionaries"/> using a tiny
/// synthetic .dic/.aff pair written to disk — no network, no real affix rules needed, just
/// enough to confirm WordList.CreateFromStreams loads and Check()/WordCount() behave.
/// </summary>
public class LanguageDictionariesHunspellTests : IDisposable
{
    const string Lang = "zz-test";
    readonly string _dicPath = Path.Combine(LanguageDictionaries.WordListDir, Lang + ".dic");
    readonly string _affPath = Path.Combine(LanguageDictionaries.WordListDir, Lang + ".aff");

    public LanguageDictionariesHunspellTests()
    {
        Directory.CreateDirectory(LanguageDictionaries.WordListDir);
        // Stem count on line 1, then stems; "book/S" + a SFX rule lets the engine accept "books" too.
        File.WriteAllText(_dicPath, "2\nbook/S\nworld\n");
        File.WriteAllText(_affPath, "SET UTF-8\nSFX S Y 1\nSFX S 0 s .\n");
    }

    public void Dispose()
    {
        File.Delete(_dicPath);
        File.Delete(_affPath);
    }

    static List<UserDictionary> Dicts(params (string Lang, bool Enabled)[] entries) =>
        entries.Select(e => new UserDictionary { Name = e.Lang, Lang = e.Lang, Enabled = e.Enabled }).ToList();

    [Fact]
    public void Contains_StemWord_IsKnown()
    {
        var dicts = new LanguageDictionaries(Dicts((Lang, true)));
        Assert.True(dicts.Contains(Lang, "book"));
        Assert.True(dicts.Contains(Lang, "world"));
    }

    [Fact]
    public void Contains_AffixExpandedForm_IsKnown()
    {
        var dicts = new LanguageDictionaries(Dicts((Lang, true)));
        Assert.True(dicts.Contains(Lang, "books"));
    }

    [Fact]
    public void Contains_UnknownWord_IsNotKnown()
    {
        var dicts = new LanguageDictionaries(Dicts((Lang, true)));
        Assert.False(dicts.Contains(Lang, "zzqxw"));
    }

    [Fact]
    public void WordCount_ReflectsStemCountFromDicFile()
    {
        var dicts = new LanguageDictionaries(Dicts((Lang, true)));
        Assert.Equal(2, dicts.WordCount(Lang));
    }

    [Fact]
    public void Contains_CapitalizedForm_IsKnown()
    {
        var dicts = new LanguageDictionaries(Dicts((Lang, true)));
        Assert.True(dicts.Contains(Lang, "Book"));
    }
}

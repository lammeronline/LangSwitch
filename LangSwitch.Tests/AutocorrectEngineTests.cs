using System.Windows.Forms;
using LangSwitch.Autocorrect;
using LangSwitch.Core;

namespace LangSwitch.Tests;

sealed class FakeTextInjector : ITextInjector
{
    public List<(int DeleteCount, string NewText)> Calls { get; } = [];
    public void Replace(int deleteCount, string newText) => Calls.Add((deleteCount, newText));
}

/// <summary>Canned candidates keyed case-insensitively by the typed word, standing in for
/// the real Win32-backed <see cref="LayoutCandidateGenerator"/> so tests stay deterministic.</summary>
sealed class FakeLayoutCandidateProvider(Dictionary<string, (string Word, string Lang)[]>? byWord = null) : ILayoutCandidateProvider
{
    readonly Dictionary<string, (string Word, string Lang)[]> _byWord =
        new(byWord ?? [], StringComparer.OrdinalIgnoreCase);

    public IEnumerable<(string Word, string Lang)> GetAllCandidates(string word) =>
        _byWord.TryGetValue(word, out var c) ? c : [];

    public string ConvertToOppositeLayout(string text) =>
        GetAllCandidates(text).Select(c => c.Word).FirstOrDefault(w => w != text) ?? text;
}

public class AutocorrectEngineTests
{
    static LanguageDictionaries Dicts(params string[] langs) =>
        new(langs.Select(l => new UserDictionary { Name = l, Lang = l, Enabled = true }).ToList());

    static WordReadyEventArgs WordReady(string word, char boundary = ' ') =>
        new(word, boundary, new HookKeyEventArgs(Keys.Space, 0));

    static readonly ILayoutCandidateProvider DefaultCandidates = new FakeLayoutCandidateProvider(
        new() { ["ghbdtn"] = [("привет", "ru")] });

    static AutocorrectEngine Engine(
        LanguageDictionaries dicts, ITextInjector injector,
        List<Abbreviation>? abbreviations = null, bool fixCapsLock = true,
        ILayoutCandidateProvider? candidates = null, HashSet<string>? neverCorrect = null) =>
        new(dicts, injector, candidates ?? DefaultCandidates, abbreviations ?? [], fixCapsLock, neverCorrect);

    [Fact]
    public void TryReplace_KnownWord_DoesNotInject()
    {
        var injector = new FakeTextInjector();
        var engine = Engine(Dicts("en", "ru"), injector);
        var e = WordReady("hello");

        bool replaced = engine.TryReplace(e, out var replacement);

        Assert.False(replaced);
        Assert.Null(replacement);
        Assert.Empty(injector.Calls);
        Assert.False(e.HookArgs.Handled);
    }

    [Fact]
    public void TryReplace_WrongLayoutWord_InjectsCorrectedWordAndBoundary()
    {
        var injector = new FakeTextInjector();
        var engine = Engine(Dicts("en", "ru"), injector);
        var e = WordReady("ghbdtn", boundary: ' ');

        bool replaced = engine.TryReplace(e, out var replacement);

        Assert.True(replaced);
        Assert.Equal("привет", replacement);
        Assert.True(e.HookArgs.Handled);
        var call = Assert.Single(injector.Calls);
        Assert.Equal(6, call.DeleteCount);
        Assert.Equal("привет ", call.NewText);
    }

    [Fact]
    public void TryReplace_PreservesCapitalizedFirstLetter()
    {
        var injector = new FakeTextInjector();
        var engine = Engine(Dicts("en", "ru"), injector);
        var e = WordReady("Ghbdtn");

        engine.TryReplace(e, out _);

        var call = Assert.Single(injector.Calls);
        Assert.StartsWith("Привет", call.NewText);
    }

    [Fact]
    public void TryReplace_KnownAbbreviation_ExpandsInsteadOfLayoutCorrecting()
    {
        var injector = new FakeTextInjector();
        var abbrevs = new List<Abbreviation> { new() { Trigger = "btw", Expansion = "by the way", Enabled = true } };
        var engine = Engine(Dicts("en"), injector, abbrevs);
        var e = WordReady("btw");

        bool replaced = engine.TryReplace(e, out var replacement);

        Assert.True(replaced);
        Assert.Equal("by the way", replacement);
        var call = Assert.Single(injector.Calls);
        Assert.Equal("by the way ", call.NewText);
    }

    [Fact]
    public void TryReplace_DisabledAbbreviation_DoesNotExpand()
    {
        var injector = new FakeTextInjector();
        var abbrevs = new List<Abbreviation> { new() { Trigger = "btw", Expansion = "by the way", Enabled = false } };
        var engine = Engine(Dicts("en"), injector, abbrevs);
        var e = WordReady("btw");

        bool replaced = engine.TryReplace(e, out _);

        Assert.False(replaced);
        Assert.Empty(injector.Calls);
    }

    [Fact]
    public void TryReplace_CapsLockInversion_FixedBeforeLayoutCorrection()
    {
        var injector = new FakeTextInjector();
        var engine = Engine(Dicts("en"), injector);
        var e = WordReady("hELLO");

        bool replaced = engine.TryReplace(e, out var replacement);

        Assert.True(replaced);
        Assert.Equal("Hello", replacement);
    }

    [Fact]
    public void TryReplace_CapsLockFixDisabled_FallsThroughToLayoutCorrection()
    {
        var injector = new FakeTextInjector();
        // "hELLO" case-inverted is "Hello" (en) but with fix disabled it should not trigger;
        // layout-conversion candidates for "hELLO" are not real words either, so no correction at all.
        var engine = Engine(Dicts("en"), injector, fixCapsLock: false);
        var e = WordReady("hELLO");

        bool replaced = engine.TryReplace(e, out _);

        Assert.False(replaced);
    }

    [Fact]
    public void TryReplace_WordInNeverCorrectSet_IsLeftAlone()
    {
        var injector = new FakeTextInjector();
        var neverCorrect = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ghbdtn" };
        var engine = Engine(Dicts("en", "ru"), injector, neverCorrect: neverCorrect);
        var e = WordReady("ghbdtn");

        bool replaced = engine.TryReplace(e, out var replacement);

        Assert.False(replaced);
        Assert.Null(replacement);
        Assert.Empty(injector.Calls);
    }

    [Theory]
    [InlineData("hi", "HI", "HI")]
    [InlineData("Hi", "there", "There")]
    public void PreserveCase_Behavior(string typed, string replacement, string expected)
    {
        Assert.Equal(expected, AutocorrectEngine.PreserveCase(typed, replacement));
    }

    [Fact]
    public void PreserveCase_AllCapsTypedWord_UppercasesWholeReplacement()
    {
        var result = AutocorrectEngine.PreserveCase("GHBDTN", "привет");
        Assert.Equal("ПРИВЕТ", result);
    }
}

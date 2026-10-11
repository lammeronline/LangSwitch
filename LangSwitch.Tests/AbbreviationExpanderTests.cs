using LangSwitch.Core;

namespace LangSwitch.Tests;

public class AbbreviationExpanderTests
{
    [Fact]
    public void TryExpand_KnownTrigger_ReturnsExpansion()
    {
        var abbrevs = new List<Abbreviation> { new() { Trigger = "btw", Expansion = "by the way", Enabled = true } };
        Assert.Equal("by the way", AbbreviationExpander.TryExpand("btw", abbrevs));
    }

    [Fact]
    public void TryExpand_IsCaseInsensitive()
    {
        var abbrevs = new List<Abbreviation> { new() { Trigger = "btw", Expansion = "by the way", Enabled = true } };
        Assert.Equal("by the way", AbbreviationExpander.TryExpand("BTW", abbrevs));
    }

    [Fact]
    public void TryExpand_DisabledEntry_ReturnsNull()
    {
        var abbrevs = new List<Abbreviation> { new() { Trigger = "btw", Expansion = "by the way", Enabled = false } };
        Assert.Null(AbbreviationExpander.TryExpand("btw", abbrevs));
    }

    [Fact]
    public void TryExpand_UnknownWord_ReturnsNull()
    {
        var abbrevs = new List<Abbreviation> { new() { Trigger = "btw", Expansion = "by the way", Enabled = true } };
        Assert.Null(AbbreviationExpander.TryExpand("otherword", abbrevs));
    }

    [Fact]
    public void TryExpand_EmptyList_ReturnsNull()
    {
        Assert.Null(AbbreviationExpander.TryExpand("btw", []));
    }
}

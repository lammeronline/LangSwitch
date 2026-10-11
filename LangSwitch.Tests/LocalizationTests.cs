using LangSwitch.Core;

namespace LangSwitch.Tests;

public class LocalizationTests
{
    [Fact]
    public void Merge_OverlayOverridesFallback()
    {
        var fallback = new Dictionary<string, string> { ["a"] = "A-en", ["b"] = "B-en" };
        var overlay  = new Dictionary<string, string> { ["a"] = "A-ru" };

        var merged = Localization.Merge(fallback, overlay);

        Assert.Equal("A-ru", merged["a"]); // overridden
        Assert.Equal("B-en", merged["b"]); // falls back — overlay doesn't have it
    }

    [Fact]
    public void Merge_NullOverlay_ReturnsFallbackOnly()
    {
        var fallback = new Dictionary<string, string> { ["a"] = "A-en" };

        var merged = Localization.Merge(fallback, null);

        Assert.Equal("A-en", merged["a"]);
        Assert.Single(merged);
    }

    [Fact]
    public void Merge_EmptyFallback_OverlayStillApplies()
    {
        var overlay = new Dictionary<string, string> { ["x"] = "X" };

        var merged = Localization.Merge(new Dictionary<string, string>(), overlay);

        Assert.Equal("X", merged["x"]);
    }

    [Fact]
    public void Merge_DoesNotMutateInputDictionaries()
    {
        var fallback = new Dictionary<string, string> { ["a"] = "A-en" };
        var overlay  = new Dictionary<string, string> { ["a"] = "A-ru" };

        Localization.Merge(fallback, overlay);

        Assert.Equal("A-en", fallback["a"]);
        Assert.Equal("A-ru", overlay["a"]);
    }
}

using LangSwitch.Core;

namespace LangSwitch.Tests;

public class LayoutConverterTests
{
    [Theory]
    [InlineData("ghbdtn", "привет")]  // "привет" typed on an EN layout
    [InlineData("vbh", "мир")]
    public void ConvertToBestLayout_LatinToRussian(string typed, string expected)
    {
        Assert.Equal(expected, LayoutConverter.ConvertToBestLayout(typed));
    }

    [Fact]
    public void ConvertToLayout_RussianToLatin_RoundTrips()
    {
        const string original = "ghbdtn";
        var ru = LayoutConverter.ConvertToLayout(original, "ru");
        var back = LayoutConverter.ConvertToLayout(ru, "en");
        Assert.Equal(original, back);
    }

    [Fact]
    public void ConvertToBestLayout_PreservesUppercase()
    {
        var result = LayoutConverter.ConvertToBestLayout("Ghbdtn");
        Assert.Equal("Привет", result);
    }

    [Fact]
    public void ConvertToBestLayout_EmptyInput_ReturnsEmpty()
    {
        Assert.Equal("", LayoutConverter.ConvertToBestLayout(""));
    }

    [Fact]
    public void GetAllCandidates_LatinInput_YieldsRussianAndUkrainian()
    {
        var langs = LayoutConverter.GetAllCandidates("ghbdtn").Select(c => c.Lang).ToList();
        Assert.Contains("ru", langs);
        Assert.Contains("uk", langs);
    }

    [Fact]
    public void GetAllCandidates_CyrillicInput_YieldsEnglish()
    {
        var langs = LayoutConverter.GetAllCandidates("привет").Select(c => c.Lang).ToList();
        Assert.Contains("en", langs);
    }

    [Fact]
    public void IsLatin_And_IsCyrillic_ClassifyCorrectly()
    {
        Assert.True(LayoutConverter.IsLatin('a'));
        Assert.False(LayoutConverter.IsLatin('а')); // Cyrillic 'а'
        Assert.True(LayoutConverter.IsCyrillic('п'));
        Assert.False(LayoutConverter.IsCyrillic('p'));
    }
}

using System.Globalization;
using LangSwitch.Core;

namespace LangSwitch.Tests;

public class FlagResolverTests
{
    [Theory]
    [InlineData("en-US", "us")]
    [InlineData("ru-RU", "ru")]
    [InlineData("uk-UA", "ua")]
    [InlineData("de-DE", "de")]
    public void Resolve_SpecificCulture_UsesRegion(string cultureName, string expectedFlag)
    {
        var culture = CultureInfo.GetCultureInfo(cultureName);
        Assert.Equal(expectedFlag, FlagResolver.Resolve(culture));
    }

    [Theory]
    [InlineData("en", "us")]
    [InlineData("ru", "ru")]
    [InlineData("uk", "ua")]
    [InlineData("kk", "kz")]
    [InlineData("be", "by")]
    public void Resolve_NeutralCulture_FallsBackToLanguageMapping(string cultureName, string expectedFlag)
    {
        var culture = CultureInfo.GetCultureInfo(cultureName);
        Assert.Equal(expectedFlag, FlagResolver.Resolve(culture));
    }
}

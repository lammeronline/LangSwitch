using System.Globalization;

namespace LangSwitch.Core;

/// <summary>
/// Resolves the two-letter flag/region code used to locate a flag PNG for a culture.
/// Single shared implementation — previously duplicated (and out of sync) between
/// LayoutManager and LayoutInfo.
/// </summary>
public static class FlagResolver
{
    public static string Resolve(CultureInfo culture)
    {
        if (!culture.IsNeutralCulture && !string.IsNullOrEmpty(culture.Name))
        {
            try { return new RegionInfo(culture.Name).TwoLetterISORegionName.ToLowerInvariant(); }
            catch { }
        }
        // Neutral culture fallback: language → flag mapping for common cases
        return culture.TwoLetterISOLanguageName.ToLowerInvariant() switch
        {
            "en" => "us",
            "ru" => "ru",
            "uk" => "ua",
            "de" => "de",
            "fr" => "fr",
            "es" => "es",
            "pl" => "pl",
            "be" => "by",
            "kk" => "kz",
            "ky" => "kg",
            "tg" => "tj",
            "uz" => "uz",
            "ka" => "ge",
            "hy" => "am",
            "az" => "az",
            "lt" => "lt",
            "lv" => "lv",
            "et" => "ee",
            var x => x,
        };
    }
}

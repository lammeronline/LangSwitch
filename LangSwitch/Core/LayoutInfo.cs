using System.Globalization;

namespace LangSwitch.Core;

/// <summary>
/// Represents an installed Windows keyboard layout.
/// </summary>
sealed class LayoutInfo
{
    public IntPtr Handle { get; init; }
    public CultureInfo Culture { get; init; } = CultureInfo.InvariantCulture;
    public string DisplayName { get; init; } = string.Empty;
    public string ShortCode { get; init; } = "??";

    /// <summary>Two-letter ISO region code used to locate the flag PNG (e.g. "us", "ru", "ua").</summary>
    public string FlagCode { get; init; } = string.Empty;

    public static LayoutInfo FromInputLanguage(InputLanguage lang)
    {
        var culture = lang.Culture;
        var flagCode = FlagResolver.Resolve(culture);
        return new LayoutInfo
        {
            Handle = lang.Handle,
            Culture = culture,
            DisplayName = culture.DisplayName,
            ShortCode = flagCode.ToUpperInvariant(),
            FlagCode = flagCode,
        };
    }

    public override string ToString() => DisplayName;
}

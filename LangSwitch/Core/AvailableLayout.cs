namespace LangSwitch.Core;

/// <summary>
/// A keyboard layout known to Windows (may or may not be currently installed for the user).
/// </summary>
sealed class AvailableLayout
{
    /// <summary>8-digit hex string, e.g. "00000409" for English US.</summary>
    public required string Klid { get; init; }
    public required string Name { get; init; }
    public required string FlagCode { get; init; }
    public required string ShortCode { get; init; }

    /// <summary>True when the layout is present in HKCU\Keyboard Layout\Preload.</summary>
    public bool IsInstalled { get; set; }

    public override string ToString() => Name;
}

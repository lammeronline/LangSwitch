namespace LangSwitch.Core;

/// <summary>
/// Provides, for each currently installed keyboard layout, a physical-key character map:
/// the character a fixed set of ~34 physical keys produces under that layout, keyed by the
/// character the same physical key produces on a US/QWERTY baseline (upper and lower case
/// both included). This is the seam that keeps <see cref="LayoutCandidateGenerator"/>'s
/// composition logic unit-testable without a real Windows keyboard-layout subsystem.
/// </summary>
interface ILayoutCharMapSource
{
    /// <summary>Keyed by each installed layout's two-letter language code (e.g. "ru").</summary>
    IReadOnlyDictionary<string, IReadOnlyDictionary<char, char>> GetInstalledLayoutMaps();
}

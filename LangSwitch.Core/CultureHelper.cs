using System.Globalization;

namespace LangSwitch.Core;

/// <summary>
/// CultureInfo's constructors throw for an unrecognized language code or LCID, and every
/// caller that resolves a culture from user/registry-sourced data needs to guard against
/// that the same way — this centralizes the two overloads instead of leaving each call site
/// to reimplement its own try/catch (with its own fallback policy, prone to drifting).
/// </summary>
public static class CultureHelper
{
    public static CultureInfo? TryParse(string name)
    {
        try { return new CultureInfo(name); }
        catch { return null; }
    }

    public static CultureInfo? TryParse(int lcid)
    {
        try { return new CultureInfo(lcid); }
        catch { return null; }
    }
}

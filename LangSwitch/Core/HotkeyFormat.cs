namespace LangSwitch.Core;

/// <summary>Formats a WinForms modifier+key combo as a "Ctrl+Shift+X" style string.</summary>
static class HotkeyFormat
{
    /// <summary>Returns "" for Keys.None — callers that need a placeholder for "no hotkey
    /// set" (e.g. a localized "(None)") apply that on top of this.</summary>
    public static string Combo(Keys k)
    {
        if (k == Keys.None) return "";
        var parts = new List<string>();
        if ((k & Keys.Control) != 0) parts.Add("Ctrl");
        if ((k & Keys.Shift)   != 0) parts.Add("Shift");
        if ((k & Keys.Alt)     != 0) parts.Add("Alt");
        var baseKey = k & Keys.KeyCode;
        if (baseKey != Keys.None) parts.Add(baseKey.ToString());
        return string.Join("+", parts);
    }
}

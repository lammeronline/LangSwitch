using Microsoft.Win32;

namespace LangSwitch.Core;

/// <summary>
/// Registers/unregisters LangSwitch to launch at Windows sign-in via the
/// per-user Run registry key. The registry is the single source of truth —
/// there is no mirrored flag in settings.json — so the UI checkbox can never
/// drift out of sync with reality.
/// </summary>
static class StartupManager
{
    const string RunKey    = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ValueName = "LangSwitch";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) is string value && value == CommandLine();
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        if (key is null) return;

        if (enabled)
            key.SetValue(ValueName, CommandLine());
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    static string CommandLine()
    {
        var path = Environment.ProcessPath ?? Application.ExecutablePath;
        return $"\"{path}\"";
    }
}

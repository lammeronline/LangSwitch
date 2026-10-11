using Microsoft.Win32;

namespace LangSwitch.UI;

static class SystemTheme
{
    /// <summary>True when Windows apps are currently set to dark mode.</summary>
    public static bool IsDark()
    {
        using var key = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("AppsUseLightTheme") is int v && v == 0;
    }
}

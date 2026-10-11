namespace LangSwitch.Core;

/// <summary>
/// The app's icon, extracted from the executable's own Win32 resource (set via
/// ApplicationIcon in LangSwitch.csproj) rather than re-reading the .ico file from disk —
/// loaded once and shared, so every dialog (Settings, About, the app picker) shows it in the
/// taskbar instead of the default blank WinForms icon.
/// </summary>
static class AppIcon
{
    public static readonly Icon? Shared = TryLoad();

    static Icon? TryLoad()
    {
        try { return Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
        catch (Exception ex) { AppLog.Error(ex); return null; }
    }
}

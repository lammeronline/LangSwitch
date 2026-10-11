using LangSwitch.Core;
using LangSwitch.Settings;
using LangSwitch.UI;

namespace LangSwitch;

static class Program
{
    // Fixed GUID so the mutex name is stable across versions/builds — this is what makes
    // "only one LangSwitch at a time" possible, since a second launch sees it already held.
    const string SingleInstanceMutexName = "Global\\LangSwitch-9F3B2C7E-6A1D-4E8F-9B2A-1C7D4E6F8A3B";

    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.SystemAware);

        Application.ThreadException += (_, e) => AppLog.Error(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex) AppLog.Error(ex);
        };

        using var mutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            ShowAlreadyRunningMessage();
            return;
        }

        Application.Run(new TrayApplicationContext());
    }

    static void ShowAlreadyRunningMessage()
    {
        try
        {
            var settings = SettingsStore.Load();
            Localization.Load(string.IsNullOrEmpty(settings.UiLanguage) ? "en" : settings.UiLanguage);
        }
        catch (Exception ex) { AppLog.Error(ex); }

        MessageBox.Show(Localization.T("Common.AlreadyRunning"), Localization.T("Common.AppName"),
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}

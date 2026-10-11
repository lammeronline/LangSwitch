using LangSwitch.Core;

namespace LangSwitch.Settings;

static class SettingsStore
{
    const string FileName = "settings.json";

    public static AppSettings Load() => JsonFileStore.Load(FileName, () => new AppSettings());

    public static void Save(AppSettings settings) => JsonFileStore.Save(FileName, settings);
}

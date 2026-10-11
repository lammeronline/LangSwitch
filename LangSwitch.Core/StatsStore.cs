namespace LangSwitch.Core;

public sealed class Stats
{
    public int WordsCorrected    { get; set; }
    public int CharactersFixed   { get; set; }
    public int ManualConversions { get; set; }
}

public static class StatsStore
{
    const string FileName = "stats.json";

    public static Stats Load() => JsonFileStore.Load(FileName, () => new Stats());

    public static void Save(Stats stats) => JsonFileStore.Save(FileName, stats);
}

namespace LangSwitch.Core;

public static class AbbreviationStore
{
    const string FileName = "abbreviations.json";

    public static List<Abbreviation> Load() => JsonFileStore.Load(FileName, () => new List<Abbreviation>());

    public static void Save(List<Abbreviation> abbreviations) => JsonFileStore.Save(FileName, abbreviations);
}

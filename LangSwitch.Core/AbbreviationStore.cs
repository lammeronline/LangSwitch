using System.Text.Json;

namespace LangSwitch.Core;

public static class AbbreviationStore
{
    static readonly string FilePath = Path.Combine(AppContext.BaseDirectory, "abbreviations.json");

    static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static List<Abbreviation> Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return [];
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<List<Abbreviation>>(json, JsonOptions) ?? [];
        }
        catch (Exception ex)
        {
            AppLog.Error(ex);
            return [];
        }
    }

    public static void Save(List<Abbreviation> abbreviations)
    {
        try
        {
            File.WriteAllText(FilePath, JsonSerializer.Serialize(abbreviations, JsonOptions));
        }
        catch (Exception ex)
        {
            AppLog.Error(ex);
        }
    }
}

using System.Text.Json;

namespace LangSwitch.Core;

/// <summary>
/// Tracks how many times each word's autocorrection has been undone (Backspace right after
/// a correction). Once a word crosses the user's configured threshold, the caller is expected
/// to stop autocorrecting it — see <see cref="AutocorrectEngine.ReloadNeverCorrect"/> ... in
/// the app layer, which owns the undo-count → never-correct decision.
/// </summary>
public static class UndoLearningStore
{
    static readonly string FilePath = Path.Combine(AppContext.BaseDirectory, "word-exceptions.json");

    static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static Dictionary<string, int> Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new(StringComparer.OrdinalIgnoreCase);
            var json = File.ReadAllText(FilePath);
            var dict = JsonSerializer.Deserialize<Dictionary<string, int>>(json, JsonOptions);
            return dict is null
                ? new(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, int>(dict, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            AppLog.Error(ex);
            return new(StringComparer.OrdinalIgnoreCase);
        }
    }

    public static void Save(Dictionary<string, int> counts)
    {
        try
        {
            File.WriteAllText(FilePath, JsonSerializer.Serialize(counts, JsonOptions));
        }
        catch (Exception ex)
        {
            AppLog.Error(ex);
        }
    }
}

namespace LangSwitch.Core;

/// <summary>
/// Tracks how many times each word's autocorrection has been undone (Backspace right after
/// a correction). Once a word crosses the user's configured threshold, the caller is expected
/// to stop autocorrecting it — see <see cref="AutocorrectEngine.ReloadNeverCorrect"/> ... in
/// the app layer, which owns the undo-count → never-correct decision.
/// </summary>
public static class UndoLearningStore
{
    const string FileName = "word-exceptions.json";

    public static Dictionary<string, int> Load()
    {
        var loaded = JsonFileStore.Load<Dictionary<string, int>?>(FileName, () => null);
        return loaded is null
            ? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, int>(loaded, StringComparer.OrdinalIgnoreCase);
    }

    public static void Save(Dictionary<string, int> counts) => JsonFileStore.Save(FileName, counts);
}

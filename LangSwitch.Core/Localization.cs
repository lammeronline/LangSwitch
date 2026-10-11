using System.Text.Json;

namespace LangSwitch.Core;

/// <summary>
/// File-based UI localization — same philosophy as word lists/dictionaries/abbreviations:
/// data lives in editable files next to the exe, so anyone can add a language by dropping in
/// a new Locales/{code}.json with no code changes. English is always loaded underneath the
/// active language as a fallback, so a partial translation never shows a blank or a raw key.
/// </summary>
public static class Localization
{
    public static readonly string LocalesDir = Path.Combine(AppContext.BaseDirectory, "Locales");
    const string FallbackCode = "en";
    const string DisplayNameKey = "_displayName";

    static Dictionary<string, string> _fallback = new();
    static Dictionary<string, string> _active = new();

    /// <summary>Fires after <see cref="Load"/> changes the active language — UI built once at
    /// startup (the tray menu) listens here to know it must rebuild itself.</summary>
    public static event Action? Changed;

    public static IReadOnlyList<(string Code, string DisplayName)> GetAvailableLanguages()
    {
        var result = new List<(string, string)>();
        if (!Directory.Exists(LocalesDir)) return result;

        foreach (var path in Directory.EnumerateFiles(LocalesDir, "*.json"))
        {
            var code = Path.GetFileNameWithoutExtension(path);
            try
            {
                var map = ReadFile(path);
                var name = map.GetValueOrDefault(DisplayNameKey, code);
                result.Add((code, name));
            }
            catch (Exception ex) { AppLog.Error(ex); }
        }
        return result.OrderBy(l => l.Item2, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static void Load(string code)
    {
        _fallback = TryReadLang(FallbackCode) ?? new();
        _active = string.Equals(code, FallbackCode, StringComparison.OrdinalIgnoreCase)
            ? _fallback
            : Merge(_fallback, TryReadLang(code));
        Changed?.Invoke();
    }

    public static string T(string key) =>
        _active.TryGetValue(key, out var v) ? v
            : _fallback.TryGetValue(key, out var fv) ? fv
            : key;

    /// <summary>Pure merge step, split out from file I/O so it's unit-testable: start from the
    /// fallback map, let the overlay (the selected language, which may be partial/custom)
    /// override whatever keys it has.</summary>
    internal static Dictionary<string, string> Merge(Dictionary<string, string> fallback, Dictionary<string, string>? overlay)
    {
        var merged = new Dictionary<string, string>(fallback);
        if (overlay is not null)
            foreach (var (k, v) in overlay)
                merged[k] = v;
        return merged;
    }

    static Dictionary<string, string>? TryReadLang(string code)
    {
        var path = Path.Combine(LocalesDir, code + ".json");
        try { return File.Exists(path) ? ReadFile(path) : null; }
        catch (Exception ex) { AppLog.Error(ex); return null; }
    }

    static Dictionary<string, string> ReadFile(string path) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path)) ?? new();
}

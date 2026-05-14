using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace LangSwitch.Core;

/// <summary>
/// Installs / uninstalls Windows keyboard layouts by writing to
/// HKCU\Keyboard Layout\Preload and calling LoadKeyboardLayout / UnloadKeyboardLayout.
/// </summary>
static class LayoutManager
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr LoadKeyboardLayout(string pwszKLID, uint Flags);

    [DllImport("user32.dll")]
    static extern bool UnloadKeyboardLayout(IntPtr hkl);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint Msg,
        IntPtr wParam, string lParam, uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

    const uint KLF_ACTIVATE = 0x00000001;
    const uint KLF_NOTELLSHELL = 0x00000080;
    const uint WM_SETTINGCHANGE = 0x001A;
    const uint SMTO_ABORTIFHUNG = 0x0002;
    static readonly IntPtr HWND_BROADCAST = new(0xFFFF);

    // ── Public API ──────────────────────────────────────────────────────────

    public static IReadOnlyList<AvailableLayout> GetAvailableLayouts()
    {
        var installed = GetInstalledKlids();
        var result = new List<AvailableLayout>();

        using var root = Registry.LocalMachine.OpenSubKey(
            @"SYSTEM\CurrentControlSet\Control\Keyboard Layouts");
        if (root is null) return result;

        foreach (var klid in root.GetSubKeyNames())
        {
            // Only standard keyboard layouts (0000xxxx), skip special input methods
            if (klid.Length != 8 || !klid.StartsWith("0000", StringComparison.OrdinalIgnoreCase))
                continue;

            using var sub = root.OpenSubKey(klid);
            string? name = sub?.GetValue("Layout Text") as string;
            if (string.IsNullOrWhiteSpace(name)) continue;

            if (!int.TryParse(klid[4..], NumberStyles.HexNumber, null, out int langId)) continue;

            CultureInfo culture;
            try { culture = new CultureInfo(langId); }
            catch { continue; }

            result.Add(new AvailableLayout
            {
                Klid = klid.ToUpperInvariant(),
                Name = name,
                FlagCode = ResolveFlagCode(culture),
                ShortCode = culture.TwoLetterISOLanguageName.ToUpperInvariant(),
                IsInstalled = installed.Contains(klid.ToUpperInvariant()),
            });
        }

        // Installed first, then alphabetical
        return result
            .OrderByDescending(l => l.IsInstalled)
            .ThenBy(l => l.Name)
            .ToList();
    }

    public static void Install(string klid)
    {
        LoadKeyboardLayout(klid, KLF_ACTIVATE | KLF_NOTELLSHELL);
        AddToPreload(klid.ToUpperInvariant());
        BroadcastChange();
    }

    /// <summary>Returns false if the layout cannot be unloaded (e.g. it is the only one).</summary>
    public static bool Uninstall(LayoutInfo layout)
    {
        bool ok = UnloadKeyboardLayout(layout.Handle);
        if (!ok) return false;

        string klid = KlidFromHandle(layout.Handle).ToUpperInvariant();
        RemoveFromPreload(klid);
        BroadcastChange();
        return true;
    }

    // ── Registry helpers ────────────────────────────────────────────────────

    static HashSet<string> GetInstalledKlids()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var key = Registry.CurrentUser.OpenSubKey(@"Keyboard Layout\Preload");
        if (key is null) return set;

        foreach (var name in key.GetValueNames())
            if (key.GetValue(name) is string klid)
                set.Add(klid.ToUpperInvariant());

        return set;
    }

    static void AddToPreload(string klid)
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Keyboard Layout\Preload", writable: true);
        if (key is null) return;

        // Skip if already present
        if (key.GetValueNames().Any(n =>
                string.Equals(key.GetValue(n)?.ToString(), klid, StringComparison.OrdinalIgnoreCase)))
            return;

        var indices = key.GetValueNames()
            .Where(n => int.TryParse(n, out _))
            .Select(int.Parse)
            .ToHashSet();

        int next = 1;
        while (indices.Contains(next)) next++;
        key.SetValue(next.ToString(), klid.ToLowerInvariant());
    }

    static void RemoveFromPreload(string klid)
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Keyboard Layout\Preload", writable: true);
        if (key is null) return;

        foreach (var name in key.GetValueNames())
        {
            if (string.Equals(key.GetValue(name)?.ToString(), klid, StringComparison.OrdinalIgnoreCase))
            {
                key.DeleteValue(name);
                return;
            }
        }
    }

    static string KlidFromHandle(IntPtr hkl)
    {
        int langId = (int)(hkl.ToInt64() & 0xFFFF);
        return $"0000{langId:X4}";
    }

    static void BroadcastChange()
    {
        SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE,
            IntPtr.Zero, "intl", SMTO_ABORTIFHUNG, 3000, out _);
    }

    // ── Culture helpers ─────────────────────────────────────────────────────

    static string ResolveFlagCode(CultureInfo culture)
    {
        if (!culture.IsNeutralCulture && !string.IsNullOrEmpty(culture.Name))
        {
            try { return new RegionInfo(culture.Name).TwoLetterISORegionName.ToLowerInvariant(); }
            catch { }
        }
        return culture.TwoLetterISOLanguageName.ToLowerInvariant() switch
        {
            "en" => "us",
            "ru" => "ru",
            "uk" => "ua",
            "de" => "de",
            "fr" => "fr",
            "es" => "es",
            "pl" => "pl",
            "be" => "by",
            "kk" => "kz",
            "ky" => "kg",
            "tg" => "tj",
            "uz" => "uz",
            "ka" => "ge",
            "hy" => "am",
            "az" => "az",
            "lt" => "lt",
            "lv" => "lv",
            "et" => "ee",
            var x => x,
        };
    }
}

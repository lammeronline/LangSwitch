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

    [DllImport("user32.dll")]
    static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);

    [DllImport("user32.dll")]
    static extern IntPtr ActivateKeyboardLayout(IntPtr hkl, uint uFlags);

    [DllImport("user32.dll")]
    static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, [MarshalAs(UnmanagedType.Bool)] bool fAttach);

    [DllImport("kernel32.dll")]
    static extern uint GetCurrentThreadId();

    [StructLayout(LayoutKind.Sequential)]
    struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    struct GUITHREADINFO
    {
        public int cbSize;
        public uint flags;
        public IntPtr hwndActive;
        public IntPtr hwndFocus;
        public IntPtr hwndCapture;
        public IntPtr hwndMenuOwner;
        public IntPtr hwndMoveSize;
        public IntPtr hwndCaret;
        public RECT rcCaret;
    }

    const uint KLF_ACTIVATE = 0x00000001;
    const uint KLF_NOTELLSHELL = 0x00000080;
    const uint WM_SETTINGCHANGE = 0x001A;
    const uint WM_INPUTLANGCHANGEREQUEST = 0x0050;
    const uint SMTO_ABORTIFHUNG = 0x0002;
    static readonly IntPtr HWND_BROADCAST = new(0xFFFF);

    // ── Public API ──────────────────────────────────────────────────────────

    /// <summary>
    /// The currently foreground window — callers that are about to show their own UI (a
    /// menu, a dialog) should capture this *before* doing so and pass it to
    /// <see cref="ActivateLayout"/>, since showing that UI briefly makes it the foreground
    /// window itself, which would otherwise receive the layout switch instead of the app
    /// the user was actually typing in.
    /// </summary>
    public static IntPtr CurrentForegroundWindow => GetForegroundWindow();

    /// <summary>True if the given window belongs to this process (our own tray/menu/dialog
    /// windows) — used to filter those out when tracking "the last real app window".</summary>
    public static bool BelongsToCurrentProcess(IntPtr hwnd)
    {
        GetWindowThreadProcessId(hwnd, out uint pid);
        return pid == (uint)Environment.ProcessId;
    }

    /// <summary>
    /// Switches the target window's keyboard layout. The keyboard layout is a per-thread
    /// input-queue attribute, so changing it for a thread other than our own requires
    /// temporarily attaching our input queue to that thread's (AttachThreadInput) before
    /// calling ActivateKeyboardLayout — the same mechanism real layout-switcher utilities
    /// use; posting WM_INPUTLANGCHANGEREQUEST alone relies on the target's own window
    /// procedure cooperating, which many apps (Electron/Chromium among them) simply never do.
    /// </summary>
    public static void ActivateLayout(LayoutInfo layout, IntPtr targetHwnd)
    {
        if (targetHwnd == IntPtr.Zero) return;

        uint targetThreadId  = GetWindowThreadProcessId(targetHwnd, out _);
        uint currentThreadId = GetCurrentThreadId();
        if (targetThreadId == 0) return;

        bool attached = targetThreadId != currentThreadId &&
            AttachThreadInput(currentThreadId, targetThreadId, true);
        try
        {
            ActivateKeyboardLayout(layout.Handle, 0);
        }
        finally
        {
            if (attached) AttachThreadInput(currentThreadId, targetThreadId, false);
        }

        // Courtesy notification for apps that do listen for it.
        var focusHwnd = GetFocusedChildWindow(targetHwnd) ?? targetHwnd;
        PostMessage(focusHwnd, WM_INPUTLANGCHANGEREQUEST, IntPtr.Zero, layout.Handle);
    }

    static IntPtr? GetFocusedChildWindow(IntPtr topLevelHwnd)
    {
        uint threadId = GetWindowThreadProcessId(topLevelHwnd, out _);
        var info = new GUITHREADINFO { cbSize = Marshal.SizeOf<GUITHREADINFO>() };
        if (!GetGUIThreadInfo(threadId, ref info)) return null;
        return info.hwndFocus == IntPtr.Zero ? null : info.hwndFocus;
    }

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

            var culture = CultureHelper.TryParse(langId);
            if (culture is null) continue;

            result.Add(new AvailableLayout
            {
                Klid = klid.ToUpperInvariant(),
                Name = name,
                FlagCode = FlagResolver.Resolve(culture),
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

    const string PreloadKeyPath = @"Keyboard Layout\Preload";

    static Microsoft.Win32.RegistryKey? OpenPreloadKey(bool writable = false) =>
        Registry.CurrentUser.OpenSubKey(PreloadKeyPath, writable);

    static HashSet<string> GetInstalledKlids()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var key = OpenPreloadKey();
        if (key is null) return set;

        foreach (var name in key.GetValueNames())
            if (key.GetValue(name) is string klid)
                set.Add(klid.ToUpperInvariant());

        return set;
    }

    static void AddToPreload(string klid)
    {
        using var key = OpenPreloadKey(writable: true);
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
        using var key = OpenPreloadKey(writable: true);
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

}

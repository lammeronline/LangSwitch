using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LangSwitch.Core;

static class WindowsLayoutDetector
{
    [DllImport("user32.dll")]
    static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    static extern IntPtr GetKeyboardLayout(uint idThread);

    public static LayoutInfo? GetForegroundLayout()
    {
        IntPtr hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return null;

        uint threadId = GetWindowThreadProcessId(hwnd, out _);
        IntPtr hkl = GetKeyboardLayout(threadId);
        if (hkl == IntPtr.Zero) return null;

        return FindByHandle(hkl);
    }

    public static LayoutInfo? FindByHandle(IntPtr hkl)
    {
        foreach (InputLanguage lang in InputLanguage.InstalledInputLanguages)
        {
            if (lang.Handle == hkl)
                return LayoutInfo.FromInputLanguage(lang);
        }
        return null;
    }

    public static IReadOnlyList<LayoutInfo> GetInstalledLayouts()
    {
        var list = new List<LayoutInfo>();
        foreach (InputLanguage lang in InputLanguage.InstalledInputLanguages)
            list.Add(LayoutInfo.FromInputLanguage(lang));
        return list;
    }

    /// <summary>Process name (no extension) of the app currently in the foreground, or null.</summary>
    public static string? GetForegroundProcessName()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return null;

        GetWindowThreadProcessId(hwnd, out uint pid);
        try
        {
            using var proc = Process.GetProcessById((int)pid);
            return proc.ProcessName;
        }
        catch (ArgumentException) { return null; } // process already exited
    }
}

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace LangSwitch.Core;

static class RunningWindowsEnumerator
{
    delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] static extern int  GetWindowTextLength(IntPtr hWnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);

    public static List<(string ProcessName, string Title, string? ExePath)> GetVisibleAppWindows()
    {
        var result = new List<(string ProcessName, string Title, string? ExePath)>();

        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd)) return true;

            int len = GetWindowTextLength(hwnd);
            if (len == 0) return true;

            var sb = new StringBuilder(len + 1);
            GetWindowText(hwnd, sb, sb.Capacity);
            var title = sb.ToString();
            if (string.IsNullOrWhiteSpace(title)) return true;

            GetWindowThreadProcessId(hwnd, out uint pid);
            try
            {
                using var proc = Process.GetProcessById((int)pid);
                // MainModule throws Win32Exception for processes we don't have access to
                // (elevated/other-user) — the exe path is a nice-to-have (for the app
                // picker's icon), not essential, so that failure alone shouldn't drop the
                // whole entry.
                string? exePath = null;
                try { exePath = proc.MainModule?.FileName; } catch { }
                result.Add((proc.ProcessName + ".exe", title, exePath));
            }
            catch { }

            return true;
        }, IntPtr.Zero);

        return result
            .GroupBy(x => x.ProcessName, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(x => x.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

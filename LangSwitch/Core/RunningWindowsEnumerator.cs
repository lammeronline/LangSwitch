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

    public static List<(string ProcessName, string Title)> GetVisibleAppWindows()
    {
        var result = new List<(string ProcessName, string Title)>();

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
                result.Add((proc.ProcessName + ".exe", title));
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

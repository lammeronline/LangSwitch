using System.Runtime.InteropServices;

namespace LangSwitch.Core;

/// <summary>
/// Detects whether the currently focused control is a native Win32 password field
/// (ES_PASSWORD style), so the hook can avoid buffering/autocorrecting password input.
/// Covers classic Win32/WinForms password edits (UAC, RDP, many desktop login forms).
/// Does NOT cover browser-rendered password fields (Chrome/Firefox/Edge draw their own,
/// not exposed as a native Edit control) — use Settings → Exclusions for those.
/// </summary>
static class PasswordFieldDetector
{
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

    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll")] static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    const int GWL_STYLE   = -16;
    const int ES_PASSWORD = 0x20;

    public static bool IsFocusedFieldPassword()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return false;

        uint threadId = GetWindowThreadProcessId(hwnd, out _);
        var info = new GUITHREADINFO { cbSize = Marshal.SizeOf<GUITHREADINFO>() };
        if (!GetGUIThreadInfo(threadId, ref info)) return false;
        if (info.hwndFocus == IntPtr.Zero) return false;

        int style = GetWindowLong(info.hwndFocus, GWL_STYLE);
        return (style & ES_PASSWORD) != 0;
    }
}

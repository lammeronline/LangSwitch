using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace LangSwitch.Core;

/// <summary>
/// Global low-level mouse hook that detects a scroll-wheel event while the cursor is over
/// the given NotifyIcon's tray icon, raising <see cref="Scrolled"/>. WinForms' NotifyIcon has
/// no public API for this — and Explorer only forwards WM_MOUSEWHEEL as a tray-icon callback
/// sub-message under NOTIFYICON_VERSION_4, which WinForms' NotifyIcon never opts into — so
/// this instead hooks mouse input globally and checks the cursor position against the icon's
/// actual screen rectangle via Shell_NotifyIconGetRect, which stays correct even when the
/// icon is tucked inside the "hidden icons" overflow flyout.
/// </summary>
sealed class TrayScrollHook : IDisposable
{
    [DllImport("user32.dll")] static extern IntPtr SetWindowsHookEx(int id, LowLevelProc fn, IntPtr mod, uint tid);
    [DllImport("user32.dll")] static extern bool   UnhookWindowsHookEx(IntPtr hk);
    [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hk, int code, IntPtr wp, IntPtr lp);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern IntPtr GetModuleHandle(string mod);

    [DllImport("shell32.dll")]
    static extern int Shell_NotifyIconGetRect(ref NOTIFYICONIDENTIFIER identifier, out RECT iconLocation);

    delegate IntPtr LowLevelProc(int code, IntPtr wp, IntPtr lp);

    [StructLayout(LayoutKind.Sequential)]
    struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    struct MSLLHOOKSTRUCT { public POINT pt; public uint mouseData, flags, time; public nint dwExtraInfo; }

    [StructLayout(LayoutKind.Sequential)]
    struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct NOTIFYICONIDENTIFIER { public uint cbSize; public IntPtr hWnd; public uint uID; public Guid guidItem; }

    const int WH_MOUSE_LL   = 14;
    const int WM_MOUSEWHEEL = 0x020A;

    readonly LowLevelProc _proc; // must be kept alive for the lifetime of the hook
    readonly IntPtr _hook;
    readonly IntPtr _iconHwnd;
    readonly uint _iconId;

    /// <summary>Positive = scrolled up/forward, negative = scrolled down/back.</summary>
    public event EventHandler<int>? Scrolled;

    public TrayScrollHook(NotifyIcon icon)
    {
        (_iconHwnd, _iconId) = GetIconIdentity(icon);
        _proc = Callback;
        using var m = Process.GetCurrentProcess().MainModule!;
        _hook = SetWindowsHookEx(WH_MOUSE_LL, _proc, GetModuleHandle(m.ModuleName!), 0);
        if (_hook == IntPtr.Zero)
            AppLog.Info("TrayScrollHook: SetWindowsHookEx failed — scroll-to-switch-layout will not work.");
    }

    IntPtr Callback(int code, IntPtr wp, IntPtr lp)
    {
        if (code >= 0 && wp == WM_MOUSEWHEEL && _iconHwnd != IntPtr.Zero)
        {
            var s = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lp);
            if (IsOverIcon(s.pt))
            {
                int delta = (short)(s.mouseData >> 16);
                Scrolled?.Invoke(this, delta);
            }
        }
        return CallNextHookEx(_hook, code, wp, lp);
    }

    bool IsOverIcon(POINT pt)
    {
        var id = new NOTIFYICONIDENTIFIER
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONIDENTIFIER>(),
            hWnd   = _iconHwnd,
            uID    = _iconId,
        };
        if (Shell_NotifyIconGetRect(ref id, out var rect) != 0) return false;
        return pt.X >= rect.Left && pt.X < rect.Right && pt.Y >= rect.Top && pt.Y < rect.Bottom;
    }

    /// <summary>
    /// NotifyIcon keeps its native window handle and Shell_NotifyIcon icon ID as private
    /// fields with no public accessor (named "_window"/"_id" as of .NET 8) — reflection is
    /// the only way to get at them.
    /// </summary>
    static (IntPtr hwnd, uint id) GetIconIdentity(NotifyIcon icon)
    {
        try
        {
            var windowField = typeof(NotifyIcon).GetField("_window", BindingFlags.Instance | BindingFlags.NonPublic);
            var idField     = typeof(NotifyIcon).GetField("_id", BindingFlags.Instance | BindingFlags.NonPublic);
            if (windowField?.GetValue(icon) is NativeWindow window && idField?.GetValue(icon) is uint id)
                return (window.Handle, id);
        }
        catch (Exception ex) { AppLog.Error(ex); }
        return (IntPtr.Zero, 0);
    }

    public void Dispose() => UnhookWindowsHookEx(_hook);
}

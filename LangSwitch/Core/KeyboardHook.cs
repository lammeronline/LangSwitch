using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LangSwitch.Core;

sealed class KeyboardHook : IDisposable
{
    [DllImport("user32.dll")] static extern IntPtr SetWindowsHookEx(int id, LowLevelProc fn, IntPtr mod, uint tid);
    [DllImport("user32.dll")] static extern bool   UnhookWindowsHookEx(IntPtr hk);
    [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hk, int code, IntPtr wp, IntPtr lp);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern IntPtr GetModuleHandle(string mod);

    delegate IntPtr LowLevelProc(int code, IntPtr wp, IntPtr lp);

    [StructLayout(LayoutKind.Sequential)]
    struct KBDLLHOOKSTRUCT { public uint vkCode, scanCode, flags, time; public nint dwExtraInfo; }

    const int WH_KEYBOARD_LL = 13;
    const int WM_KEYDOWN     = 0x100;
    const int WM_SYSKEYDOWN  = 0x104;

    // Stamp we put on SendInput events so the hook ignores its own injections
    public const nint InjectedMark = 0x4C53_4B48;

    readonly LowLevelProc _proc; // must be kept alive for the lifetime of the hook
    readonly IntPtr _hook;

    public event EventHandler<HookKeyEventArgs>? KeyDown;

    public KeyboardHook()
    {
        _proc = Callback;
        using var m = Process.GetCurrentProcess().MainModule!;
        _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(m.ModuleName!), 0);
    }

    IntPtr Callback(int code, IntPtr wp, IntPtr lp)
    {
        if (code >= 0 && (wp == WM_KEYDOWN || wp == WM_SYSKEYDOWN))
        {
            var s = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lp);
            if (s.dwExtraInfo != InjectedMark)
            {
                var e = new HookKeyEventArgs((Keys)s.vkCode, s.scanCode);
                KeyDown?.Invoke(this, e);
                if (e.Handled) return (IntPtr)1; // swallow the keystroke
            }
        }
        return CallNextHookEx(_hook, code, wp, lp);
    }

    public void Dispose() => UnhookWindowsHookEx(_hook);
}

sealed class HookKeyEventArgs(Keys key, uint scanCode) : EventArgs
{
    public Keys Key      { get; } = key;
    public uint ScanCode { get; } = scanCode;
    /// <summary>Set true to swallow the keystroke (prevent it from reaching the target app).</summary>
    public bool Handled  { get; set; }
}

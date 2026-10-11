using System.Runtime.InteropServices;
using System.Text;

namespace LangSwitch.Core;

/// <summary>
/// Classic auto-punctuation: a second consecutive space becomes a period
/// ("word  " → "word. "), and " - " becomes " — " (em dash) once the trailing space is
/// typed. Tracks only the last 2 committed characters — independent of
/// <see cref="WordTracker"/>'s word buffer, which is unrelated (punctuation can span word
/// boundaries). Duplicates WordTracker's key-to-char translation deliberately rather than
/// refactoring the already-working, tested WordTracker.
/// </summary>
sealed class AutoPunctuator
{
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint   GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll")] static extern IntPtr GetKeyboardLayout(uint tid);
    [DllImport("user32.dll")] static extern bool   GetKeyboardState(byte[] state);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int ToUnicodeEx(uint vk, uint scan, byte[] state,
        [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder buf, int bufLen, uint flags, IntPtr hkl);

    const uint DontChangeKeyboardState = 0x4; // peek the translation without disturbing a live dead-key sequence

    char _prev1, _prev2; // _prev1 = most recently committed char

    /// <summary>Returns true if this keystroke was handled (swallowed + replacement injected).</summary>
    public bool TryHandle(HookKeyEventArgs e)
    {
        switch (e.Key)
        {
            case Keys.Back:
            case Keys.Escape or Keys.Delete
              or Keys.Left or Keys.Right or Keys.Up or Keys.Down
              or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown:
                Reset();
                return false;
        }

        var keyState = new byte[256];
        GetKeyboardState(keyState);
        // See WordTracker.Feed for why AltGr (reported as synthetic Left-Ctrl+Right-Alt)
        // must not be treated as a real Ctrl combo here.
        bool ctrlDown = (keyState[(int)Keys.ControlKey] & 0x80) != 0;
        bool altGr    = (keyState[(int)Keys.RMenu] & 0x80) != 0;
        if (ctrlDown && !altGr) { Reset(); return false; }

        if (e.Key == Keys.Space)
        {
            if (_prev1 == ' ')
            {
                e.Handled = true;
                TextInjector.Replace(1, ". ");
                Reset();
                return true;
            }
            if (_prev1 == '-' && _prev2 == ' ')
            {
                e.Handled = true;
                TextInjector.Replace(1, "— ");
                Reset();
                return true;
            }

            Push(' ');
            return false;
        }

        // DontChangeKeyboardState: this is a peek, not a commit — WordTracker's own
        // ToUnicodeEx call (which does commit) is the one source of truth for dead-key state.
        var sb = new StringBuilder(4);
        int res = ToUnicodeEx((uint)e.Key, e.ScanCode, keyState, sb, 4, DontChangeKeyboardState, ForegroundLayout());
        if (res == 1) Push(sb[0]);

        return false;
    }

    void Push(char c) { _prev2 = _prev1; _prev1 = c; }
    void Reset() { _prev1 = _prev2 = '\0'; }

    static IntPtr ForegroundLayout()
    {
        var hwnd = GetForegroundWindow();
        uint tid = GetWindowThreadProcessId(hwnd, out _);
        return GetKeyboardLayout(tid);
    }
}

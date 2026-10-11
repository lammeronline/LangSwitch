using System.Runtime.InteropServices;
using System.Text;

namespace LangSwitch.Core;

/// <summary>
/// Maintains a buffer of the word currently being typed and fires WordReady
/// when a word boundary (space, punctuation, Enter…) is detected.
/// </summary>
sealed class WordTracker
{
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint   GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll")] static extern IntPtr GetKeyboardLayout(uint tid);
    [DllImport("user32.dll")] static extern bool   GetKeyboardState(byte[] state);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int ToUnicodeEx(uint vk, uint scan, byte[] state,
        [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder buf, int bufLen, uint flags, IntPtr hkl);

    readonly StringBuilder _buf = new();

    /// <summary>Fires when a word is complete (boundary key was pressed).</summary>
    public event EventHandler<WordReadyEventArgs>? WordReady;

    public string Current => _buf.ToString();

    public void Feed(HookKeyEventArgs e)
    {
        switch (e.Key)
        {
            case Keys.Back:
                if (_buf.Length > 0) _buf.Length--;
                return;

            case Keys.Escape or Keys.Delete or Keys.Tab
              or Keys.Left or Keys.Right or Keys.Up or Keys.Down
              or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown:
                _buf.Clear();
                return;
        }

        var keyState = new byte[256];
        GetKeyboardState(keyState);

        // Ctrl+anything resets the word (copy, paste, undo, etc.) — but AltGr (used to type
        // @/€/other symbols on many non-US layouts) is reported by Windows as a synthetic
        // Left-Ctrl+Right-Alt chord, so checking Ctrl alone would wrongly discard the
        // in-progress word on every AltGr keystroke. Only reset for a "real" Ctrl combo.
        bool ctrlDown = (keyState[(int)Keys.ControlKey] & 0x80) != 0;
        bool altGr    = (keyState[(int)Keys.RMenu] & 0x80) != 0;
        if (ctrlDown && !altGr) { _buf.Clear(); return; }

        var sb  = new StringBuilder(4);
        int res = ToUnicodeEx((uint)e.Key, e.ScanCode, keyState, sb, 4, 0, ForegroundLayout());

        if (res != 1) return; // dead key, compose sequence, or no mapping

        char ch = sb[0];

        if (IsWordChar(ch))
        {
            _buf.Append(ch);
        }
        else if (_buf.Length > 0)
        {
            var word = _buf.ToString();
            _buf.Clear();
            WordReady?.Invoke(this, new WordReadyEventArgs(word, ch, e));
        }
    }

    public void Clear() => _buf.Clear();

    /// <summary>Replaces the buffer with a known word (e.g. after hotkey conversion).</summary>
    public void Set(string word) { _buf.Clear(); _buf.Append(word); }

    static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '\'' || c == '-';

    static IntPtr ForegroundLayout()
    {
        var hwnd = GetForegroundWindow();
        uint tid = GetWindowThreadProcessId(hwnd, out _);
        return GetKeyboardLayout(tid);
    }
}

sealed class WordReadyEventArgs(string word, char boundary, HookKeyEventArgs hookArgs) : EventArgs
{
    public string          Word     { get; } = word;
    public char            Boundary { get; } = boundary;
    /// <summary>The hook event for the boundary keystroke — set Handled=true to swallow it.</summary>
    public HookKeyEventArgs HookArgs { get; } = hookArgs;
}

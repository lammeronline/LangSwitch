using System.Runtime.InteropServices;

namespace LangSwitch.Core;

/// <summary>
/// Sends synthetic keystrokes to the active window using SendInput,
/// stamped with InjectedMark so the keyboard hook ignores them.
/// </summary>
static class TextInjector
{
    [StructLayout(LayoutKind.Sequential)] struct INPUT { public uint type; public INPUTUNION u; }
    [StructLayout(LayoutKind.Explicit, Size = 32)] struct INPUTUNION { [FieldOffset(0)] public KEYBDINPUT ki; }
    [StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT
    {
        public ushort wVk, wScan;
        public uint   dwFlags, time;
        public nint   dwExtraInfo;
    }

    [DllImport("user32.dll")] static extern uint SendInput(uint n, INPUT[] arr, int cb);

    const uint INPUT_KEYBOARD    = 1;
    const uint KEYEVENTF_KEYUP   = 0x0002;
    const uint KEYEVENTF_UNICODE = 0x0004;

    /// <summary>Sends a single virtual-key down+up, stamped with InjectedMark.</summary>
    public static void SendVk(ushort vk)
    {
        var arr = new[] { VkInput(vk, 0), VkInput(vk, KEYEVENTF_KEYUP) };
        SendInput(2, arr, Marshal.SizeOf<INPUT>());
    }

    /// <summary>
    /// Sends a key chord (all keys down in order, then all up in reverse),
    /// e.g. Ctrl+C: SendChord(0x11, 0x43).
    /// </summary>
    public static void SendChord(params ushort[] vks)
    {
        var inputs = new List<INPUT>(vks.Length * 2);
        foreach (var vk in vks)             inputs.Add(VkInput(vk, 0));
        for (int i = vks.Length - 1; i >= 0; i--) inputs.Add(VkInput(vks[i], KEYEVENTF_KEYUP));
        SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<INPUT>());
    }

    /// <summary>
    /// Erases <paramref name="deleteCount"/> characters with Backspace,
    /// then types <paramref name="newText"/> as Unicode keystrokes.
    /// </summary>
    public static void Replace(int deleteCount, string newText)
    {
        var inputs = new List<INPUT>(deleteCount * 2 + newText.Length * 2);

        for (int i = 0; i < deleteCount; i++)
        {
            inputs.Add(VkInput(0x08, 0));              // Backspace down
            inputs.Add(VkInput(0x08, KEYEVENTF_KEYUP)); // Backspace up
        }
        foreach (char c in newText)
        {
            inputs.Add(CharInput(c, 0));
            inputs.Add(CharInput(c, KEYEVENTF_KEYUP));
        }

        SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<INPUT>());
    }

    static INPUT VkInput(ushort vk, uint flags) => new()
    {
        type = INPUT_KEYBOARD,
        u    = new() { ki = new() { wVk = vk, dwFlags = flags, dwExtraInfo = KeyboardHook.InjectedMark } }
    };

    static INPUT CharInput(char c, uint extraFlags) => new()
    {
        type = INPUT_KEYBOARD,
        u    = new() { ki = new() { wScan = c, dwFlags = KEYEVENTF_UNICODE | extraFlags, dwExtraInfo = KeyboardHook.InjectedMark } }
    };
}

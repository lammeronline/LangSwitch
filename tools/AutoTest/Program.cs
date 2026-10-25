// Self-test harness for the manual conversion hotkey, driven against a real Notepad++
// instance so the global keyboard hook sees genuine SendInput keystrokes (not stamped with
// KeyboardHook.InjectedMark, so they aren't ignored the way LangSwitch's own text-replacement
// keystrokes are). Requires LangSwitch.exe to already be running with its hook active.
//
// Usage: dotnet run --project tools/AutoTest -- "<typed text>" "<expected result, optional>"

using System.Diagnostics;
using System.Runtime.InteropServices;
using LangSwitch.Core;

namespace AutoTest;

static class Program
{
    const string NppPath = @"C:\Program Files\Notepad++\notepad++.exe";

    static void DictCheck(string[] words)
    {
        var dicts = new LanguageDictionaries(new[]
        {
            new UserDictionary { Lang = "ru", Enabled = true },
            new UserDictionary { Lang = "en", Enabled = true },
            new UserDictionary { Lang = "uk", Enabled = true },
        });
        foreach (var w in words)
        {
            Console.WriteLine($"'{w}': ru={dicts.Contains("ru", w)} en={dicts.Contains("en", w)} uk={dicts.Contains("uk", w)} ContainsAny={dicts.ContainsAny(w)} source(ru)={dicts.SourceOf("ru")}");
        }
    }

    [STAThread]
    static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--dict-check")
        {
            DictCheck(args.Skip(1).ToArray());
            return;
        }

        string typed    = args.Length > 0 ? args[0] : "Ghbdtn Fylhtq!";
        string expected = args.Length > 1 ? args[1] : "";

        if (!File.Exists(NppPath))
        {
            Console.WriteLine($"Notepad++ not found at {NppPath}");
            return;
        }

        var psi = new ProcessStartInfo(NppPath, "-multiInst -nosession -noPlugin") { UseShellExecute = true };
        var proc = Process.Start(psi)!;

        IntPtr hwnd = IntPtr.Zero;
        for (int i = 0; i < 100 && hwnd == IntPtr.Zero; i++)
        {
            Thread.Sleep(50);
            proc.Refresh();
            hwnd = proc.MainWindowHandle;
        }
        if (hwnd == IntPtr.Zero)
        {
            Console.WriteLine("Timed out waiting for Notepad++ window.");
            return;
        }

        SetForegroundWindow(hwnd);
        Thread.Sleep(400);

        // Safety check: if the real foreground window isn't the one we just launched, abort
        // instead of typing blindly into whatever the user actually has open.
        IntPtr actualFg = GetForegroundWindow();
        if (actualFg != hwnd)
        {
            Console.WriteLine($"ABORT: expected foreground window {hwnd} (our Notepad++), but actual foreground is {actualFg}. Not typing anything.");
            try { proc.Kill(); } catch { }
            return;
        }

        // Force the US-English layout on this window before typing — otherwise SendInput's
        // plain VK codes get translated through whatever layout is actually active (e.g.
        // Russian), producing correct Cyrillic text directly instead of the Latin "wrong
        // layout" gibberish this test is supposed to reproduce.
        IntPtr enUs = LoadKeyboardLayout("00000409", 1 /*KLF_ACTIVATE*/);
        PostMessage(hwnd, WM_INPUTLANGCHANGEREQUEST, IntPtr.Zero, enUs);
        Thread.Sleep(200);

        Console.WriteLine($"Typing: {typed}");
        foreach (char c in typed)
        {
            if (GetForegroundWindow() != hwnd)
            {
                Console.WriteLine("ABORT: foreground window changed mid-typing. Stopping immediately.");
                try { proc.Kill(); } catch { }
                return;
            }
            TypeChar(c);
            Thread.Sleep(15);
        }

        Thread.Sleep(400);

        Console.WriteLine("Pressing convert hotkey (Home)...");
        SendVk(0x24); // VK_HOME — matches ConvertHotKey=36 in settings.json; adjust if the hotkey differs

        Thread.Sleep(900); // TryConvertSelectionAsync does several awaited clipboard round-trips

        // Select-all + copy to read back the result without touching Scintilla cross-process APIs.
        SendChord(0x11, 0x41); // Ctrl+A
        Thread.Sleep(100);
        SendChord(0x11, 0x43); // Ctrl+C
        Thread.Sleep(200);

        string result = Clipboard.ContainsText() ? Clipboard.GetText() : "<clipboard empty>";
        Console.WriteLine($"Result:  {result}");

        bool ok = expected.Length > 0 ? result.Trim() == expected : !LooksDuplicated(typed, result);
        Console.WriteLine(ok ? "PASS" : "FAIL");

        // Close without saving.
        SendChord(0x11, 0x57); // Ctrl+W
        Thread.Sleep(300);
        SendVk(0x4E); // 'N' — in case a "Save changes?" dialog appeared, answer No. Harmless if not.
        try { if (!proc.HasExited) proc.Kill(); } catch { }
    }

    // A correct conversion either replaces the typed text in place or replaces just the last
    // word — either way the result should never be longer than the original typed text plus a
    // little slack for case/char-count drift. A duplication bug makes the result roughly double
    // the length (original text left in place, converted text appended alongside it).
    static bool LooksDuplicated(string typed, string result) =>
        result.Replace("\r", "").Replace("\n", "").Length > typed.Length + 4;

    static void TypeChar(char c)
    {
        bool needsShift = char.IsUpper(c) || "!@#$%^&*()_+{}|:\"<>?~".Contains(c);
        ushort vk = VkForChar(c);
        if (vk == 0) return;

        if (needsShift) { SendKeyDown(0x10); Thread.Sleep(60); } // Shift — give GetKeyboardState time to observe it
        SendVk(vk);
        Thread.Sleep(40);
        if (needsShift) { SendKeyUp(0x10); Thread.Sleep(40); }
    }

    static ushort VkForChar(char c) => char.ToUpperInvariant(c) switch
    {
        ' ' => 0x20,
        '!' => 0x31, // Shift+1
        >= 'A' and <= 'Z' => (ushort)char.ToUpperInvariant(c),
        _ => 0
    };

    const uint WM_INPUTLANGCHANGEREQUEST = 0x0050;

    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint SendInput(uint n, INPUT[] arr, int cb);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr LoadKeyboardLayout(string pwszKLID, uint Flags);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)] struct INPUT { public uint type; public INPUTUNION u; }
    [StructLayout(LayoutKind.Explicit, Size = 32)] struct INPUTUNION { [FieldOffset(0)] public KEYBDINPUT ki; }
    [StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public nint dwExtraInfo; }

    const uint INPUT_KEYBOARD  = 1;
    const uint KEYEVENTF_KEYUP = 0x0002;

    static void SendVk(ushort vk) => SendInput(2, new[] { Mk(vk, 0), Mk(vk, KEYEVENTF_KEYUP) }, Marshal.SizeOf<INPUT>());
    static void SendKeyDown(ushort vk) => SendInput(1, new[] { Mk(vk, 0) }, Marshal.SizeOf<INPUT>());
    static void SendKeyUp(ushort vk) => SendInput(1, new[] { Mk(vk, KEYEVENTF_KEYUP) }, Marshal.SizeOf<INPUT>());

    static void SendChord(params ushort[] vks)
    {
        var inputs = new List<INPUT>(vks.Length * 2);
        foreach (var vk in vks) inputs.Add(Mk(vk, 0));
        for (int i = vks.Length - 1; i >= 0; i--) inputs.Add(Mk(vks[i], KEYEVENTF_KEYUP));
        SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<INPUT>());
    }

    // No KeyboardHook.InjectedMark stamp here — these must look like real hardware input so
    // LangSwitch's global hook (and WordTracker's own ToUnicodeEx translation) picks them up
    // exactly the way a human typing would.
    static INPUT Mk(ushort vk, uint flags) => new()
    {
        type = INPUT_KEYBOARD,
        u    = new() { ki = new() { wVk = vk, dwFlags = flags, dwExtraInfo = 0 } }
    };
}

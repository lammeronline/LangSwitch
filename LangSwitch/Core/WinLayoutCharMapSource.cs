using System.Runtime.InteropServices;
using System.Text;

namespace LangSwitch.Core;

/// <summary>
/// Builds a physical-key character map for every installed Windows keyboard layout by
/// asking Windows itself (<see cref="ToUnicodeEx"/>) what each physical key produces under
/// that layout — the same API <see cref="WordTracker"/> uses to translate real keystrokes.
/// No per-language data is hardcoded; any layout the user installs via Settings → Languages
/// becomes usable automatically.
/// </summary>
sealed class WinLayoutCharMapSource : ILayoutCharMapSource
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int ToUnicodeEx(uint vk, uint scan, byte[] state,
        [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder buf, int bufLen, uint flags, IntPtr hkl);

    [DllImport("user32.dll")]
    static extern uint MapVirtualKey(uint vk, uint mapType);

    const uint MAPVK_VK_TO_VSC = 0;
    const uint DontChangeKeyboardState = 0x4; // avoids disturbing a live dead-key sequence
    const int VK_SHIFT = 0x10;

    // The physical keys the old hardcoded tables covered, keyed by their US/QWERTY baseline char.
    static readonly (ushort Vk, char Baseline)[] ProbeKeys =
    [
        (0xC0, '`'),
        (0x51, 'q'), (0x57, 'w'), (0x45, 'e'), (0x52, 'r'), (0x54, 't'),
        (0x59, 'y'), (0x55, 'u'), (0x49, 'i'), (0x4F, 'o'), (0x50, 'p'),
        (0xDB, '['), (0xDD, ']'),
        (0x41, 'a'), (0x53, 's'), (0x44, 'd'), (0x46, 'f'), (0x47, 'g'),
        (0x48, 'h'), (0x4A, 'j'), (0x4B, 'k'), (0x4C, 'l'),
        (0xBA, ';'), (0xDE, '\''),
        (0x5A, 'z'), (0x58, 'x'), (0x43, 'c'), (0x56, 'v'), (0x42, 'b'),
        (0x4E, 'n'), (0x4D, 'm'),
        (0xBC, ','), (0xBE, '.'), (0xBF, '/'),
    ];

    IReadOnlyList<IntPtr> _cachedHandles = [];
    IReadOnlyDictionary<string, IReadOnlyDictionary<char, char>> _cachedMaps =
        new Dictionary<string, IReadOnlyDictionary<char, char>>();

    public IReadOnlyDictionary<string, IReadOnlyDictionary<char, char>> GetInstalledLayoutMaps()
    {
        var layouts = WindowsLayoutDetector.GetInstalledLayouts();
        var handles = layouts.Select(l => l.Handle).ToList();

        if (!handles.SequenceEqual(_cachedHandles))
        {
            _cachedHandles = handles;
            _cachedMaps = Build(layouts);
        }

        return _cachedMaps;
    }

    static Dictionary<string, IReadOnlyDictionary<char, char>> Build(IReadOnlyList<LayoutInfo> layouts)
    {
        var result = new Dictionary<string, IReadOnlyDictionary<char, char>>();
        foreach (var layout in layouts)
        {
            var lang = layout.Culture.TwoLetterISOLanguageName.ToLowerInvariant();
            if (result.ContainsKey(lang)) continue; // one map per language code
            result[lang] = BuildMap(layout.Handle);
        }
        return result;
    }

    static Dictionary<char, char> BuildMap(IntPtr hkl)
    {
        var map = new Dictionary<char, char>();
        var lowerState = new byte[256];
        var upperState = new byte[256];
        upperState[VK_SHIFT] = 0x80;

        foreach (var (vk, baseline) in ProbeKeys)
        {
            uint scan = MapVirtualKey(vk, MAPVK_VK_TO_VSC);

            if (TryTranslate(vk, scan, lowerState, hkl, out char lower))
                map[baseline] = lower;
            if (TryTranslate(vk, scan, upperState, hkl, out char upper) && char.IsLetter(baseline))
                map[char.ToUpperInvariant(baseline)] = upper;
        }
        return map;
    }

    static bool TryTranslate(uint vk, uint scan, byte[] state, IntPtr hkl, out char ch)
    {
        var buf = new StringBuilder(4);
        int res = ToUnicodeEx(vk, scan, state, buf, 4, DontChangeKeyboardState, hkl);
        if (res == 1) { ch = buf[0]; return true; }
        ch = '\0';
        return false;
    }
}

namespace LangSwitch.Core;

/// <summary>
/// Physical-key layout conversion tables: Latin ↔ Russian / Ukrainian.
/// Ported from the original LanguageSwitcher project.
/// </summary>
static class LayoutConverter
{
    static readonly Dictionary<char, char> LatinToRussian = new()
    {
        ['`']='ё',['q']='й',['w']='ц',['e']='у',['r']='к',['t']='е',['y']='н',['u']='г',
        ['i']='ш',['o']='щ',['p']='з',['[']='х',[']']='ъ',['a']='ф',['s']='ы',['d']='в',
        ['f']='а',['g']='п',['h']='р',['j']='о',['k']='л',['l']='д',[';']='ж',['\'']='э',
        ['z']='я',['x']='ч',['c']='с',['v']='м',['b']='и',['n']='т',['m']='ь',[',']='б',
        ['.']='ю',['/']='.',
    };

    static readonly Dictionary<char, char> LatinToUkrainian = new()
    {
        ['`']='ґ',['q']='й',['w']='ц',['e']='у',['r']='к',['t']='е',['y']='н',['u']='г',
        ['i']='ш',['o']='щ',['p']='з',['[']='х',[']']='ї',['a']='ф',['s']='і',['d']='в',
        ['f']='а',['g']='п',['h']='р',['j']='о',['k']='л',['l']='д',[';']='ж',['\'']='є',
        ['z']='я',['x']='ч',['c']='с',['v']='м',['b']='и',['n']='т',['m']='ь',[',']='б',
        ['.']='ю',['/']='.',
    };

    static readonly Dictionary<char, char> CyrillicToLatin;

    static LayoutConverter()
    {
        AddUppercase(LatinToRussian);
        AddUppercase(LatinToUkrainian);
        CyrillicToLatin = BuildCyrillicToLatin();
    }

    public static string ConvertToOppositeLayout(string text) => ConvertToBestLayout(text);

    public static string ConvertToBestLayout(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var candidates = GetCandidates(text).Distinct().Where(c => c != text).ToArray();
        return candidates.Length == 0 ? text : candidates[0];
    }

    public static string ConvertToLayout(string text, string targetLang)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return targetLang switch
        {
            "en" => Apply(text, CyrillicToLatin),
            "ru" => Apply(text, LatinToRussian),
            "uk" => Apply(text, LatinToUkrainian),
            _    => ConvertToBestLayout(text),
        };
    }

    /// <summary>Returns all layout-converted candidates for the given word.</summary>
    public static IEnumerable<(string Word, string Lang)> GetAllCandidates(string text)
    {
        if (text.Any(IsLatin))
        {
            yield return (Apply(text, LatinToRussian),    "ru");
            yield return (Apply(text, LatinToUkrainian),  "uk");
        }
        if (text.Any(IsCyrillic))
            yield return (Apply(text, CyrillicToLatin), "en");
    }

    static IEnumerable<string> GetCandidates(string text)
    {
        foreach (var (w, _) in GetAllCandidates(text)) yield return w;
    }

    static string Apply(string text, IReadOnlyDictionary<char, char> map) =>
        new(text.Select(c => map.GetValueOrDefault(c, c)).ToArray());

    static Dictionary<char, char> BuildCyrillicToLatin()
    {
        var map = new Dictionary<char, char>();
        foreach (var pair in LatinToRussian.Concat(LatinToUkrainian))
            map[pair.Value] = pair.Key;

        // Ukrainian-specific overrides
        map['і'] = 's'; map['І'] = 'S';
        map['ї'] = ']'; map['Ї'] = ']';
        map['є'] = '\''; map['Є'] = '\'';
        map['ґ'] = '`'; map['Ґ'] = '`';

        AddUppercase(map);
        return map;
    }

    static void AddUppercase(Dictionary<char, char> map)
    {
        foreach (var pair in map.ToArray())
            if (char.IsLetter(pair.Key))
                map[char.ToUpperInvariant(pair.Key)] = char.ToUpperInvariant(pair.Value);
    }

    public static bool IsLatin(char c)    => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
    public static bool IsCyrillic(char c) => c is >= 'Ѐ' and <= 'ӿ';
}

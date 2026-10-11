using System.Globalization;

namespace LangSwitch.Core;

/// <summary>
/// Shared "is this character part of a word" check used both when building a word list
/// (<see cref="WordListDownloader"/>) and when normalizing a typed word for dictionary lookup
/// (<see cref="LanguageDictionaries"/>) — they must agree, or a word gets written to disk one
/// way and looked up a different way and never matches.
///
/// char.IsLetter alone rejects combining marks (Unicode category Mn/Mc/Me) — diacritics that
/// are not full characters by themselves but attach to the base letter before them. Several
/// scripts (Thai, Bengali, Malayalam, and others) rely on these constantly, so a plain
/// "all chars are letters" filter throws out most real words in those languages.
/// </summary>
static class TextNormalization
{
    public static bool IsWordChar(char c) =>
        char.IsLetter(c) || char.GetUnicodeCategory(c) is
            UnicodeCategory.NonSpacingMark or
            UnicodeCategory.SpacingCombiningMark or
            UnicodeCategory.EnclosingMark;
}

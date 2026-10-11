using LangSwitch.Core;

namespace LangSwitch.Tests;

public class TextNormalizationTests
{
    [Fact]
    public void IsWordChar_PlainLetter_ReturnsTrue()
    {
        Assert.True(TextNormalization.IsWordChar('a'));
        Assert.True(TextNormalization.IsWordChar('п')); // Cyrillic
    }

    [Fact]
    public void IsWordChar_CombiningMark_ReturnsTrue()
    {
        // U+0301 COMBINING ACUTE ACCENT — attaches to the letter before it (e.g. decomposed
        // "é" = 'e' + U+0301). Several scripts (Thai, Bengali, Malayalam...) rely on
        // combining marks like this constantly; a plain char.IsLetter check rejects them.
        Assert.True(TextNormalization.IsWordChar('́'));
    }

    [Fact]
    public void IsWordChar_Digit_ReturnsFalse()
    {
        Assert.False(TextNormalization.IsWordChar('5'));
    }

    [Fact]
    public void IsWordChar_Punctuation_ReturnsFalse()
    {
        Assert.False(TextNormalization.IsWordChar('.'));
        Assert.False(TextNormalization.IsWordChar(' '));
    }

    [Fact]
    public void WordWithCombiningMark_SurvivesFullWordFilter()
    {
        // Decomposed "café": c-a-f-e + combining acute accent (5 UTF-16 chars).
        var word = "café";
        Assert.True(word.All(TextNormalization.IsWordChar));
    }
}

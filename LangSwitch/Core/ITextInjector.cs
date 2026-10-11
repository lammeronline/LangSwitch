namespace LangSwitch.Core;

/// <summary>
/// Seam over <see cref="TextInjector"/> so orchestration logic (e.g. AutocorrectEngine)
/// can be unit-tested with a fake instead of sending real synthetic keystrokes.
/// </summary>
interface ITextInjector
{
    void Replace(int deleteCount, string newText);
}

sealed class WinTextInjector : ITextInjector
{
    public void Replace(int deleteCount, string newText) => TextInjector.Replace(deleteCount, newText);
}

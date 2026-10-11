namespace LangSwitch.Core;

/// <summary>Generates wrong-layout conversion candidates for a typed word.</summary>
interface ILayoutCandidateProvider
{
    /// <summary>All plausible reinterpretations of <paramref name="word"/> under every
    /// other installed layout, each tagged with the target layout's language code.</summary>
    IEnumerable<(string Word, string Lang)> GetAllCandidates(string word);

    /// <summary>The first candidate that differs from <paramref name="text"/>, or
    /// <paramref name="text"/> unchanged if none does.</summary>
    string ConvertToOppositeLayout(string text);
}

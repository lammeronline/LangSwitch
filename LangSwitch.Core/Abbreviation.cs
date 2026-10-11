namespace LangSwitch.Core;

public sealed class Abbreviation
{
    public string Trigger   { get; set; } = "";
    public string Expansion { get; set; } = "";
    public bool   Enabled   { get; set; } = true;

    public override string ToString() =>
        string.IsNullOrEmpty(Trigger) ? "New abbreviation" : $"{Trigger} → {Expansion}";
}

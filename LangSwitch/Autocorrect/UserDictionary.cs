namespace LangSwitch.Autocorrect;

sealed class UserDictionary
{
    public string Name    { get; set; } = "Dictionary";
    public string Lang    { get; set; } = "";
    public bool   Enabled { get; set; } = true;
    public string Url     { get; set; } = "";

    public override string ToString() =>
        string.IsNullOrEmpty(Lang) ? Name : $"{Name} ({Lang.ToUpperInvariant()})";
}

namespace LangSwitch.Settings;

public class AppSettings
{
    public FlagDisplaySize PopupSize         { get; set; } = FlagDisplaySize.Medium;
    public ScreenCorner    PopupPosition     { get; set; } = ScreenCorner.BottomRight;
    public int             PopupDurationMs   { get; set; } = 1500;
    public bool            AutocorrectEnabled  { get; set; } = true;
    public bool            NotifyWrongLayout   { get; set; } = false;
    public bool            FixCapsLock         { get; set; } = true;
    public Keys            ConvertHotKey       { get; set; } = Keys.None;
    public int             ClipboardDelayMs    { get; set; } = 20;

    public List<string> ExcludedApps { get; set; } =
    [
        "cmd.exe", "powershell.exe", "pwsh.exe", "WindowsTerminal.exe", "Code.exe", "devenv.exe",
    ];
}

public enum FlagDisplaySize
{
    Small = 64,
    Medium = 96,
    Large = 128,
}

public enum ScreenCorner
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
}

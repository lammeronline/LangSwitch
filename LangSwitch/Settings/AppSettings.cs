namespace LangSwitch.Settings;

public class AppSettings
{
    public FlagDisplaySize PopupSize         { get; set; } = FlagDisplaySize.Medium;
    public ScreenCorner    PopupPosition     { get; set; } = ScreenCorner.BottomRight;
    public int             PopupDurationMs   { get; set; } = 1500;
    public bool            AutocorrectEnabled  { get; set; } = true;
    public bool            NotifyWrongLayout   { get; set; } = false;
    public bool            FixCapsLock         { get; set; } = true;
    public bool            AutoPunctuation     { get; set; } = false;
    public int             UndoLearnThreshold  { get; set; } = 3;
    /// <summary>Empty means "not resolved yet" — TrayApplicationContext picks a default from
    /// the system locale on first run and persists it.</summary>
    public string          UiLanguage          { get; set; } = "";
    public Keys            ConvertHotKey       { get; set; } = Keys.None;
    public int             ClipboardDelayMs    { get; set; } = 20;
    public bool            ScrollToSwitchLayout { get; set; } = false;

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

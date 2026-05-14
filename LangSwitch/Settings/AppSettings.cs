namespace LangSwitch.Settings;

public class AppSettings
{
    public FlagDisplaySize PopupSize { get; set; } = FlagDisplaySize.Medium;
    public ScreenCorner PopupPosition { get; set; } = ScreenCorner.BottomRight;
    public int PopupDurationMs { get; set; } = 1500;
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

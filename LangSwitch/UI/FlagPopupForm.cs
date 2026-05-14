using System.Runtime.InteropServices;
using LangSwitch.Core;
using LangSwitch.Settings;

namespace LangSwitch.UI;

sealed class FlagPopupForm : Form
{
    // ── WinAPI: keep topmost without activating ───────────────────────────────
    [DllImport("user32.dll")]
    static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int x, int y, int cx, int cy, SetWindowPosFlags uFlags);

    [Flags]
    enum SetWindowPosFlags : uint { NoActivate = 0x0010, ShowWindow = 0x0040 }

    static readonly IntPtr HWND_TOPMOST = new(-1);

    // ── DWM: rounded corners + border colour (Windows 11) ────────────────────
    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    const int DWMWA_BORDER_COLOR             = 34;
    const int DWMWCP_ROUND                   = 2;
    // COLORREF is 0x00BBGGRR — a muted dark-gray border
    const int BorderColorRef                 = 0x00505050;

    // ── Layout constants ──────────────────────────────────────────────────────
    const int FormWidth   = 180;
    const int LabelHeight = 26;
    const int Pad         = 12;

    readonly PictureBox _flagBox;
    readonly Label _label;
    readonly System.Windows.Forms.Timer _hideTimer;
    Image? _currentImage;

    AppSettings _settings;

    public FlagPopupForm(AppSettings settings)
    {
        _settings = settings;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar   = false;
        StartPosition   = FormStartPosition.Manual;
        TopMost         = true;
        BackColor       = Color.FromArgb(24, 26, 32);
        Opacity         = 0.80;

        _flagBox = new PictureBox
        {
            Dock     = DockStyle.Top,
            Height   = (int)settings.PopupSize,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent,
        };

        _label = new Label
        {
            Dock      = DockStyle.Bottom,
            Height    = LabelHeight,
            TextAlign = ContentAlignment.MiddleCenter,
            Font      = new Font("Segoe UI", 12, FontStyle.Bold),
            ForeColor = Color.FromArgb(220, 220, 220),
            BackColor = Color.Transparent,
        };

        _hideTimer = new System.Windows.Forms.Timer { Interval = settings.PopupDurationMs };
        _hideTimer.Tick += (_, _) => { _hideTimer.Stop(); Hide(); };

        Controls.Add(_label);
        Controls.Add(_flagBox);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        int corner = DWMWCP_ROUND;
        DwmSetWindowAttribute(Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));

        int border = BorderColorRef;
        DwmSetWindowAttribute(Handle, DWMWA_BORDER_COLOR, ref border, sizeof(int));
    }

    protected override CreateParams CreateParams
    {
        get
        {
            const int WS_EX_NOACTIVATE = 0x08000000;
            const int WS_EX_TOOLWINDOW = 0x00000080;
            const int WS_EX_TOPMOST    = 0x00000008;
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST;
            return cp;
        }
    }

    protected override bool ShowWithoutActivation => true;

    public void ShowLayout(LayoutInfo layout)
    {
        _hideTimer.Stop();
        ApplySize();
        SetFlag(layout);
        _label.Text = layout.ShortCode;
        Location = CalcLocation();

        SetWindowPos(Handle, HWND_TOPMOST, Left, Top, Width, Height,
            SetWindowPosFlags.NoActivate | SetWindowPosFlags.ShowWindow);
        Show();

        _hideTimer.Interval = _settings.PopupDurationMs;
        _hideTimer.Start();
    }

    public void ApplySettings(AppSettings settings)
    {
        _settings = settings;
        _hideTimer.Interval = settings.PopupDurationMs;
    }

    void ApplySize()
    {
        int flagH = (int)_settings.PopupSize;
        _flagBox.Height = flagH;
        Padding = new Padding(Pad);
        Size = new Size(FormWidth, Pad + flagH + LabelHeight + Pad);
    }

    void SetFlag(LayoutInfo layout)
    {
        var previous = _currentImage;
        _currentImage = TrayIconFactory.LoadFlagBitmap(layout.FlagCode);
        _flagBox.Image = _currentImage;
        previous?.Dispose();
    }

    Point CalcLocation()
    {
        var area = Screen.PrimaryScreen?.WorkingArea ?? Screen.GetWorkingArea(Point.Empty);
        const int margin = 20;

        return _settings.PopupPosition switch
        {
            ScreenCorner.TopLeft    => new Point(area.Left  + margin,          area.Top    + margin),
            ScreenCorner.TopRight   => new Point(area.Right - Width  - margin, area.Top    + margin),
            ScreenCorner.BottomLeft => new Point(area.Left  + margin,          area.Bottom - Height - margin),
            _                       => new Point(area.Right - Width  - margin, area.Bottom - Height - margin),
        };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hideTimer.Dispose();
            _currentImage?.Dispose();
            _label.Font.Dispose();
        }
        base.Dispose(disposing);
    }
}

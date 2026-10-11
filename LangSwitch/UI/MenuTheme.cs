using System.Runtime.InteropServices;

namespace LangSwitch.UI;

/// <summary>Dark colors for ToolStrip menus, modeled on the Windows 11 dark flyout.</summary>
sealed class DarkMenuColorTable : ProfessionalColorTable
{
    public static readonly Color Background = Color.FromArgb(44, 44, 44);
    public static readonly Color Hover      = Color.FromArgb(63, 63, 63);
    public static readonly Color Border     = Color.FromArgb(60, 60, 60);
    public static readonly Color Text       = Color.FromArgb(240, 240, 240);

    public override Color ToolStripDropDownBackground => Background;
    public override Color ImageMarginGradientBegin     => Background;
    public override Color ImageMarginGradientMiddle     => Background;
    public override Color ImageMarginGradientEnd        => Background;
    public override Color MenuBorder                    => Border;
    public override Color MenuItemBorder                => Hover;
    public override Color MenuItemSelected               => Hover;
    public override Color MenuItemSelectedGradientBegin  => Hover;
    public override Color MenuItemSelectedGradientEnd    => Hover;
    public override Color MenuItemPressedGradientBegin   => Hover;
    public override Color MenuItemPressedGradientEnd     => Hover;
    public override Color SeparatorDark                  => Border;
    public override Color SeparatorLight                 => Border;
}

/// <summary>Applies the current Windows light/dark theme to a popup menu (colors + rounded corners).</summary>
static class MenuTheme
{
    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    const int DWMWA_BORDER_COLOR             = 34;
    const int DWMWCP_ROUND                   = 2;
    const int DarkBorderColorRef             = 0x00505050; // COLORREF 0x00BBGGRR

    public static void Apply(ContextMenuStrip menu)
    {
        bool dark = SystemTheme.IsDark();

        menu.Renderer = dark
            ? new ToolStripProfessionalRenderer(new DarkMenuColorTable())
            : new ToolStripProfessionalRenderer();
        menu.BackColor = dark ? DarkMenuColorTable.Background : SystemColors.Menu;
        menu.ForeColor = dark ? DarkMenuColorTable.Text : SystemColors.MenuText;

        foreach (ToolStripItem item in menu.Items)
            item.ForeColor = menu.ForeColor;

        menu.HandleCreated -= OnHandleCreated;
        menu.HandleCreated += OnHandleCreated;
    }

    static void OnHandleCreated(object? sender, EventArgs e)
    {
        var menu = (ContextMenuStrip)sender!;
        int corner = DWMWCP_ROUND;
        DwmSetWindowAttribute(menu.Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));

        if (SystemTheme.IsDark())
        {
            int border = DarkBorderColorRef;
            DwmSetWindowAttribute(menu.Handle, DWMWA_BORDER_COLOR, ref border, sizeof(int));
        }
    }
}

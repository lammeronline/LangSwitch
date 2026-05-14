using LangSwitch.Core;
using LangSwitch.Settings;

namespace LangSwitch.UI;

sealed class TrayApplicationContext : ApplicationContext
{
    readonly NotifyIcon _tray;
    readonly FlagPopupForm _popup;
    readonly LayoutMonitor _monitor;
    AppSettings _settings;
    Icon? _currentIcon;

    public TrayApplicationContext()
    {
        _settings = SettingsStore.Load();

        _popup = new FlagPopupForm(_settings);
        _monitor = new LayoutMonitor();
        _monitor.LayoutChanged += OnLayoutChanged;

        _tray = new NotifyIcon
        {
            Text = "LangSwitch",
            Visible = true,
            ContextMenuStrip = BuildMenu(),
        };

        _monitor.Start();
        RefreshTrayIcon(_monitor.Current);
    }

    ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Settings…", null, (_, _) => OpenSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Quit());
        return menu;
    }

    void OnLayoutChanged(object? sender, LayoutChangedEventArgs e)
    {
        RefreshTrayIcon(e.Current);
        _popup.ShowLayout(e.Current);
    }

    void RefreshTrayIcon(LayoutInfo? layout)
    {
        layout ??= WindowsLayoutDetector.GetForegroundLayout();
        if (layout is null) return;

        var icon = TrayIconFactory.Create(layout);
        _tray.Icon = icon;
        _tray.Text = $"LangSwitch — {layout.DisplayName}";
        _currentIcon?.Dispose();
        _currentIcon = icon;
    }

    void OpenSettings()
    {
        using var form = new SettingsForm(_settings);
        if (form.ShowDialog() == DialogResult.OK)
        {
            SettingsStore.Save(_settings);
            _popup.ApplySettings(_settings);
            RefreshTrayIcon(_monitor.Current);
        }
    }

    void Quit()
    {
        _tray.Visible = false;
        _monitor.Dispose();
        _popup.Dispose();
        Application.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tray.Dispose();
            _currentIcon?.Dispose();
            _monitor.Dispose();
            _popup.Dispose();
        }
        base.Dispose(disposing);
    }
}

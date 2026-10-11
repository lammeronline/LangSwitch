using LangSwitch.Core;
using static LangSwitch.Core.Localization;

namespace LangSwitch.UI;

/// <summary>Small modal list of currently visible app windows, for picking one to exclude.</summary>
sealed class AppPickerForm : Form
{
    readonly ListView _list;
    readonly ImageList _icons = new() { ImageSize = new Size(20, 20), ColorDepth = ColorDepth.Depth32Bit };

    public string? SelectedProcessName { get; private set; }

    public AppPickerForm()
    {
        Text            = T("AppPicker.Title");
        Size            = new Size(460, 440);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        MinimizeBox     = false;
        StartPosition   = FormStartPosition.CenterParent;
        Font            = new Font("Segoe UI", 9f);
        AppIcon.ApplyTo(this);

        _list = new ListView
        {
            Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true,
            MultiSelect = false, HideSelection = false, SmallImageList = _icons,
        };
        _list.Columns.Add(T("AppPicker.ColumnTitle"), 240);
        _list.Columns.Add(T("AppPicker.ColumnProcess"), 160);

        _icons.Images.Add("fallback", SystemIcons.Application.ToBitmap());

        foreach (var (processName, title, exePath) in RunningWindowsEnumerator.GetVisibleAppWindows())
        {
            var iconKey = ResolveIconKey(processName, exePath);
            var item = new ListViewItem(title, iconKey) { Tag = processName };
            item.SubItems.Add(processName);
            _list.Items.Add(item);
        }
        _list.DoubleClick += (_, _) => Accept();

        var bar = new Panel { Dock = DockStyle.Bottom, Height = 46, Padding = new Padding(0, 9, 16, 0) };
        var btnOk     = new Button { Text = T("Common.OK"),     DialogResult = DialogResult.OK,     Width = 84, Height = 28, Anchor = AnchorStyles.Top | AnchorStyles.Right };
        var btnCancel = new Button { Text = T("Common.Cancel"), DialogResult = DialogResult.Cancel, Width = 84, Height = 28, Anchor = AnchorStyles.Top | AnchorStyles.Right };
        btnOk.Click += (_, _) => Accept();
        void Place()
        {
            btnCancel.Location = new Point(bar.Width - bar.Padding.Right - btnCancel.Width, 9);
            btnOk.Location     = new Point(btnCancel.Left - 8 - btnOk.Width, 9);
        }
        Place();
        bar.Resize += (_, _) => Place();
        bar.Controls.AddRange([btnOk, btnCancel]);
        AcceptButton = btnOk;
        CancelButton = btnCancel;

        Controls.Add(_list);
        Controls.Add(bar);
    }

    /// <summary>Adds the process's own icon to the shared ImageList on first sight (icons are
    /// cached per process name, since several windows commonly belong to the same exe) and
    /// returns the key to use for this row — falling back to a generic icon when the exe
    /// path isn't available or its icon can't be extracted (e.g. an elevated process we don't
    /// have access to).</summary>
    string ResolveIconKey(string processName, string? exePath)
    {
        if (_icons.Images.ContainsKey(processName)) return processName;

        if (exePath is not null)
        {
            try
            {
                using var icon = Icon.ExtractAssociatedIcon(exePath);
                if (icon is not null)
                {
                    _icons.Images.Add(processName, icon.ToBitmap());
                    return processName;
                }
            }
            catch (Exception ex) { AppLog.Error(ex); }
        }
        return "fallback";
    }

    void Accept()
    {
        if (_list.SelectedItems.Count > 0 && _list.SelectedItems[0].Tag is string processName)
        {
            SelectedProcessName = processName;
            DialogResult = DialogResult.OK;
        }
    }
}

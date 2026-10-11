using LangSwitch.Core;
using static LangSwitch.Core.Localization;

namespace LangSwitch.UI;

/// <summary>Small modal list of currently visible app windows, for picking one to exclude.</summary>
sealed class AppPickerForm : Form
{
    readonly ListBox _list;

    public string? SelectedProcessName { get; private set; }

    public AppPickerForm()
    {
        Text            = T("AppPicker.Title");
        Size            = new Size(420, 420);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        MinimizeBox     = false;
        StartPosition   = FormStartPosition.CenterParent;
        Font            = new Font("Segoe UI", 9f);

        _list = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
        foreach (var (processName, title) in RunningWindowsEnumerator.GetVisibleAppWindows())
            _list.Items.Add(new Entry(processName, title));
        _list.DoubleClick += (_, _) => Accept();

        var bar = new Panel { Dock = DockStyle.Bottom, Height = 46, Padding = new Padding(0, 9, 12, 0) };
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

    void Accept()
    {
        if (_list.SelectedItem is Entry entry)
        {
            SelectedProcessName = entry.ProcessName;
            DialogResult = DialogResult.OK;
        }
    }

    sealed class Entry(string processName, string title)
    {
        public string ProcessName { get; } = processName;
        public override string ToString() => $"{title}  —  {ProcessName}";
    }
}

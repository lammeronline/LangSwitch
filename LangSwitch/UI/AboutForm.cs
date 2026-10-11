using System.Diagnostics;
using System.Reflection;
using LangSwitch.Core;
using static LangSwitch.Core.Localization;

namespace LangSwitch.UI;

/// <summary>Small "About" dialog — version, and a link to the LammerOnline project portal.</summary>
sealed class AboutForm : Form
{
    const string PortalUrl = "https://lammeronline.com/";

    public AboutForm()
    {
        Text            = T("About.Title");
        Size            = new Size(360, 220);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        MinimizeBox     = false;
        StartPosition   = FormStartPosition.CenterParent;
        Font            = new Font("Segoe UI", 9f);

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        var versionText = version is null ? "" : $"v{version.Major}.{version.Minor}.{version.Build}";

        var lblName = new Label
        {
            Text = T("Common.AppName"), AutoSize = true, Font = new Font("Segoe UI", 15f, FontStyle.Bold),
            Location = new Point(24, 20),
        };
        var lblVersion = new Label
        {
            Text = versionText, AutoSize = true, ForeColor = SystemColors.GrayText,
            Location = new Point(26, 56),
        };
        var lblTagline = new Label
        {
            Text = T("About.Tagline"), AutoSize = true,
            Location = new Point(26, 82), MaximumSize = new Size(310, 0),
        };

        var link = new LinkLabel
        {
            Text = T("About.PortalLink"), AutoSize = true,
            Location = new Point(26, 118),
        };
        link.LinkClicked += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo(PortalUrl) { UseShellExecute = true }); }
            catch (Exception ex) { AppLog.Error(ex); }
        };

        var btnClose = new Button
        {
            Text = T("Common.OK"), DialogResult = DialogResult.OK,
            Width = 84, Height = 28, Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
        };
        btnClose.Location = new Point(ClientSize.Width - 24 - btnClose.Width, ClientSize.Height - 40);
        AcceptButton = btnClose;

        Controls.AddRange([lblName, lblVersion, lblTagline, link, btnClose]);
    }
}

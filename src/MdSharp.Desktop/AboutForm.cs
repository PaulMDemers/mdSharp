using MdSharp.Core;
using System.Diagnostics;

namespace MdSharp.Desktop;

internal sealed class AboutForm : Form
{
    public AboutForm(Icon? appIcon)
    {
        Text = $"About {AppInfo.Name}";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(520, 300);
        Font = SystemFonts.MessageBoxFont;
        if (appIcon is not null)
        {
            Icon = (Icon)appIcon.Clone();
        }

        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            ColumnCount = 2,
            RowCount = 1,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        PictureBox icon = new()
        {
            Size = new Size(64, 64),
            SizeMode = PictureBoxSizeMode.StretchImage,
            Image = appIcon?.ToBitmap(),
            Margin = new Padding(0, 4, 16, 0),
        };

        FlowLayoutPanel content = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
        };

        Label title = new()
        {
            AutoSize = true,
            Font = new Font(Font.FontFamily, 16, FontStyle.Bold),
            Text = AppInfo.Name,
        };
        Label version = new()
        {
            AutoSize = true,
            Margin = new Padding(0, 4, 0, 12),
            Text = $"Version {AppInfo.DisplayVersion}",
        };
        Label description = new()
        {
            AutoSize = false,
            Width = 380,
            Height = 56,
            Text = "Experimental Sega Genesis/Mega Drive emulator written in C#.",
        };
        Label copyright = new()
        {
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 12),
            Text = "Copyright (c) 2026 Paul Demers",
        };
        LinkLabel repository = new()
        {
            AutoSize = true,
            Text = AppInfo.RepositoryUrl,
        };
        repository.LinkClicked += (_, _) => OpenUrl(AppInfo.RepositoryUrl);

        LinkLabel license = new()
        {
            AutoSize = true,
            Margin = new Padding(0, 6, 0, 18),
            Text = AppInfo.LicenseName,
        };
        license.LinkClicked += (_, _) => OpenUrl($"{AppInfo.RepositoryUrl}/blob/main/LICENSE");

        Button close = new()
        {
            AutoSize = true,
            DialogResult = DialogResult.OK,
            Text = "OK",
        };

        content.Controls.Add(title);
        content.Controls.Add(version);
        content.Controls.Add(description);
        content.Controls.Add(copyright);
        content.Controls.Add(repository);
        content.Controls.Add(license);
        content.Controls.Add(close);

        layout.Controls.Add(icon, 0, 0);
        layout.Controls.Add(content, 1, 0);
        Controls.Add(layout);
        AcceptButton = close;
        CancelButton = close;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (Control control in Controls)
            {
                DisposeImages(control);
            }
        }

        base.Dispose(disposing);
    }

    private static void DisposeImages(Control control)
    {
        if (control is PictureBox { Image: not null } picture)
        {
            picture.Image.Dispose();
            picture.Image = null;
        }

        foreach (Control child in control.Controls)
        {
            DisposeImages(child);
        }
    }

    private static void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true,
        });
    }
}

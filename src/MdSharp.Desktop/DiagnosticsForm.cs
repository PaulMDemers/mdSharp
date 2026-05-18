namespace MdSharp.Desktop;

internal sealed class DiagnosticsForm : Form
{
    private readonly TextBox _textBox;

    public DiagnosticsForm(string diagnostics, Icon? icon)
    {
        Text = "Diagnostics";
        if (icon is not null)
        {
            Icon = (Icon)icon.Clone();
        }

        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = false;
        ClientSize = new Size(760, 560);
        MinimumSize = new Size(560, 380);
        Font = SystemFonts.MessageBoxFont;

        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 2,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _textBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Text = diagnostics,
            Font = new Font(FontFamily.GenericMonospace, Font.Size),
        };
        layout.Controls.Add(_textBox, 0, 0);

        FlowLayoutPanel buttons = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
        };

        Button close = new()
        {
            Text = "Close",
            DialogResult = DialogResult.OK,
            AutoSize = true,
        };
        Button copy = new()
        {
            Text = "Copy",
            AutoSize = true,
        };
        copy.Click += (_, _) =>
        {
            Clipboard.SetText(_textBox.Text);
        };

        buttons.Controls.Add(close);
        buttons.Controls.Add(copy);
        layout.Controls.Add(buttons, 0, 1);

        Controls.Add(layout);
        AcceptButton = close;
        CancelButton = close;
    }
}

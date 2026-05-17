namespace MdSharp.Desktop;

internal sealed class PreferencesForm : Form
{
    private readonly TextBox _romFolderBox = new();
    private readonly ComboBox _budgetBox = new();
    private readonly CheckBox _mutedBox = new();

    public PreferencesForm(DesktopSettings settings)
    {
        Text = "Preferences";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(560, 250);
        Font = SystemFonts.MessageBoxFont;

        _romFolderBox.Text = settings.DefaultRomDirectory ?? string.Empty;
        _mutedBox.Checked = settings.Muted;

        _budgetBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _budgetBox.Items.AddRange(
        [
            new BudgetOption("200k instructions per frame", 200_000),
            new BudgetOption("300k instructions per frame", 300_000),
            new BudgetOption("500k instructions per frame", 500_000),
        ]);
        _budgetBox.SelectedItem = _budgetBox.Items
            .OfType<BudgetOption>()
            .FirstOrDefault(option => option.Value == settings.InstructionBudget)
            ?? _budgetBox.Items[1];

        Controls.Add(BuildLayout());
        AcceptButton = Controls.Find("okButton", searchAllChildren: true).OfType<Button>().FirstOrDefault();
        CancelButton = Controls.Find("cancelButton", searchAllChildren: true).OfType<Button>().FirstOrDefault();
    }

    public string? DefaultRomDirectory
    {
        get
        {
            string path = _romFolderBox.Text.Trim();
            return string.IsNullOrWhiteSpace(path) ? null : path;
        }
    }

    public int InstructionBudget => _budgetBox.SelectedItem is BudgetOption option ? option.Value : 300_000;

    public bool Muted => _mutedBox.Checked;

    private Control BuildLayout()
    {
        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 5,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "Default ROM folder",
            Margin = new Padding(0, 0, 0, 4),
        }, 0, 0);

        FlowLayoutPanel folderRow = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 14),
        };
        _romFolderBox.Width = 390;
        _romFolderBox.Margin = new Padding(0, 3, 8, 3);
        Button browse = new()
        {
            AutoSize = true,
            Text = "Browse...",
            Margin = new Padding(0, 0, 8, 0),
        };
        browse.Click += (_, _) => BrowseRomFolder();
        Button clear = new()
        {
            AutoSize = true,
            Text = "Clear",
        };
        clear.Click += (_, _) => _romFolderBox.Clear();
        folderRow.Controls.Add(_romFolderBox);
        folderRow.Controls.Add(browse);
        folderRow.Controls.Add(clear);
        root.Controls.Add(folderRow, 0, 1);

        FlowLayoutPanel emulationRow = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 14),
        };
        emulationRow.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "Instruction budget:",
            Margin = new Padding(0, 7, 8, 0),
        });
        _budgetBox.Width = 220;
        emulationRow.Controls.Add(_budgetBox);
        root.Controls.Add(emulationRow, 0, 2);

        _mutedBox.AutoSize = true;
        _mutedBox.Text = "Mute audio";
        root.Controls.Add(_mutedBox, 0, 3);

        FlowLayoutPanel buttons = new()
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
        };
        Button ok = new()
        {
            Name = "okButton",
            DialogResult = DialogResult.OK,
            Text = "OK",
            AutoSize = true,
        };
        Button cancel = new()
        {
            Name = "cancelButton",
            DialogResult = DialogResult.Cancel,
            Text = "Cancel",
            AutoSize = true,
        };
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);
        root.Controls.Add(buttons, 0, 4);

        return root;
    }

    private void BrowseRomFolder()
    {
        using FolderBrowserDialog dialog = new()
        {
            Description = "Choose the default ROM folder",
            UseDescriptionForTitle = true,
        };

        string? current = DefaultRomDirectory;
        if (!string.IsNullOrWhiteSpace(current) && Directory.Exists(current))
        {
            dialog.SelectedPath = current;
        }

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _romFolderBox.Text = dialog.SelectedPath;
        }
    }

    private sealed record BudgetOption(string Text, int Value)
    {
        public override string ToString() => Text;
    }
}

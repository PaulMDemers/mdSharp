namespace MdSharp.Desktop;

internal sealed class PreferencesForm : Form
{
    private readonly TextBox _romFolderBox = new();
    private readonly TextBox _saveRamFolderBox = new();
    private readonly TextBox _stateFolderBox = new();
    private readonly ComboBox _budgetBox = new();
    private readonly ComboBox _aspectModeBox = new();
    private readonly CheckBox _integerScaleBox = new();
    private readonly CheckBox _smoothingBox = new();
    private readonly CheckBox _mutedBox = new();

    public PreferencesForm(DesktopSettings settings)
    {
        Text = "Preferences";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(620, 470);
        Font = SystemFonts.MessageBoxFont;

        _romFolderBox.Text = settings.DefaultRomDirectory ?? string.Empty;
        _saveRamFolderBox.Text = settings.SaveRamDirectory ?? string.Empty;
        _stateFolderBox.Text = settings.StateDirectory ?? string.Empty;
        _integerScaleBox.Checked = settings.VideoIntegerScale;
        _smoothingBox.Checked = settings.VideoSmoothing;
        _mutedBox.Checked = settings.Muted;

        _aspectModeBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _aspectModeBox.Items.AddRange(
        [
            new AspectOption("Native 320x224", VideoAspectMode.Native),
            new AspectOption("4:3 corrected", VideoAspectMode.FourThree),
            new AspectOption("Stretch to window", VideoAspectMode.Stretch),
        ]);
        _aspectModeBox.SelectedItem = _aspectModeBox.Items
            .OfType<AspectOption>()
            .FirstOrDefault(option => option.Value == settings.VideoAspectMode)
            ?? _aspectModeBox.Items[0];

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

    public string? SaveRamDirectory => TextOrNull(_saveRamFolderBox);

    public string? StateDirectory => TextOrNull(_stateFolderBox);

    public VideoAspectMode VideoAspectMode => _aspectModeBox.SelectedItem is AspectOption option ? option.Value : VideoAspectMode.Native;

    public bool VideoIntegerScale => _integerScaleBox.Checked;

    public bool VideoSmoothing => _smoothingBox.Checked;

    private Control BuildLayout()
    {
        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 12,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        AddFolderSetting(root, 0, "Default ROM folder", _romFolderBox, "Choose the default ROM folder");
        AddFolderSetting(root, 2, "Save RAM folder", _saveRamFolderBox, "Choose the save RAM folder");
        AddFolderSetting(root, 4, "Save-state folder", _stateFolderBox, "Choose the save-state folder");

        FlowLayoutPanel displayRow = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0, 2, 0, 10),
        };
        displayRow.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "Aspect:",
            Margin = new Padding(0, 7, 8, 0),
        });
        _aspectModeBox.Width = 180;
        displayRow.Controls.Add(_aspectModeBox);
        _integerScaleBox.AutoSize = true;
        _integerScaleBox.Text = "Integer scale";
        _integerScaleBox.Margin = new Padding(18, 6, 0, 0);
        displayRow.Controls.Add(_integerScaleBox);
        _smoothingBox.AutoSize = true;
        _smoothingBox.Text = "Smooth scaling";
        _smoothingBox.Margin = new Padding(18, 6, 0, 0);
        displayRow.Controls.Add(_smoothingBox);
        root.Controls.Add(displayRow, 0, 6);

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
        root.Controls.Add(emulationRow, 0, 7);

        _mutedBox.AutoSize = true;
        _mutedBox.Text = "Mute audio";
        root.Controls.Add(_mutedBox, 0, 8);

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
        root.Controls.Add(buttons, 0, 11);

        return root;
    }

    private void AddFolderSetting(TableLayoutPanel root, int row, string label, TextBox textBox, string description)
    {
        root.Controls.Add(new Label
        {
            AutoSize = true,
            Text = label,
            Margin = new Padding(0, 0, 0, 4),
        }, 0, row);

        FlowLayoutPanel folderRow = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 12),
        };
        textBox.Width = 440;
        textBox.Margin = new Padding(0, 3, 8, 3);
        Button browse = new()
        {
            AutoSize = true,
            Text = "Browse...",
            Margin = new Padding(0, 0, 8, 0),
        };
        browse.Click += (_, _) => BrowseFolder(textBox, description);
        Button clear = new()
        {
            AutoSize = true,
            Text = "Clear",
        };
        clear.Click += (_, _) => textBox.Clear();
        folderRow.Controls.Add(textBox);
        folderRow.Controls.Add(browse);
        folderRow.Controls.Add(clear);
        root.Controls.Add(folderRow, 0, row + 1);
    }

    private void BrowseFolder(TextBox textBox, string description)
    {
        using FolderBrowserDialog dialog = new()
        {
            Description = description,
            UseDescriptionForTitle = true,
        };

        string? current = TextOrNull(textBox);
        if (!string.IsNullOrWhiteSpace(current) && Directory.Exists(current))
        {
            dialog.SelectedPath = current;
        }

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            textBox.Text = dialog.SelectedPath;
        }
    }

    private static string? TextOrNull(TextBox textBox)
    {
        string path = textBox.Text.Trim();
        return string.IsNullOrWhiteSpace(path) ? null : path;
    }

    private sealed record BudgetOption(string Text, int Value)
    {
        public override string ToString() => Text;
    }

    private sealed record AspectOption(string Text, VideoAspectMode Value)
    {
        public override string ToString() => Text;
    }
}

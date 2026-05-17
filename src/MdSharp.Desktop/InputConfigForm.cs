using MdSharp.Core.Input;

namespace MdSharp.Desktop;

internal sealed class InputConfigForm : Form
{
    private static readonly GenesisButton[] ConfigurableButtons =
    [
        GenesisButton.Up,
        GenesisButton.Down,
        GenesisButton.Left,
        GenesisButton.Right,
        GenesisButton.A,
        GenesisButton.B,
        GenesisButton.C,
        GenesisButton.Start,
        GenesisButton.X,
        GenesisButton.Y,
        GenesisButton.Z,
        GenesisButton.Mode,
    ];

    private readonly Dictionary<int, Dictionary<GenesisButton, TextBox>> _keyBoxes = [];
    private readonly Dictionary<int, Dictionary<GenesisButton, ComboBox>> _gamepadBoxes = [];
    private readonly ComboBox[] _gamepadIndexBoxes = new ComboBox[InputSettings.ControllerCount];
    private readonly CheckBox[] _keyboardEnabledBoxes = new CheckBox[InputSettings.ControllerCount];
    private readonly CheckBox[] _sixButtonEnabledBoxes = new CheckBox[InputSettings.ControllerCount];
    private readonly Label[] _gamepadStatusLabels = new Label[InputSettings.ControllerCount];
    private readonly List<InputProfileSettings> _profiles;
    private ComboBox _profileBox = null!;
    private ComboBox _port1DeviceBox = null!;
    private ComboBox _port2DeviceBox = null!;
    private InputSettings _settings;

    public InputConfigForm(InputSettings settings, IEnumerable<InputProfileSettings>? profiles = null)
    {
        _settings = settings.Clone();
        _settings.EnsureDefaults();
        _profiles = profiles?.Select(profile => profile.Clone()).ToList() ?? [];

        Text = "Input Configuration";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(620, 590);

        Controls.Add(BuildLayout());
        AcceptButton = Controls.Find("okButton", searchAllChildren: true).OfType<Button>().FirstOrDefault();
        CancelButton = Controls.Find("cancelButton", searchAllChildren: true).OfType<Button>().FirstOrDefault();
    }

    public InputSettings Settings => _settings.Clone();

    public List<InputProfileSettings> Profiles => _profiles.Select(profile => profile.Clone()).ToList();

    private Control BuildLayout()
    {
        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(12),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.Controls.Add(BuildProfileRow(), 0, 0);

        Label hint = new()
        {
            AutoSize = true,
            Text = "Click a keyboard cell and press a key. Backspace clears a key. Each player can use a keyboard profile and a specific XInput controller.",
            Margin = new Padding(0, 0, 0, 10),
        };
        root.Controls.Add(hint, 0, 1);

        FlowLayoutPanel hardware = new()
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 10),
        };
        hardware.Controls.Add(new Label { Text = "Port 1 device:", AutoSize = true, Margin = new Padding(0, 8, 6, 0) });
        _port1DeviceBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 180,
            Margin = new Padding(0, 4, 0, 4),
        };
        _port1DeviceBox.Items.AddRange(Enum.GetValues<ControllerPortDevice>().Cast<object>().ToArray());
        _port1DeviceBox.SelectedItem = _settings.Port1Device;
        _port1DeviceBox.SelectedIndexChanged += (_, _) =>
        {
            if (_port1DeviceBox.SelectedItem is ControllerPortDevice device)
            {
                _settings.Port1Device = device;
            }
        };
        hardware.Controls.Add(_port1DeviceBox);
        hardware.Controls.Add(new Label { Text = "Port 2 device:", AutoSize = true, Margin = new Padding(24, 8, 6, 0) });
        _port2DeviceBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 180,
            Margin = new Padding(0, 4, 0, 4),
        };
        _port2DeviceBox.Items.AddRange(Enum.GetValues<ControllerPortDevice>().Cast<object>().ToArray());
        _port2DeviceBox.SelectedItem = _settings.Port2Device;
        _port2DeviceBox.SelectedIndexChanged += (_, _) =>
        {
            if (_port2DeviceBox.SelectedItem is ControllerPortDevice device)
            {
                _settings.Port2Device = device;
            }
        };
        hardware.Controls.Add(_port2DeviceBox);
        root.Controls.Add(hardware, 0, 2);

        TabControl tabs = new()
        {
            Dock = DockStyle.Fill,
        };

        for (int playerIndex = 0; playerIndex < InputSettings.ControllerCount; playerIndex++)
        {
            TabPage page = new($"Player {playerIndex + 1}")
            {
                Padding = new Padding(8),
            };
            page.Controls.Add(BuildPlayerPage(playerIndex));
            tabs.TabPages.Add(page);
        }

        root.Controls.Add(tabs, 0, 3);

        FlowLayoutPanel buttons = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
        };

        Button ok = new() { Name = "okButton", Text = "OK", DialogResult = DialogResult.OK, Width = 88 };
        Button cancel = new() { Name = "cancelButton", Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 88 };
        Button defaults = new() { Text = "Defaults", Width = 88 };
        Button refresh = new() { Text = "Refresh", Width = 88 };
        defaults.Click += (_, _) => ResetDefaults();
        refresh.Click += (_, _) => RefreshGamepadStatus();

        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(refresh);
        buttons.Controls.Add(defaults);
        root.Controls.Add(buttons, 0, 4);

        return root;
    }

    private Control BuildProfileRow()
    {
        FlowLayoutPanel profiles = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 10),
        };

        profiles.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "Profile:",
            Margin = new Padding(0, 8, 6, 0),
        });

        _profileBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDown,
            Width = 190,
            Margin = new Padding(0, 4, 6, 4),
        };
        RefreshProfileBox();
        profiles.Controls.Add(_profileBox);

        Button load = new() { Text = "Load", Width = 72, Margin = new Padding(0, 3, 6, 3) };
        Button save = new() { Text = "Save", Width = 72, Margin = new Padding(0, 3, 6, 3) };
        Button delete = new() { Text = "Delete", Width = 72, Margin = new Padding(0, 3, 6, 3) };
        load.Click += (_, _) => LoadProfile();
        save.Click += (_, _) => SaveProfile();
        delete.Click += (_, _) => DeleteProfile();
        profiles.Controls.Add(load);
        profiles.Controls.Add(save);
        profiles.Controls.Add(delete);

        return profiles;
    }

    private Control BuildPlayerPage(int playerIndex)
    {
        ControllerInputSettings player = _settings.Controller(playerIndex);
        _keyBoxes[playerIndex] = [];
        _gamepadBoxes[playerIndex] = [];

        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        FlowLayoutPanel options = new()
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 10),
        };

        CheckBox keyboardEnabled = new()
        {
            Text = "Keyboard enabled",
            Checked = player.KeyboardEnabled,
            AutoSize = true,
            Margin = new Padding(0, 4, 16, 4),
        };
        keyboardEnabled.CheckedChanged += (_, _) => player.KeyboardEnabled = keyboardEnabled.Checked;
        _keyboardEnabledBoxes[playerIndex] = keyboardEnabled;
        options.Controls.Add(keyboardEnabled);

        CheckBox sixButtonEnabled = new()
        {
            Text = "Six-button pad",
            Checked = player.SixButtonEnabled,
            AutoSize = true,
            Margin = new Padding(0, 4, 16, 4),
        };
        sixButtonEnabled.CheckedChanged += (_, _) => player.SixButtonEnabled = sixButtonEnabled.Checked;
        _sixButtonEnabledBoxes[playerIndex] = sixButtonEnabled;
        options.Controls.Add(sixButtonEnabled);

        Label gamepadLabel = new()
        {
            Text = "XInput device:",
            AutoSize = true,
            Margin = new Padding(0, 8, 6, 0),
        };
        options.Controls.Add(gamepadLabel);

        ComboBox gamepadIndex = new()
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 150,
            Margin = new Padding(0, 4, 0, 4),
        };
        gamepadIndex.Items.Add(new GamepadDeviceItem(-1, "Disabled"));
        for (int i = 0; i < 4; i++)
        {
            gamepadIndex.Items.Add(new GamepadDeviceItem(i, $"Controller {i + 1}"));
        }

        gamepadIndex.SelectedItem = gamepadIndex.Items
            .OfType<GamepadDeviceItem>()
            .FirstOrDefault(item => item.Index == player.GamepadIndex)
            ?? gamepadIndex.Items[0];
        gamepadIndex.SelectedIndexChanged += (_, _) =>
        {
            if (gamepadIndex.SelectedItem is GamepadDeviceItem item)
            {
                player.GamepadIndex = item.Index;
                RefreshGamepadStatus(playerIndex);
            }
        };
        _gamepadIndexBoxes[playerIndex] = gamepadIndex;
        options.Controls.Add(gamepadIndex);

        Label status = new()
        {
            AutoSize = true,
            Margin = new Padding(10, 8, 0, 0),
        };
        _gamepadStatusLabels[playerIndex] = status;
        options.Controls.Add(status);
        RefreshGamepadStatus(playerIndex);
        root.Controls.Add(options, 0, 0);

        TableLayoutPanel grid = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = ConfigurableButtons.Length + 1,
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        AddHeader(grid, "Genesis", 0);
        AddHeader(grid, "Keyboard", 1);
        AddHeader(grid, "Gamepad", 2);

        for (int row = 0; row < ConfigurableButtons.Length; row++)
        {
            GenesisButton button = ConfigurableButtons[row];
            int gridRow = row + 1;

            grid.Controls.Add(new Label { Text = button.ToString(), AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 7, 3, 3) }, 0, gridRow);

            TextBox keyBox = new()
            {
                ReadOnly = true,
                Text = KeyName(player.Keyboard[button]),
                Tag = (Player: playerIndex, Button: button),
                Dock = DockStyle.Fill,
                Margin = new Padding(3),
            };
            keyBox.KeyDown += (_, e) => CaptureKey(keyBox, e);
            _keyBoxes[playerIndex][button] = keyBox;
            grid.Controls.Add(keyBox, 1, gridRow);

            ComboBox gamepadBox = new()
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Dock = DockStyle.Fill,
                Margin = new Padding(3),
            };
            gamepadBox.Items.AddRange(Enum.GetValues<GamepadControl>().Cast<object>().ToArray());
            gamepadBox.SelectedItem = player.Gamepad[button];
            gamepadBox.SelectedIndexChanged += (_, _) =>
            {
                if (gamepadBox.SelectedItem is GamepadControl control)
                {
                    player.Gamepad[button] = control;
                }
            };
            _gamepadBoxes[playerIndex][button] = gamepadBox;
            grid.Controls.Add(gamepadBox, 2, gridRow);
        }

        root.Controls.Add(grid, 0, 1);
        return root;
    }

    private void CaptureKey(TextBox keyBox, KeyEventArgs e)
    {
        (int playerIndex, GenesisButton button) = ((int Player, GenesisButton Button))keyBox.Tag!;
        Keys key = e.KeyCode is Keys.Back or Keys.Delete ? Keys.None : e.KeyCode;
        ControllerInputSettings player = _settings.Controller(playerIndex);
        player.Keyboard[button] = key;
        keyBox.Text = KeyName(key);
        e.SuppressKeyPress = true;
        e.Handled = true;
    }

    private void ResetDefaults()
    {
        InputSettings defaults = InputSettings.Default();
        _settings.Port1Device = defaults.Port1Device;
        _settings.Port2Device = defaults.Port2Device;

        for (int playerIndex = 0; playerIndex < InputSettings.ControllerCount; playerIndex++)
        {
            ControllerInputSettings player = _settings.Controller(playerIndex);
            ControllerInputSettings defaultPlayer = defaults.Controller(playerIndex);
            player.KeyboardEnabled = defaultPlayer.KeyboardEnabled;
            player.SixButtonEnabled = defaultPlayer.SixButtonEnabled;
            player.GamepadIndex = defaultPlayer.GamepadIndex;
            player.Keyboard = new Dictionary<GenesisButton, Keys>(defaultPlayer.Keyboard);
            player.Gamepad = new Dictionary<GenesisButton, GamepadControl>(defaultPlayer.Gamepad);
        }

        ApplySettingsToControls();
    }

    private static void AddHeader(TableLayoutPanel grid, string text, int column)
    {
        grid.Controls.Add(new Label { Text = text, AutoSize = true, Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold), Anchor = AnchorStyles.Left }, column, 0);
    }

    private void RefreshGamepadStatus()
    {
        for (int playerIndex = 0; playerIndex < InputSettings.ControllerCount; playerIndex++)
        {
            RefreshGamepadStatus(playerIndex);
        }
    }

    private void RefreshGamepadStatus(int playerIndex)
    {
        if (_gamepadStatusLabels[playerIndex] is null)
        {
            return;
        }

        int gamepadIndex = _settings.Controller(playerIndex).GamepadIndex;
        if (gamepadIndex < 0)
        {
            _gamepadStatusLabels[playerIndex].Text = "Disabled";
            _gamepadStatusLabels[playerIndex].ForeColor = SystemColors.GrayText;
            return;
        }

        bool connected = XInputGamepad.IsConnected(gamepadIndex);
        _gamepadStatusLabels[playerIndex].Text = connected ? "Connected" : "Disconnected";
        _gamepadStatusLabels[playerIndex].ForeColor = connected ? Color.DarkGreen : Color.Firebrick;
    }

    private static string KeyName(Keys key)
    {
        return key == Keys.None ? "None" : key.ToString();
    }

    private void LoadProfile()
    {
        string? name = SelectedProfileName();
        if (name is null)
        {
            return;
        }

        InputProfileSettings? profile = _profiles.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
        if (profile is null)
        {
            return;
        }

        _settings = profile.Input.Clone();
        ApplySettingsToControls();
    }

    private void SaveProfile()
    {
        string? name = SelectedProfileName();
        if (name is null)
        {
            MessageBox.Show(this, "Enter a profile name before saving.", "Input Profile", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        InputProfileSettings? existing = _profiles.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            _profiles.Add(new InputProfileSettings { Name = name, Input = _settings.Clone() });
        }
        else
        {
            existing.Name = name;
            existing.Input = _settings.Clone();
        }

        RefreshProfileBox(name);
    }

    private void DeleteProfile()
    {
        string? name = SelectedProfileName();
        if (name is null)
        {
            return;
        }

        _profiles.RemoveAll(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
        RefreshProfileBox();
    }

    private string? SelectedProfileName()
    {
        string name = _profileBox.Text.Trim();
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }

    private void RefreshProfileBox(string? selectedName = null)
    {
        if (_profileBox is null)
        {
            return;
        }

        _profileBox.Items.Clear();
        foreach (InputProfileSettings profile in _profiles.OrderBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase))
        {
            _profileBox.Items.Add(profile.Name);
        }

        if (!string.IsNullOrWhiteSpace(selectedName))
        {
            _profileBox.Text = selectedName;
        }
        else
        {
            _profileBox.Text = string.Empty;
        }
    }

    private void ApplySettingsToControls()
    {
        _settings.EnsureDefaults();
        _port1DeviceBox.SelectedItem = _settings.Port1Device;
        _port2DeviceBox.SelectedItem = _settings.Port2Device;

        for (int playerIndex = 0; playerIndex < InputSettings.ControllerCount; playerIndex++)
        {
            ControllerInputSettings player = _settings.Controller(playerIndex);
            _keyboardEnabledBoxes[playerIndex].Checked = player.KeyboardEnabled;
            _sixButtonEnabledBoxes[playerIndex].Checked = player.SixButtonEnabled;
            _gamepadIndexBoxes[playerIndex].SelectedItem = _gamepadIndexBoxes[playerIndex].Items
                .OfType<GamepadDeviceItem>()
                .First(item => item.Index == player.GamepadIndex);
            RefreshGamepadStatus(playerIndex);

            foreach (GenesisButton button in ConfigurableButtons)
            {
                _keyBoxes[playerIndex][button].Text = KeyName(player.Keyboard[button]);
                _gamepadBoxes[playerIndex][button].SelectedItem = player.Gamepad[button];
            }
        }
    }

    private sealed record GamepadDeviceItem(int Index, string Label)
    {
        public override string ToString()
        {
            return Label;
        }
    }
}

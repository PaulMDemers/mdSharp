using MdSharp.Core.Input;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MdSharp.Desktop;

internal sealed class DesktopSettings
{
    private const int MaxRecentRoms = 10;
    private const int MaxInputProfiles = 25;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter(),
        },
    };

    public List<string> RecentRoms { get; set; } = [];
    public string? DefaultRomDirectory { get; set; }
    public string? SaveRamDirectory { get; set; }
    public string? StateDirectory { get; set; }
    public string? LastRomDirectory { get; set; }
    public string? LastRomPath { get; set; }
    public int CurrentStateSlot { get; set; } = 1;
    public int WindowWidth { get; set; } = 960;
    public int WindowHeight { get; set; } = 720;
    public int? WindowLeft { get; set; }
    public int? WindowTop { get; set; }
    public bool WindowMaximized { get; set; }
    public bool StartFullscreen { get; set; }
    public bool ShowDeveloperOptions { get; set; }
    public bool Muted { get; set; }
    public int InstructionBudget { get; set; } = 300_000;
    public VideoAspectMode VideoAspectMode { get; set; } = VideoAspectMode.Native;
    public bool VideoIntegerScale { get; set; }
    public bool VideoSmoothing { get; set; }
    public InputSettings Input { get; set; } = InputSettings.Default();
    public List<InputProfileSettings> InputProfiles { get; set; } = [];

    public static string SettingsPath => DesktopPaths.SettingsPath;

    public static DesktopSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                DesktopSettings? settings = JsonSerializer.Deserialize<DesktopSettings>(File.ReadAllText(SettingsPath), JsonOptions);
                if (settings is not null)
                {
                    settings.Input.EnsureDefaults();
                    settings.NormalizeInputProfiles();
                    settings.NormalizeRecentRoms();
                    settings.NormalizeSession();
                    return settings;
                }
            }
        }
        catch
        {
            // Bad settings should not prevent the emulator from starting.
        }

        return new DesktopSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath) ?? ".");
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch
        {
            // Settings persistence is best-effort.
        }
    }

    public void AddRecentRom(string path)
    {
        string fullPath = Path.GetFullPath(path);
        RecentRoms.RemoveAll(item => string.Equals(Path.GetFullPath(item), fullPath, StringComparison.OrdinalIgnoreCase));
        RecentRoms.Insert(0, fullPath);
        CurrentStateSlot = Math.Clamp(CurrentStateSlot, 1, 10);
        LastRomPath = fullPath;
        LastRomDirectory = Path.GetDirectoryName(fullPath);
        NormalizeRecentRoms();
    }

    private void NormalizeRecentRoms()
    {
        RecentRoms = RecentRoms
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxRecentRoms)
            .ToList();
    }

    public void NormalizeSession()
    {
        CurrentStateSlot = Math.Clamp(CurrentStateSlot, 1, 10);
        WindowWidth = Math.Clamp(WindowWidth, 640, 7680);
        WindowHeight = Math.Clamp(WindowHeight, 480, 4320);
        InstructionBudget = InstructionBudget switch
        {
            200_000 or 300_000 or 500_000 => InstructionBudget,
            _ => 300_000,
        };
        if (!Enum.IsDefined(VideoAspectMode))
        {
            VideoAspectMode = VideoAspectMode.Native;
        }

        if (!string.IsNullOrWhiteSpace(LastRomPath))
        {
            LastRomPath = Path.GetFullPath(LastRomPath);
        }

        if (!string.IsNullOrWhiteSpace(DefaultRomDirectory))
        {
            DefaultRomDirectory = Path.GetFullPath(DefaultRomDirectory);
        }

        if (!string.IsNullOrWhiteSpace(SaveRamDirectory))
        {
            SaveRamDirectory = Path.GetFullPath(SaveRamDirectory);
        }

        if (!string.IsNullOrWhiteSpace(StateDirectory))
        {
            StateDirectory = Path.GetFullPath(StateDirectory);
        }

        if (!string.IsNullOrWhiteSpace(LastRomDirectory))
        {
            LastRomDirectory = Path.GetFullPath(LastRomDirectory);
        }

        NormalizeInputProfiles();
    }

    private void NormalizeInputProfiles()
    {
        InputProfiles = InputProfiles
            .Where(profile => profile is not null && !string.IsNullOrWhiteSpace(profile.Name) && profile.Input is not null)
            .Select(profile =>
            {
                profile.Name = profile.Name.Trim();
                profile.Input.EnsureDefaults();
                return profile;
            })
            .GroupBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Take(MaxInputProfiles)
            .ToList();
    }
}

internal sealed class InputProfileSettings
{
    public string Name { get; set; } = string.Empty;
    public InputSettings Input { get; set; } = InputSettings.Default();

    public InputProfileSettings Clone()
    {
        return new InputProfileSettings
        {
            Name = Name,
            Input = Input.Clone(),
        };
    }
}

internal enum VideoAspectMode
{
    Native,
    FourThree,
    Stretch,
}

internal sealed class InputSettings
{
    public const int ControllerCount = 4;

    public List<ControllerInputSettings> Controllers { get; set; } = [];
    public ControllerPortDevice Port1Device { get; set; } = ControllerPortDevice.Gamepad;
    public ControllerPortDevice Port2Device { get; set; } = ControllerPortDevice.Gamepad;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<GenesisButton, Keys>? Keyboard { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<GenesisButton, GamepadControl>? Gamepad { get; set; }

    public static InputSettings Default()
    {
        InputSettings settings = new();
        settings.EnsureDefaults();
        return settings;
    }

    public void EnsureDefaults()
    {
        if (Controllers.Count == 0)
        {
            Controllers.Add(ControllerInputSettings.Default(playerIndex: 0));
        }

        if (Keyboard is not null || Gamepad is not null)
        {
            Controllers[0].Keyboard = Keyboard is null
                ? []
                : new Dictionary<GenesisButton, Keys>(Keyboard);
            Controllers[0].Gamepad = Gamepad is null
                ? []
                : new Dictionary<GenesisButton, GamepadControl>(Gamepad);
            Keyboard = null;
            Gamepad = null;
        }

        while (Controllers.Count < ControllerCount)
        {
            Controllers.Add(ControllerInputSettings.Default(Controllers.Count));
        }

        if (Controllers.Count > ControllerCount)
        {
            Controllers = Controllers.Take(ControllerCount).ToList();
        }

        for (int i = 0; i < Controllers.Count; i++)
        {
            Controllers[i] ??= ControllerInputSettings.Default(i);
            Controllers[i].EnsureDefaults(i);
        }

        if (!Enum.IsDefined(Port1Device))
        {
            Port1Device = ControllerPortDevice.Gamepad;
        }

        if (!Enum.IsDefined(Port2Device))
        {
            Port2Device = ControllerPortDevice.Gamepad;
        }
    }

    public ControllerInputSettings Controller(int playerIndex)
    {
        EnsureDefaults();
        return Controllers[Math.Clamp(playerIndex, 0, ControllerCount - 1)];
    }

    public InputSettings Clone()
    {
        EnsureDefaults();
        InputSettings clone = new()
        {
            Port1Device = Port1Device,
            Port2Device = Port2Device,
            Controllers = Controllers.Select(controller => controller.Clone()).ToList(),
        };
        clone.EnsureDefaults();
        return clone;
    }
}

internal sealed class ControllerInputSettings
{
    public bool KeyboardEnabled { get; set; } = true;
    public bool SixButtonEnabled { get; set; }
    public int GamepadIndex { get; set; }
    public Dictionary<GenesisButton, Keys> Keyboard { get; set; } = [];
    public Dictionary<GenesisButton, GamepadControl> Gamepad { get; set; } = [];

    public static ControllerInputSettings Default(int playerIndex)
    {
        ControllerInputSettings settings = new()
        {
            GamepadIndex = playerIndex,
        };
        settings.EnsureDefaults(playerIndex);
        return settings;
    }

    public void EnsureDefaults(int playerIndex)
    {
        GamepadIndex = GamepadIndex is >= -1 and < 4 ? GamepadIndex : playerIndex;

        if (playerIndex == 0)
        {
            EnsureKeyboard(GenesisButton.Up, Keys.Up);
            EnsureKeyboard(GenesisButton.Down, Keys.Down);
            EnsureKeyboard(GenesisButton.Left, Keys.Left);
            EnsureKeyboard(GenesisButton.Right, Keys.Right);
            EnsureKeyboard(GenesisButton.A, Keys.Z);
            EnsureKeyboard(GenesisButton.B, Keys.X);
            EnsureKeyboard(GenesisButton.C, Keys.C);
            EnsureKeyboard(GenesisButton.Start, Keys.Enter);
            EnsureKeyboard(GenesisButton.X, Keys.V);
            EnsureKeyboard(GenesisButton.Y, Keys.B);
            EnsureKeyboard(GenesisButton.Z, Keys.N);
            EnsureKeyboard(GenesisButton.Mode, Keys.ShiftKey);
        }
        else if (playerIndex == 1)
        {
            EnsureKeyboard(GenesisButton.Up, Keys.W);
            EnsureKeyboard(GenesisButton.Down, Keys.S);
            EnsureKeyboard(GenesisButton.Left, Keys.A);
            EnsureKeyboard(GenesisButton.Right, Keys.D);
            EnsureKeyboard(GenesisButton.A, Keys.J);
            EnsureKeyboard(GenesisButton.B, Keys.K);
            EnsureKeyboard(GenesisButton.C, Keys.L);
            EnsureKeyboard(GenesisButton.Start, Keys.Space);
            EnsureKeyboard(GenesisButton.X, Keys.U);
            EnsureKeyboard(GenesisButton.Y, Keys.I);
            EnsureKeyboard(GenesisButton.Z, Keys.O);
            EnsureKeyboard(GenesisButton.Mode, Keys.H);
        }
        else
        {
            EnsureKeyboard(GenesisButton.Up, Keys.None);
            EnsureKeyboard(GenesisButton.Down, Keys.None);
            EnsureKeyboard(GenesisButton.Left, Keys.None);
            EnsureKeyboard(GenesisButton.Right, Keys.None);
            EnsureKeyboard(GenesisButton.A, Keys.None);
            EnsureKeyboard(GenesisButton.B, Keys.None);
            EnsureKeyboard(GenesisButton.C, Keys.None);
            EnsureKeyboard(GenesisButton.Start, Keys.None);
            EnsureKeyboard(GenesisButton.X, Keys.None);
            EnsureKeyboard(GenesisButton.Y, Keys.None);
            EnsureKeyboard(GenesisButton.Z, Keys.None);
            EnsureKeyboard(GenesisButton.Mode, Keys.None);
        }

        EnsureGamepad(GenesisButton.Up, GamepadControl.DPadUp);
        EnsureGamepad(GenesisButton.Down, GamepadControl.DPadDown);
        EnsureGamepad(GenesisButton.Left, GamepadControl.DPadLeft);
        EnsureGamepad(GenesisButton.Right, GamepadControl.DPadRight);
        EnsureGamepad(GenesisButton.A, GamepadControl.X);
        EnsureGamepad(GenesisButton.B, GamepadControl.A);
        EnsureGamepad(GenesisButton.C, GamepadControl.B);
        EnsureGamepad(GenesisButton.Start, GamepadControl.Start);
        EnsureGamepad(GenesisButton.X, GamepadControl.Y);
        EnsureGamepad(GenesisButton.Y, GamepadControl.LeftShoulder);
        EnsureGamepad(GenesisButton.Z, GamepadControl.RightShoulder);
        EnsureGamepad(GenesisButton.Mode, GamepadControl.Back);
    }

    public ControllerInputSettings Clone()
    {
        return new ControllerInputSettings
        {
            KeyboardEnabled = KeyboardEnabled,
            SixButtonEnabled = SixButtonEnabled,
            GamepadIndex = GamepadIndex,
            Keyboard = new Dictionary<GenesisButton, Keys>(Keyboard),
            Gamepad = new Dictionary<GenesisButton, GamepadControl>(Gamepad),
        };
    }

    private void EnsureKeyboard(GenesisButton button, Keys key)
    {
        Keyboard.TryAdd(button, key);
    }

    private void EnsureGamepad(GenesisButton button, GamepadControl control)
    {
        Gamepad.TryAdd(button, control);
    }
}

internal enum GamepadControl
{
    None,
    DPadUp,
    DPadDown,
    DPadLeft,
    DPadRight,
    A,
    B,
    X,
    Y,
    LeftShoulder,
    RightShoulder,
    Back,
    Start,
    LeftThumb,
    RightThumb,
    LeftStickUp,
    LeftStickDown,
    LeftStickLeft,
    LeftStickRight,
    RightStickUp,
    RightStickDown,
    RightStickLeft,
    RightStickRight,
}

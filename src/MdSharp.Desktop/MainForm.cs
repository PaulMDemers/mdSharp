using MdSharp.Core;
using MdSharp.Core.Cartridge;
using MdSharp.Core.Cpu.M68k;
using MdSharp.Core.Input;
using MdSharp.Core.State;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace MdSharp.Desktop;

internal sealed class MainForm : Form
{
    private static readonly string AppTitle = $"{AppInfo.Name} {AppInfo.DisplayVersion}";

    private readonly VideoSurface _video = new() { Dock = DockStyle.Fill };
    private readonly StatusStrip _status = new();
    private readonly ToolStripStatusLabel _statusText = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly ToolStripStatusLabel _fpsText = new() { Text = "FPS 0.0" };
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 1 };
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly DesktopSettings _settings = DesktopSettings.Load();
    private readonly short[] _audioFrameBuffer = new short[4096];
    private readonly byte[] _videoFrameBuffer = new byte[MdSharp.Core.Video.Vdp.ScreenWidth * MdSharp.Core.Video.Vdp.ScreenHeight * 3];

    private ToolStripMenuItem _pauseMenu = null!;
    private ToolStripMenuItem _muteMenu = null!;
    private ToolStripMenuItem _openLastMenu = null!;
    private ToolStripMenuItem _recentMenu = null!;
    private ToolStripMenuItem _quickSaveMenu = null!;
    private ToolStripMenuItem _quickLoadMenu = null!;
    private ToolStripMenuItem _stateSlotMenu = null!;
    private ToolStripMenuItem _fullscreenMenu = null!;
    private ToolStripMenuItem _developerOptionsMenu = null!;
    private ToolStripMenuItem _startRecordingMenu = null!;
    private ToolStripMenuItem _stopRecordingMenu = null!;
    private ToolStripMenuItem _playMovieMenu = null!;
    private ToolStripMenuItem _stopMovieMenu = null!;
    private ToolStripSeparator _developerSeparator = null!;
    private ToolStripMenuItem _budget200Menu = null!;
    private ToolStripMenuItem _budget300Menu = null!;
    private ToolStripMenuItem _budget500Menu = null!;
    private ToolStripMenuItem _enable32XMenu = null!;
    private MegaDrive? _machine;
    private CartridgeImage? _cartridge;
    private WaveOutAudio? _audio;
    private string? _romPath;
    private string? _recordingPath;
    private InputMovie? _recordingMovie;
    private InputMovie? _playbackMovie;
    private int _playbackFrame;
    private double _nextFrameMs;
    private double _lastFpsMs;
    private int _framesThisSecond;
    private bool _paused = true;
    private bool _muted;
    private bool _fullscreen;
    private Rectangle _windowedBounds;
    private FormWindowState _windowedState;
    private FormBorderStyle _windowedBorderStyle;
    private int _instructionsPerFrame = 300_000;
    private Point _lightGunPoint = new(MdSharp.Core.Video.Vdp.ScreenWidth / 2, MdSharp.Core.Video.Vdp.ScreenHeight / 2);
    private bool _lightGunVisible;
    private GenesisButton _lightGunButtons = GenesisButton.None;

    public MainForm(string? initialRom)
    {
        Text = AppTitle;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? Icon;
        _settings.NormalizeSession();
        ClientSize = new Size(_settings.WindowWidth, _settings.WindowHeight);
        MinimumSize = new Size(640, 480);
        KeyPreview = true;
        _muted = _settings.Muted;
        _instructionsPerFrame = _settings.InstructionBudget;
        ApplyVideoSettings();
        if (_settings.WindowLeft.HasValue && _settings.WindowTop.HasValue)
        {
            StartPosition = FormStartPosition.Manual;
            Location = new Point(_settings.WindowLeft.Value, _settings.WindowTop.Value);
        }
        if (_settings.WindowMaximized)
        {
            WindowState = FormWindowState.Maximized;
        }

        MainMenuStrip = BuildMenu();
        Controls.Add(_video);
        Controls.Add(_status);
        Controls.Add(MainMenuStrip);
        _status.Items.Add(_statusText);
        _status.Items.Add(_fpsText);
        _video.MouseMove += (_, e) => UpdateLightGunMouse(e.Location);
        _video.MouseEnter += (_, _) => _lightGunVisible = true;
        _video.MouseLeave += (_, _) => _lightGunVisible = false;
        _video.MouseDown += (_, e) => UpdateLightGunButtons(e.Button, pressed: true);
        _video.MouseUp += (_, e) => UpdateLightGunButtons(e.Button, pressed: false);

        _timer.Tick += (_, _) => TickEmulation();
        FormClosing += (_, _) =>
        {
            SaveSram();
            SaveDesktopSessionSettings();
            _settings.Save();
        };

        SetStatus("Open a Genesis/Mega Drive ROM to start.");
        _timer.Start();
        if (_settings.StartFullscreen)
        {
            BeginInvoke(ToggleFullscreen);
        }

        if (!string.IsNullOrWhiteSpace(initialRom) && File.Exists(initialRom))
        {
            LoadRom(initialRom);
        }
    }

    private MenuStrip BuildMenu()
    {
        MenuStrip menu = new();
        ToolStripMenuItem file = new("&File");
        ToolStripMenuItem openRomMenu = new("&Open ROM...", null, (_, _) => OpenRomDialog())
        {
            ShortcutKeys = Keys.Control | Keys.O,
        };
        file.DropDownItems.Add(openRomMenu);
        _openLastMenu = new ToolStripMenuItem("Reopen &Last ROM", null, (_, _) => ReopenLastRom())
        {
            ShortcutKeys = Keys.Control | Keys.R,
        };
        file.DropDownItems.Add(_openLastMenu);
        _recentMenu = new ToolStripMenuItem("Open &Recent");
        _recentMenu.DropDownOpening += (_, _) => PopulateRecentMenu();
        file.DropDownItems.Add(_recentMenu);
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add("&Preferences...", null, (_, _) => ShowPreferences());
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add("&Save State...", null, (_, _) => SaveStateDialog());
        file.DropDownItems.Add("&Load State...", null, (_, _) => LoadStateDialog());
        _quickSaveMenu = new ToolStripMenuItem("Quick &Save Slot", null, (_, _) => QuickSaveState()) { ShortcutKeyDisplayString = "F5" };
        _quickLoadMenu = new ToolStripMenuItem("Quick &Load Slot", null, (_, _) => QuickLoadState()) { ShortcutKeyDisplayString = "F8" };
        _stateSlotMenu = new ToolStripMenuItem("State S&lot");
        _stateSlotMenu.DropDownOpening += (_, _) => PopulateStateSlotMenu();
        file.DropDownItems.Add(_quickSaveMenu);
        file.DropDownItems.Add(_quickLoadMenu);
        file.DropDownItems.Add(_stateSlotMenu);
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add("E&xit", null, (_, _) => Close());

        ToolStripMenuItem emulation = new("&Emulation");
        _pauseMenu = new ToolStripMenuItem("&Pause", null, (_, _) => TogglePause()) { ShortcutKeyDisplayString = "P" };
        _muteMenu = new ToolStripMenuItem("&Mute", null, (_, _) => ToggleMute()) { ShortcutKeyDisplayString = "M" };
        emulation.DropDownItems.Add(_pauseMenu);
        emulation.DropDownItems.Add("&Reset", null, (_, _) => ResetMachine());
        emulation.DropDownItems.Add(_muteMenu);
        emulation.DropDownItems.Add(new ToolStripSeparator());
        emulation.DropDownItems.Add("&Input Configuration...", null, (_, _) => ShowInputConfig());
        _startRecordingMenu = new ToolStripMenuItem("Start Input &Recording...", null, (_, _) => StartInputRecording());
        _stopRecordingMenu = new ToolStripMenuItem("Stop Input Recording", null, (_, _) => StopInputRecording(save: true));
        _playMovieMenu = new ToolStripMenuItem("&Play Input Movie...", null, (_, _) => PlayInputMovieDialog());
        _stopMovieMenu = new ToolStripMenuItem("Stop Input Movie", null, (_, _) => StopInputMovie());
        emulation.DropDownItems.Add(_startRecordingMenu);
        emulation.DropDownItems.Add(_stopRecordingMenu);
        emulation.DropDownItems.Add(_playMovieMenu);
        emulation.DropDownItems.Add(_stopMovieMenu);
        _fullscreenMenu = new ToolStripMenuItem("&Fullscreen", null, (_, _) => ToggleFullscreen()) { ShortcutKeyDisplayString = "F11" };
        emulation.DropDownItems.Add(_fullscreenMenu);
        _developerSeparator = new ToolStripSeparator();
        emulation.DropDownItems.Add(_developerSeparator);
        _budget200Menu = new ToolStripMenuItem("Frame safety budget: &200k", null, (_, _) => SetInstructionBudget(200_000));
        _budget300Menu = new ToolStripMenuItem("Frame safety budget: &300k", null, (_, _) => SetInstructionBudget(300_000));
        _budget500Menu = new ToolStripMenuItem("Frame safety budget: &500k", null, (_, _) => SetInstructionBudget(500_000));
        _enable32XMenu = new ToolStripMenuItem("Enable experimental &32X ROMs", null, (_, _) => Toggle32XLoading());
        emulation.DropDownItems.Add(_budget200Menu);
        emulation.DropDownItems.Add(_budget300Menu);
        emulation.DropDownItems.Add(_budget500Menu);
        emulation.DropDownItems.Add(_enable32XMenu);

        ToolStripMenuItem view = new("&View");
        _developerOptionsMenu = new ToolStripMenuItem("&Developer Options", null, (_, _) => ToggleDeveloperOptions());
        view.DropDownItems.Add(_developerOptionsMenu);

        ToolStripMenuItem help = new("&Help");
        help.DropDownItems.Add("&Controls", null, (_, _) => ShowControls());
        help.DropDownItems.Add("&Diagnostics...", null, (_, _) => ShowDiagnostics());
        help.DropDownItems.Add(new ToolStripSeparator());
        help.DropDownItems.Add($"&About {AppInfo.Name}", null, (_, _) => ShowAbout());

        menu.Items.Add(file);
        menu.Items.Add(emulation);
        menu.Items.Add(view);
        menu.Items.Add(help);
        PopulateRecentMenu();
        PopulateStateSlotMenu();
        UpdateMenus();
        return menu;
    }

    private void OpenRomDialog()
    {
        using OpenFileDialog dialog = new()
        {
            Filter = BuildRomFileFilter(),
            Title = _settings.Enable32X ? "Open Genesis/Mega Drive/32X ROM" : "Open Genesis/Mega Drive ROM",
        };
        ApplyRomInitialDirectory(dialog);

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            LoadRom(dialog.FileName);
        }
    }

    private string BuildRomFileFilter()
    {
        return _settings.Enable32X
            ? "Genesis/32X ROMs|*.bin;*.md;*.gen;*.smd;*.rom;*.32x|Genesis/Mega Drive ROMs|*.bin;*.md;*.gen;*.smd;*.rom|32X ROMs|*.32x;*.bin;*.md|All files|*.*"
            : "Genesis/Mega Drive ROMs|*.bin;*.md;*.gen;*.smd;*.rom|All files|*.*";
    }

    private void LoadRom(string path)
    {
        try
        {
            SaveSram();
            StopInputRecording(save: true);
            StopInputMovie();
            LoadFreshMachine(path, loadSram: true);
            _settings.AddRecentRom(path);
            _settings.LastRomDirectory = Path.GetDirectoryName(Path.GetFullPath(path));
            _settings.Save();
            _paused = false;
            Text = $"{AppTitle} - {Path.GetFileName(path)}";
            SetStatus($"Loaded {Path.GetFileName(path)} | {DisplayName(_cartridge!)}");
            UpdateMenus();
        }
        catch (Exception ex)
        {
            _paused = true;
            MessageBox.Show(this, ex.Message, "Unable to load ROM", MessageBoxButtons.OK, MessageBoxIcon.Error);
            SetStatus("ROM load failed.");
            UpdateMenus();
        }
    }

    private void ReopenLastRom()
    {
        if (TryGetLastRomPath(out string? path))
        {
            LoadRom(path);
            return;
        }

        SetStatus("No last ROM is available.");
        UpdateMenus();
    }

    private void ShowPreferences()
    {
        using PreferencesForm dialog = new(_settings);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _settings.DefaultRomDirectory = dialog.DefaultRomDirectory;
        _settings.SaveRamDirectory = dialog.SaveRamDirectory;
        _settings.StateDirectory = dialog.StateDirectory;
        _settings.InstructionBudget = dialog.InstructionBudget;
        _settings.VideoAspectMode = dialog.VideoAspectMode;
        _settings.VideoIntegerScale = dialog.VideoIntegerScale;
        _settings.VideoSmoothing = dialog.VideoSmoothing;
        _settings.Muted = dialog.Muted;
        _settings.Enable32X = dialog.Enable32X;
        _settings.NormalizeSession();

        _instructionsPerFrame = _settings.InstructionBudget;
        _muted = _settings.Muted;
        ApplyVideoSettings();
        if (_muted)
        {
            _audio?.Dispose();
            _audio = null;
        }
        else
        {
            TryStartAudio();
        }

        _settings.Save();
        UpdateMenus();
        SetStatus("Preferences saved.");
    }

    private void Toggle32XLoading()
    {
        _settings.Enable32X = !_settings.Enable32X;
        _settings.Save();
        UpdateMenus();
        SetStatus(_settings.Enable32X ? "Experimental 32X ROM loading enabled." : "Experimental 32X ROM loading disabled.");
    }

    private void ToggleDeveloperOptions()
    {
        _settings.ShowDeveloperOptions = !_settings.ShowDeveloperOptions;
        _settings.Save();
        UpdateMenus();
        SetStatus(_settings.ShowDeveloperOptions ? "Developer options enabled." : "Developer options hidden.");
    }

    private void ApplyVideoSettings()
    {
        _video.Configure(_settings.VideoAspectMode, _settings.VideoIntegerScale, _settings.VideoSmoothing);
    }

    private void TickEmulation()
    {
        if (_machine is null || _paused)
        {
            return;
        }

        double now = _clock.Elapsed.TotalMilliseconds;
        double frameMs = 1000.0 / _machine.Scheduler.FrameRate;
        int frames = 0;

        try
        {
            while (now >= _nextFrameMs && frames < 4)
            {
                PollControllerInput();
                _recordingMovie?.AddFrame((int)_machine.Frames, _machine.Bus.Controller1.Pressed, _machine.Bus.Controller2.Pressed);
                _machine.RunFrame(_instructionsPerFrame);
                _machine.RenderFrameBgrInto(_videoFrameBuffer);
                _video.SetFrame(_videoFrameBuffer);
                QueueAudio();
                if (_playbackMovie is not null && ++_playbackFrame >= _playbackMovie.FrameCount)
                {
                    StopInputMovie();
                    _paused = true;
                    SetStatus("Input movie finished.");
                }

                _nextFrameMs += frameMs;
                frames++;
                _framesThisSecond++;
            }

            if (frames == 4 && now > _nextFrameMs + frameMs)
            {
                _nextFrameMs = now + frameMs;
            }
        }
        catch (M68kException ex)
        {
            PauseWithStatus($"CPU stopped: {ex.Message}");
        }
        catch (Exception ex)
        {
            PauseWithStatus($"Emulation stopped: {ex.Message}");
        }

        if (now - _lastFpsMs >= 1000)
        {
            _fpsText.Text = $"FPS {_framesThisSecond * 1000.0 / Math.Max(1, now - _lastFpsMs):0.0}";
            _framesThisSecond = 0;
            _lastFpsMs = now;
            if (_machine is not null && _romPath is not null)
            {
                string movie = _recordingMovie is not null ? " | REC" : _playbackMovie is not null ? $" | MOVIE {_playbackFrame:N0}/{_playbackMovie.FrameCount:N0}" : string.Empty;
                SetStatus($"{Path.GetFileName(_romPath)} | frame {_machine.Frames:N0} | PC ${_machine.MainCpu.PC:X8} | {_machine.Vdp.LastRenderMode}{movie}");
            }
        }
    }

    private void QueueAudio()
    {
        if (_machine is null || _muted)
        {
            return;
        }

        TryStartAudio();
        int sampleCount = _machine.RenderFrameStereoAudioSamplesInto(_audioFrameBuffer);
        _audio?.Queue(_audioFrameBuffer, sampleCount);
    }

    private void TryStartAudio()
    {
        if (_muted || _audio is not null)
        {
            return;
        }

        try
        {
            _audio = new WaveOutAudio();
        }
        catch
        {
            _muted = true;
            _muteMenu.Checked = true;
            SetStatus("Audio device unavailable; continuing muted.");
        }
    }

    private void TogglePause()
    {
        if (_machine is null)
        {
            return;
        }

        _paused = !_paused;
        _nextFrameMs = _clock.Elapsed.TotalMilliseconds;
        UpdateMenus();
        SetStatus(_paused ? "Paused." : "Running.");
    }

    private void ToggleMute()
    {
        _muted = !_muted;
        _settings.Muted = _muted;
        _settings.Save();
        if (_muted)
        {
            _audio?.Dispose();
            _audio = null;
        }
        else
        {
            TryStartAudio();
        }

        UpdateMenus();
    }

    private void ToggleFullscreen()
    {
        if (!_fullscreen)
        {
            _windowedBounds = Bounds;
            _windowedState = WindowState;
            _windowedBorderStyle = FormBorderStyle;
            _fullscreen = true;
            MainMenuStrip!.Visible = false;
            _status.Visible = false;
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Normal;
            Bounds = Screen.FromControl(this).Bounds;
        }
        else
        {
            _fullscreen = false;
            MainMenuStrip!.Visible = true;
            _status.Visible = true;
            FormBorderStyle = _windowedBorderStyle;
            WindowState = FormWindowState.Normal;
            Bounds = _windowedBounds;
            WindowState = _windowedState;
        }

        _settings.StartFullscreen = _fullscreen;
        _settings.Save();
        UpdateMenus();
    }

    private void ResetMachine()
    {
        if (_machine is null || _romPath is null)
        {
            return;
        }

        try
        {
            SaveSram();
            StopInputMovie();
            LoadFreshMachine(_romPath, loadSram: true);
            if (_recordingMovie is not null)
            {
                _recordingMovie.Frames.Clear();
                _recordingMovie.SaveRamBase64 = Convert.ToBase64String(_cartridge!.CaptureSaveRam());
            }

            _paused = false;
            SetStatus("Reset.");
            UpdateMenus();
        }
        catch (Exception ex)
        {
            PauseWithStatus($"Reset failed: {ex.Message}");
        }
    }

    private void StartInputRecording()
    {
        if (_machine is null || _cartridge is null || _romPath is null)
        {
            return;
        }

        using SaveFileDialog dialog = new()
        {
            Filter = "mdSharp input movies|*.mdmovie|All files|*.*",
            Title = "Record Input Movie",
            FileName = $"{Path.GetFileNameWithoutExtension(_romPath)}-{DateTime.Now:yyyyMMdd-HHmmss}.mdmovie",
        };
        ApplyInitialDirectory(dialog);

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            SaveSram();
            StopInputMovie();
            LoadFreshMachine(_romPath, loadSram: true);
            _recordingPath = dialog.FileName;
            _recordingMovie = InputMovie.Create(_romPath, _cartridge!);
            _paused = false;
            UpdateMenus();
            SetStatus($"Recording input movie to {Path.GetFileName(_recordingPath)}.");
        }
        catch (Exception ex)
        {
            PauseWithStatus($"Unable to start recording: {ex.Message}");
        }
    }

    private void StopInputRecording(bool save)
    {
        if (_recordingMovie is null)
        {
            return;
        }

        InputMovie movie = _recordingMovie;
        string? path = _recordingPath;
        _recordingMovie = null;
        _recordingPath = null;
        if (save && !string.IsNullOrWhiteSpace(path))
        {
            movie.Save(path);
            SetStatus($"Saved input movie {Path.GetFileName(path)} ({movie.FrameCount:N0} frames).");
        }

        UpdateMenus();
    }

    private void PlayInputMovieDialog()
    {
        using OpenFileDialog dialog = new()
        {
            Filter = "mdSharp input movies|*.mdmovie|All files|*.*",
            Title = "Play Input Movie",
        };
        ApplyInitialDirectory(dialog);

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            PlayInputMovie(dialog.FileName);
        }
    }

    private void PlayInputMovie(string path)
    {
        InputMovie movie = InputMovie.Load(path);
        string? targetRomPath = _romPath;
        if ((_machine is null || _cartridge is null) && !string.IsNullOrWhiteSpace(movie.RomPath) && File.Exists(movie.RomPath))
        {
            targetRomPath = movie.RomPath;
        }

        if (string.IsNullOrWhiteSpace(targetRomPath) || !File.Exists(targetRomPath))
        {
            MessageBox.Show(this, "Load the movie's ROM before playing this input movie.", "Input Movie", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        CartridgeImage probe = CartridgeImage.FromFile(targetRomPath);
        bool hashMatches = movie.Matches(probe);
        DialogResult summaryResult = MessageBox.Show(
            this,
            BuildMovieSummary(path, movie, targetRomPath, hashMatches),
            "Play Input Movie",
            MessageBoxButtons.OKCancel,
            hashMatches ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        if (summaryResult != DialogResult.OK)
        {
            return;
        }

        try
        {
            SaveSram();
            StopInputRecording(save: true);
            LoadFreshMachine(targetRomPath, loadSram: false, movie);
            _settings.AddRecentRom(targetRomPath);
            _settings.LastRomDirectory = Path.GetDirectoryName(Path.GetFullPath(targetRomPath));
            _settings.Save();
            _playbackMovie = movie;
            _playbackFrame = 0;
            _paused = false;
            Text = $"{AppTitle} - {Path.GetFileName(targetRomPath)}";
            UpdateMenus();
            SetStatus($"Playing input movie {Path.GetFileName(path)} ({movie.FrameCount:N0} frames).");
        }
        catch (Exception ex)
        {
            PauseWithStatus($"Unable to play input movie: {ex.Message}");
        }
    }

    private void StopInputMovie()
    {
        if (_playbackMovie is null)
        {
            return;
        }

        _playbackMovie = null;
        _playbackFrame = 0;
        UpdateMenus();
    }

    private void SaveStateDialog()
    {
        if (_machine is null)
        {
            return;
        }

        using SaveFileDialog dialog = new()
        {
            Filter = "mdSharp save states|*.mdss|All files|*.*",
            Title = "Save State",
            FileName = DefaultStateName(),
        };
        ApplyStateInitialDirectory(dialog);

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            SaveStateSerializer.Save(_machine, dialog.FileName);
            SetStatus($"Saved state to {Path.GetFileName(dialog.FileName)}.");
        }
    }

    private void QuickSaveState()
    {
        if (_machine is null || _cartridge is null || _romPath is null)
        {
            return;
        }

        string path = QuickStatePath(_settings.CurrentStateSlot);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        SaveStateSerializer.Save(_machine, path);
        SetStatus($"Saved slot {_settings.CurrentStateSlot} to {Path.GetFileName(path)}.");
        UpdateMenus();
    }

    private void QuickLoadState()
    {
        if (_machine is null || _cartridge is null || _romPath is null)
        {
            return;
        }

        string path = QuickStatePath(_settings.CurrentStateSlot);
        if (!File.Exists(path))
        {
            SetStatus($"State slot {_settings.CurrentStateSlot} is empty.");
            return;
        }

        SaveStateSerializer.Load(_machine, path);
        _machine.RenderFrameBgrInto(_videoFrameBuffer);
        _video.SetFrame(_videoFrameBuffer);
        ResetTiming();
        SetStatus($"Loaded slot {_settings.CurrentStateSlot} from {Path.GetFileName(path)}.");
        UpdateMenus();
    }

    private void LoadStateDialog()
    {
        if (_machine is null)
        {
            return;
        }

        using OpenFileDialog dialog = new()
        {
            Filter = "mdSharp save states|*.mdss|All files|*.*",
            Title = "Load State",
        };
        ApplyStateInitialDirectory(dialog);

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            SaveStateSerializer.Load(_machine, dialog.FileName);
            _machine.RenderFrameBgrInto(_videoFrameBuffer);
            _video.SetFrame(_videoFrameBuffer);
            ResetTiming();
            SetStatus($"Loaded state from {Path.GetFileName(dialog.FileName)}.");
        }
    }

    private void SetStateSlot(int slot)
    {
        _settings.CurrentStateSlot = Math.Clamp(slot, 1, 10);
        _settings.Save();
        PopulateStateSlotMenu();
        UpdateMenus();
        SetStatus($"Selected state slot {_settings.CurrentStateSlot}.");
    }

    private void DeleteCurrentStateSlot()
    {
        string path = QuickStatePathOrEmpty(_settings.CurrentStateSlot);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            SetStatus($"State slot {_settings.CurrentStateSlot} is already empty.");
            return;
        }

        File.Delete(path);
        PopulateStateSlotMenu();
        UpdateMenus();
        SetStatus($"Deleted state slot {_settings.CurrentStateSlot}.");
    }

    private void SaveSram()
    {
        if (_romPath is null || _cartridge is null)
        {
            return;
        }

        try
        {
            SramStore.Save(_romPath, _cartridge, _settings.SaveRamDirectory);
        }
        catch
        {
            // SRAM persistence should never prevent closing or loading another ROM.
        }
    }

    private void LoadFreshMachine(string path, bool loadSram, InputMovie? movie = null)
    {
        _audio?.Dispose();
        _audio = null;

        CartridgeImage cartridge = CartridgeImage.FromFile(path);
        if (HasBlockedUnsupportedHardware(cartridge))
        {
            string unsupported = string.Join(", ", BlockedUnsupportedHardware(cartridge));
            throw new NotSupportedException($"Unsupported cartridge hardware: {unsupported}");
        }

        if (movie is not null)
        {
            movie.RestoreInitialSaveRam(cartridge);
        }
        else if (loadSram)
        {
            SramStore.Load(path, cartridge, _settings.SaveRamDirectory);
        }

        MegaDrive machine = new(
            cartridge,
            thirtyTwoXM68kBios: TryLoadThirtyTwoXM68kBios(),
            thirtyTwoXMasterSh2Bios: TryLoadThirtyTwoXMasterSh2Bios(),
            thirtyTwoXSlaveSh2Bios: TryLoadThirtyTwoXSlaveSh2Bios());
        machine.Reset();
        _cartridge = cartridge;
        _machine = machine;
        _romPath = path;
        ApplyControllerSettings();
        ResetTiming();
        TryStartAudio();
    }

    private static ReadOnlyMemory<byte>? TryLoadThirtyTwoXM68kBios()
    {
        string[] candidates =
        [
            Environment.GetEnvironmentVariable("MDSHARP_32X_M68K_BIOS") ?? string.Empty,
            Path.Combine(AppContext.BaseDirectory, "32X_G_BIOS.BIN"),
            Path.Combine(AppContext.BaseDirectory, "32X_M68K_BIOS.BIN"),
            Path.Combine(Environment.CurrentDirectory, "32X_G_BIOS.BIN"),
            Path.Combine(Environment.CurrentDirectory, "32X_M68K_BIOS.BIN"),
        ];

        foreach (string candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate))
            {
                return File.ReadAllBytes(candidate);
            }
        }

        return null;
    }

    private static ReadOnlyMemory<byte>? TryLoadThirtyTwoXMasterSh2Bios()
    {
        string[] candidates =
        [
            Environment.GetEnvironmentVariable("MDSHARP_32X_MASTER_SH2_BIOS") ?? string.Empty,
            Path.Combine(AppContext.BaseDirectory, "32X_M_BIOS.BIN"),
            Path.Combine(AppContext.BaseDirectory, "32X_MASTER_BIOS.BIN"),
            Path.Combine(AppContext.BaseDirectory, "32X_MSH2_BIOS.BIN"),
            Path.Combine(Environment.CurrentDirectory, "32X_M_BIOS.BIN"),
            Path.Combine(Environment.CurrentDirectory, "32X_MASTER_BIOS.BIN"),
            Path.Combine(Environment.CurrentDirectory, "32X_MSH2_BIOS.BIN"),
        ];

        foreach (string candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate))
            {
                return File.ReadAllBytes(candidate);
            }
        }

        return null;
    }

    private static ReadOnlyMemory<byte>? TryLoadThirtyTwoXSlaveSh2Bios()
    {
        string[] candidates =
        [
            Environment.GetEnvironmentVariable("MDSHARP_32X_SLAVE_SH2_BIOS") ?? string.Empty,
            Path.Combine(AppContext.BaseDirectory, "32X_S_BIOS.BIN"),
            Path.Combine(AppContext.BaseDirectory, "32X_SLAVE_BIOS.BIN"),
            Path.Combine(AppContext.BaseDirectory, "32X_SSH2_BIOS.BIN"),
            Path.Combine(Environment.CurrentDirectory, "32X_S_BIOS.BIN"),
            Path.Combine(Environment.CurrentDirectory, "32X_SLAVE_BIOS.BIN"),
            Path.Combine(Environment.CurrentDirectory, "32X_SSH2_BIOS.BIN"),
        ];

        foreach (string candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate))
            {
                return File.ReadAllBytes(candidate);
            }
        }

        return null;
    }

    private bool HasBlockedUnsupportedHardware(CartridgeImage cartridge)
    {
        return BlockedUnsupportedHardware(cartridge).Any();
    }

    private IEnumerable<string> BlockedUnsupportedHardware(CartridgeImage cartridge)
    {
        foreach (string hardware in cartridge.Diagnostics.UnsupportedHardware)
        {
            if (_settings.Enable32X && cartridge.Diagnostics.Requires32X && hardware.Contains("32X", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return hardware;
        }
    }

    private void ResetTiming()
    {
        _nextFrameMs = _clock.Elapsed.TotalMilliseconds;
        _lastFpsMs = _nextFrameMs;
        _framesThisSecond = 0;
    }

    private void SetInstructionBudget(int budget)
    {
        _instructionsPerFrame = budget;
        _settings.InstructionBudget = budget;
        _settings.Save();
        SetStatus($"Instruction budget set to {budget:N0} per frame.");
        UpdateMenus();
    }

    private void ShowControls()
    {
        MessageBox.Show(
            this,
            "Player 1 keyboard defaults:\nArrow keys: D-pad\nZ: A\nX: B\nC: C\nEnter: Start\nV/B/N: X/Y/Z\nShift: Mode\n\nPlayer 2 keyboard defaults:\nWASD: D-pad\nJ: A\nK: B\nL: C\nSpace: Start\nU/I/O: X/Y/Z\nH: Mode\n\nPlayers 3 and 4 default to gamepads only and can be configured from Input Configuration.\n\nGamepad defaults:\nD-pad: D-pad\nX: A\nA: B\nB: C\nStart: Start\nY/LB/RB: X/Y/Z\nBack: Mode\n\nInput Configuration supports six-button pads, Sega Team Player and EA 4-Way Play on port 1, and Menacer/Justifier on port 2. J-Cart games use players 3 and 4 for cartridge controller ports. For light guns, aim with the mouse, left-click trigger, right-click Start.\n\nP: Pause\nM: Mute\nF5: Quick save\nF8: Quick load\nAlt+1..0: Select state slot\nF11: Fullscreen\nEsc: Exit fullscreen",
            "Controls",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void ShowAbout()
    {
        using AboutForm dialog = new(Icon);
        dialog.ShowDialog(this);
    }

    private void ShowDiagnostics()
    {
        using DiagnosticsForm dialog = new(BuildDiagnosticsText(), Icon);
        dialog.ShowDialog(this);
    }

    private string BuildDiagnosticsText()
    {
        StringBuilder builder = new();
        AppendLine(builder, "Generated", DateTimeOffset.Now.ToString("u"));
        AppendLine(builder, "Application", AppInfo.Name);
        AppendLine(builder, "Version", AppInfo.DisplayVersion);
        AppendLine(builder, "Executable", Application.ExecutablePath);
        AppendLine(builder, "Base directory", AppContext.BaseDirectory);
        builder.AppendLine();

        AppendLine(builder, "Portable mode", DesktopPaths.IsPortable ? "yes" : "no");
        AppendLine(builder, "Storage root", DesktopPaths.RootDirectory);
        AppendLine(builder, "Settings path", DesktopSettings.SettingsPath);
        AppendLine(builder, "Default save RAM folder", _settings.SaveRamDirectory ?? DesktopPaths.SaveRamDirectory);
        AppendLine(builder, "Default save-state folder", _settings.StateDirectory ?? DesktopPaths.StateDirectory);
        AppendLine(builder, "Default ROM folder", _settings.DefaultRomDirectory ?? "(not set)");
        builder.AppendLine();

        AppendPackageDiagnostics(builder);
        AppendDisplayDiagnostics(builder);
        AppendInputDiagnostics(builder);
        AppendRomDiagnostics(builder);

        return builder.ToString();
    }

    private void AppendPackageDiagnostics(StringBuilder builder)
    {
        string manifestPath = Path.Combine(AppContext.BaseDirectory, "manifest.json");
        AppendLine(builder, "Package manifest", File.Exists(manifestPath) ? manifestPath : "(not found)");
        if (!File.Exists(manifestPath))
        {
            builder.AppendLine();
            return;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            JsonElement root = document.RootElement;
            AppendLine(builder, "Package name", JsonString(root, "name"));
            AppendLine(builder, "Package version", JsonString(root, "version"));
            AppendLine(builder, "Build version", JsonString(root, "buildVersion"));
            AppendLine(builder, "Package commit", JsonString(root, "commit"));
            AppendLine(builder, "Build date UTC", JsonString(root, "buildDateUtc"));
            AppendLine(builder, "Runtime mode", JsonString(root, "runtimeMode"));
            AppendLine(builder, "Runtime", JsonString(root, "runtime"));
            AppendLine(builder, "Package portable", JsonBool(root, "portable"));
            AppendLine(builder, "Packaged files", JsonArrayCount(root, "files"));
        }
        catch (Exception ex)
        {
            AppendLine(builder, "Package manifest error", ex.Message);
        }

        builder.AppendLine();
    }

    private void AppendDisplayDiagnostics(StringBuilder builder)
    {
        AppendLine(builder, "Display aspect", _settings.VideoAspectMode.ToString());
        AppendLine(builder, "Integer scale", _settings.VideoIntegerScale ? "yes" : "no");
        AppendLine(builder, "Smooth scaling", _settings.VideoSmoothing ? "yes" : "no");
        AppendLine(builder, "Fullscreen", _fullscreen ? "yes" : "no");
        AppendLine(builder, "Muted", _muted ? "yes" : "no");
        AppendLine(builder, "Developer options", _settings.ShowDeveloperOptions ? "shown" : "hidden");
        if (_settings.ShowDeveloperOptions)
        {
            AppendLine(builder, "Frame safety budget", _instructionsPerFrame.ToString("N0"));
            AppendLine(builder, "Experimental 32X ROM loading", _settings.Enable32X ? "enabled" : "disabled");
        }

        builder.AppendLine();
    }

    private void AppendInputDiagnostics(StringBuilder builder)
    {
        _settings.Input.EnsureDefaults();
        AppendLine(builder, "Port 1 device", _settings.Input.Port1Device.ToString());
        AppendLine(builder, "Port 2 device", _settings.Input.Port2Device.ToString());
        for (int i = 0; i < InputSettings.ControllerCount; i++)
        {
            ControllerInputSettings controller = _settings.Input.Controller(i);
            AppendLine(builder, $"Player {i + 1}", $"keyboard={(controller.KeyboardEnabled ? "on" : "off")}, sixButton={(controller.SixButtonEnabled ? "on" : "off")}, gamepad={(controller.GamepadIndex < 0 ? "disabled" : controller.GamepadIndex + 1)}");
        }

        builder.AppendLine();
    }

    private void AppendRomDiagnostics(StringBuilder builder)
    {
        if (_cartridge is null || _romPath is null)
        {
            AppendLine(builder, "ROM", "(none loaded)");
            return;
        }

        CartridgeDiagnostics diagnostics = _cartridge.Diagnostics;
        AppendLine(builder, "ROM path", _romPath);
        AppendLine(builder, "ROM hash", InputMovie.ComputeRomSha256(_cartridge));
        AppendLine(builder, "Save RAM path", SramStore.GetSavePath(_romPath, _cartridge, _settings.SaveRamDirectory));
        AppendLine(builder, $"Quick state slot {_settings.CurrentStateSlot}", QuickStatePath(_settings.CurrentStateSlot));
        AppendLine(builder, "Domestic name", Empty(_cartridge.Header.DomesticName));
        AppendLine(builder, "Overseas name", Empty(_cartridge.Header.OverseasName));
        AppendLine(builder, "Product code", Empty(_cartridge.Header.ProductCode));
        AppendLine(builder, "Region", Empty(_cartridge.Header.Region));
        AppendLine(builder, "ROM size", $"{diagnostics.RomSize:N0} bytes");
        AppendLine(builder, "Save hardware", diagnostics.SaveHardware);
        AppendLine(builder, "Save range", FormatRange(diagnostics.SaveRamStart, diagnostics.SaveRamEnd, diagnostics.SaveRamLanes));
        AppendLine(builder, "EEPROM size", diagnostics.EepromSize?.ToString("N0") ?? "none");
        AppendLine(builder, "Bank switching", diagnostics.UsesBankSwitchRegisters ? "yes" : "no");
        AppendLine(builder, "J-Cart", diagnostics.HasJCart ? "yes" : "no");
        AppendLine(builder, "SVP", diagnostics.HasSvp ? "yes" : "no");
        AppendLine(builder, "32X", diagnostics.Requires32X ? "required" : "no");
        AppendLine(builder, "Unsupported hardware", diagnostics.UnsupportedHardware.Length == 0 ? "none" : string.Join(", ", diagnostics.UnsupportedHardware));
        AppendLine(builder, "Warnings", diagnostics.Warnings.Length == 0 ? "none" : string.Join("; ", diagnostics.Warnings));
        AppendLine(builder, "Frame", _machine?.Frames.ToString("N0") ?? "unknown");
        AppendLine(builder, "PC", _machine is null ? "unknown" : $"${_machine.MainCpu.PC:X8}");
        AppendLine(builder, "Render mode", _machine?.Vdp.LastRenderMode ?? "unknown");
    }

    private static void AppendLine(StringBuilder builder, string name, string value)
    {
        builder.Append(name);
        builder.Append(": ");
        builder.AppendLine(value);
    }

    private static string JsonString(JsonElement root, string property)
    {
        return root.TryGetProperty(property, out JsonElement value) && value.ValueKind != JsonValueKind.Null
            ? value.ToString()
            : "(not set)";
    }

    private static string JsonBool(JsonElement root, string property)
    {
        return root.TryGetProperty(property, out JsonElement value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? (value.GetBoolean() ? "yes" : "no")
            : "(not set)";
    }

    private static string JsonArrayCount(JsonElement root, string property)
    {
        return root.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.Array
            ? value.GetArrayLength().ToString("N0")
            : "(not set)";
    }

    private static string FormatRange(uint? start, uint? end, string lanes)
    {
        return start.HasValue && end.HasValue
            ? $"${start.Value:X6}-${end.Value:X6} ({lanes})"
            : "none";
    }

    private static string Empty(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "(empty)" : value.Trim();
    }

    private void ShowInputConfig()
    {
        bool wasPaused = _paused;
        if (_machine is not null)
        {
            _paused = true;
            UpdateMenus();
        }

        using InputConfigForm dialog = new(_settings.Input, _settings.InputProfiles);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _settings.Input = dialog.Settings;
            _settings.InputProfiles = dialog.Profiles;
            _settings.Input.EnsureDefaults();
            _settings.NormalizeSession();
            ApplyControllerSettings();
            _settings.Save();
            SetStatus("Input configuration saved.");
        }

        if (_machine is not null)
        {
            _paused = wasPaused;
            ResetTiming();
            UpdateMenus();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        HandleKey(e.KeyCode, pressed: true, e);
        base.OnKeyDown(e);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        HandleKey(e.KeyCode, pressed: false, e);
        base.OnKeyUp(e);
    }

    protected override bool ProcessKeyPreview(ref Message m)
    {
        const int wmKeyDown = 0x0100;
        const int wmKeyUp = 0x0101;
        const int wmSysKeyDown = 0x0104;
        const int wmSysKeyUp = 0x0105;

        if (m.Msg is wmKeyDown or wmKeyUp or wmSysKeyDown or wmSysKeyUp)
        {
            Keys keyCode = (Keys)m.WParam.ToInt32();
            bool pressed = m.Msg is wmKeyDown or wmSysKeyDown;
            if (HandleKey(keyCode, pressed))
            {
                return true;
            }
        }

        return base.ProcessKeyPreview(ref m);
    }

    private bool HandleKey(Keys keyCode, bool pressed, KeyEventArgs? e = null)
    {
        if (pressed && keyCode == Keys.P)
        {
            TogglePause();
            e?.SuppressKeyPress = true;
            e?.Handled = true;
            return true;
        }

        if (pressed && keyCode == Keys.M)
        {
            ToggleMute();
            e?.SuppressKeyPress = true;
            e?.Handled = true;
            return true;
        }

        if (pressed && keyCode == Keys.F11)
        {
            ToggleFullscreen();
            e?.SuppressKeyPress = true;
            e?.Handled = true;
            return true;
        }

        if (pressed && keyCode == Keys.F5)
        {
            QuickSaveState();
            e?.SuppressKeyPress = true;
            e?.Handled = true;
            return true;
        }

        if (pressed && keyCode == Keys.F8)
        {
            QuickLoadState();
            e?.SuppressKeyPress = true;
            e?.Handled = true;
            return true;
        }

        if (pressed && e?.Alt == true && TrySlotKey(keyCode, out int slot))
        {
            SetStateSlot(slot);
            e.SuppressKeyPress = true;
            e.Handled = true;
            return true;
        }

        if (pressed && keyCode == Keys.Escape && _fullscreen)
        {
            ToggleFullscreen();
            e?.SuppressKeyPress = true;
            e?.Handled = true;
            return true;
        }

        bool handled = IsControllerKey(keyCode);
        if (handled)
        {
            e?.SuppressKeyPress = true;
            e?.Handled = true;
        }

        return handled;
    }

    private bool IsControllerKey(Keys keyCode)
    {
        if (_machine is null)
        {
            return false;
        }

        _settings.Input.EnsureDefaults();
        foreach (ControllerInputSettings controller in _settings.Input.Controllers)
        {
            if (controller.KeyboardEnabled && controller.Keyboard.Any(pair => pair.Value == keyCode))
            {
                return true;
            }
        }

        return false;
    }

    private void PollControllerInput()
    {
        if (_machine is null)
        {
            return;
        }

        if (_playbackMovie is not null)
        {
            _machine.Bus.Controller1.Pressed = _playbackMovie.GetButtons(_playbackFrame, playerIndex: 0);
            _machine.Bus.Controller2.Pressed = _playbackMovie.GetButtons(_playbackFrame, playerIndex: 1);
            return;
        }

        _settings.Input.EnsureDefaults();
        ApplyLightGunPointer();
        ApplyControllerSettings();
        _machine.Bus.Controller1.Pressed = PollController(_settings.Input.Controllers[0]);
        _machine.Bus.Controller2.Pressed = PollController(_settings.Input.Controllers[1]) | PollLightGunButtons();
        _machine.Bus.Controller3.Pressed = PollController(_settings.Input.Controllers[2]);
        _machine.Bus.Controller4.Pressed = PollController(_settings.Input.Controllers[3]);
    }

    private void ApplyLightGunPointer()
    {
        if (_machine is null)
        {
            return;
        }

        _machine.Bus.SetLightGunPosition(_lightGunPoint.X, _lightGunPoint.Y, _lightGunVisible);
    }

    private GenesisButton PollLightGunButtons()
    {
        return _settings.Input.Port2Device is ControllerPortDevice.Menacer or ControllerPortDevice.KonamiJustifier
            ? _lightGunButtons
            : GenesisButton.None;
    }

    private void ApplyControllerSettings()
    {
        if (_machine is null)
        {
            return;
        }

        _settings.Input.EnsureDefaults();
        _machine.Bus.Port1Device = _settings.Input.Port1Device;
        _machine.Bus.Port2Device = _settings.Input.Port2Device;
        _machine.Bus.Controller1.SixButtonEnabled = _settings.Input.Controllers[0].SixButtonEnabled;
        _machine.Bus.Controller2.SixButtonEnabled = _settings.Input.Controllers[1].SixButtonEnabled;
        _machine.Bus.Controller3.SixButtonEnabled = _settings.Input.Controllers[2].SixButtonEnabled;
        _machine.Bus.Controller4.SixButtonEnabled = _settings.Input.Controllers[3].SixButtonEnabled;
    }

    private GenesisButton PollController(ControllerInputSettings settings)
    {
        return PollKeyboardInput(settings) | XInputGamepad.Poll(settings);
    }

    private GenesisButton PollKeyboardInput(ControllerInputSettings settings)
    {
        if (!settings.KeyboardEnabled)
        {
            return GenesisButton.None;
        }

        GenesisButton buttons = GenesisButton.None;
        foreach ((GenesisButton button, Keys key) in settings.Keyboard)
        {
            if (key != Keys.None && IsKeyDown(key))
            {
                buttons |= button;
            }
        }

        return buttons;
    }

    private static bool IsKeyDown(Keys key)
    {
        return (GetAsyncKeyState((int)key) & 0x8000) != 0;
    }

    private void UpdateLightGunMouse(Point point)
    {
        _lightGunVisible = _video.TryClientToFrame(point, out int x, out int y);
        if (_lightGunVisible)
        {
            _lightGunPoint = new Point(x, y);
        }
    }

    private void UpdateLightGunButtons(MouseButtons button, bool pressed)
    {
        GenesisButton mapped = button switch
        {
            MouseButtons.Left => GenesisButton.A,
            MouseButtons.Right => GenesisButton.Start,
            MouseButtons.Middle => GenesisButton.B,
            _ => GenesisButton.None,
        };

        if (mapped == GenesisButton.None)
        {
            return;
        }

        if (pressed)
        {
            _lightGunButtons |= mapped;
        }
        else
        {
            _lightGunButtons &= ~mapped;
        }
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    private void PauseWithStatus(string message)
    {
        _paused = true;
        UpdateMenus();
        SetStatus(message);
    }

    private void UpdateMenus()
    {
        _pauseMenu.Checked = _paused;
        _muteMenu.Checked = _muted;
        _fullscreenMenu.Checked = _fullscreen;
        _developerOptionsMenu.Checked = _settings.ShowDeveloperOptions;
        _startRecordingMenu.Enabled = _machine is not null && _recordingMovie is null && _playbackMovie is null;
        _stopRecordingMenu.Enabled = _recordingMovie is not null;
        _playMovieMenu.Enabled = _recordingMovie is null && _playbackMovie is null;
        _stopMovieMenu.Enabled = _playbackMovie is not null;
        _openLastMenu.Enabled = TryGetLastRomPath(out _);
        _recentMenu.Enabled = _settings.RecentRoms.Count > 0;
        _quickSaveMenu.Enabled = _machine is not null;
        _quickLoadMenu.Enabled = _machine is not null && File.Exists(QuickStatePathOrEmpty(_settings.CurrentStateSlot));
        _stateSlotMenu.Text = $"State S&lot: {_settings.CurrentStateSlot}";
        _developerSeparator.Visible = _settings.ShowDeveloperOptions;
        _budget200Menu.Visible = _settings.ShowDeveloperOptions;
        _budget300Menu.Visible = _settings.ShowDeveloperOptions;
        _budget500Menu.Visible = _settings.ShowDeveloperOptions;
        _enable32XMenu.Visible = _settings.ShowDeveloperOptions;
        _enable32XMenu.Checked = _settings.Enable32X;
        _budget200Menu.Checked = _instructionsPerFrame == 200_000;
        _budget300Menu.Checked = _instructionsPerFrame == 300_000;
        _budget500Menu.Checked = _instructionsPerFrame == 500_000;
    }

    private void PopulateRecentMenu()
    {
        _recentMenu.DropDownItems.Clear();
        List<string> existing = _settings.RecentRoms.Where(File.Exists).ToList();
        if (existing.Count == 0)
        {
            ToolStripMenuItem empty = new("(No recent ROMs)") { Enabled = false };
            _recentMenu.DropDownItems.Add(empty);
            _recentMenu.Enabled = false;
            return;
        }

        _recentMenu.Enabled = true;
        for (int i = 0; i < existing.Count; i++)
        {
            string path = existing[i];
            ToolStripMenuItem item = new($"&{i + 1} {Path.GetFileName(path)}")
            {
                ToolTipText = path,
            };
            item.Click += (_, _) => LoadRom(path);
            _recentMenu.DropDownItems.Add(item);
        }

        _recentMenu.DropDownItems.Add(new ToolStripSeparator());
        _recentMenu.DropDownItems.Add("&Clear Recent Files", null, (_, _) =>
        {
            _settings.RecentRoms.Clear();
            _settings.LastRomPath = null;
            _settings.Save();
            PopulateRecentMenu();
            UpdateMenus();
        });
    }

    private bool TryGetLastRomPath([NotNullWhen(true)] out string? path)
    {
        path = !string.IsNullOrWhiteSpace(_settings.LastRomPath) && File.Exists(_settings.LastRomPath)
            ? _settings.LastRomPath
            : _settings.RecentRoms.FirstOrDefault(File.Exists);
        return path is not null;
    }

    private void PopulateStateSlotMenu()
    {
        _stateSlotMenu.DropDownItems.Clear();
        for (int slot = 1; slot <= 10; slot++)
        {
            int selectedSlot = slot;
            string statePath = QuickStatePathOrEmpty(slot);
            bool hasState = File.Exists(statePath);
            string slotText = hasState
                ? $"Slot {slot} - {File.GetLastWriteTime(statePath):g}"
                : $"Slot {slot} - empty";
            ToolStripMenuItem item = new(slotText, null, (_, _) => SetStateSlot(selectedSlot))
            {
                Checked = slot == _settings.CurrentStateSlot,
                ToolTipText = hasState ? statePath : string.Empty,
            };
            _stateSlotMenu.DropDownItems.Add(item);
        }

        _stateSlotMenu.DropDownItems.Add(new ToolStripSeparator());
        ToolStripMenuItem delete = new("Delete Current Slot", null, (_, _) => DeleteCurrentStateSlot())
        {
            Enabled = File.Exists(QuickStatePathOrEmpty(_settings.CurrentStateSlot)),
        };
        _stateSlotMenu.DropDownItems.Add(delete);
    }

    private void SetStatus(string message)
    {
        _statusText.Text = message;
    }

    private string DefaultStateName()
    {
        string name = _romPath is null ? "state" : Path.GetFileNameWithoutExtension(_romPath);
        return $"{name}-{DateTime.Now:yyyyMMdd-HHmmss}.mdss";
    }

    private string QuickStatePathOrEmpty(int slot)
    {
        return _cartridge is null || _romPath is null ? string.Empty : QuickStatePath(slot);
    }

    private string QuickStatePath(int slot)
    {
        string romName = SafeFileName(Path.GetFileNameWithoutExtension(_romPath ?? "rom"));
        string hash = _cartridge is null ? "unknown" : InputMovie.ComputeRomSha256(_cartridge)[..8];
        return Path.Combine(
            StateStorageDirectory(),
            $"{romName}-{hash}-slot{Math.Clamp(slot, 1, 10)}.mdss");
    }

    private static string SafeFileName(string name)
    {
        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalid, '_');
        }

        return string.IsNullOrWhiteSpace(name) ? "rom" : name;
    }

    private static bool TrySlotKey(Keys keyCode, out int slot)
    {
        slot = keyCode switch
        {
            Keys.D1 or Keys.NumPad1 => 1,
            Keys.D2 or Keys.NumPad2 => 2,
            Keys.D3 or Keys.NumPad3 => 3,
            Keys.D4 or Keys.NumPad4 => 4,
            Keys.D5 or Keys.NumPad5 => 5,
            Keys.D6 or Keys.NumPad6 => 6,
            Keys.D7 or Keys.NumPad7 => 7,
            Keys.D8 or Keys.NumPad8 => 8,
            Keys.D9 or Keys.NumPad9 => 9,
            Keys.D0 or Keys.NumPad0 => 10,
            _ => 0,
        };
        return slot != 0;
    }

    private static string BuildMovieSummary(string moviePath, InputMovie movie, string targetRomPath, bool hashMatches)
    {
        bool hasPlayer2Input = movie.Frames.Any(frame => frame.Player2Buttons != 0);
        string movieHash = string.IsNullOrWhiteSpace(movie.RomSha256) ? "(not recorded)" : ShortHash(movie.RomSha256);
        return string.Join(
            Environment.NewLine,
            $"Movie: {Path.GetFileName(moviePath)}",
            $"ROM: {Path.GetFileName(targetRomPath)}",
            $"Recorded ROM: {movie.RomName ?? "(unknown)"}",
            $"Product code: {movie.RomProductCode ?? "(unknown)"}",
            $"Frames: {movie.FrameCount:N0}",
            $"Two-player input: {(hasPlayer2Input ? "yes" : "no")}",
            $"Initial SRAM: {(!string.IsNullOrWhiteSpace(movie.SaveRamBase64) ? "included" : "none")}",
            $"Recorded hash: {movieHash}",
            $"ROM hash: {(hashMatches ? "match" : "mismatch")}",
            string.Empty,
            hashMatches ? "Play this input movie?" : "The ROM hash does not match. Play this input movie anyway?");
    }

    private static string ShortHash(string hash)
    {
        return hash.Length <= 12 ? hash : hash[..12];
    }

    private void SaveDesktopSessionSettings()
    {
        Rectangle bounds = _fullscreen ? _windowedBounds : WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
        if (bounds.Width >= MinimumSize.Width && bounds.Height >= MinimumSize.Height)
        {
            _settings.WindowWidth = bounds.Width;
            _settings.WindowHeight = bounds.Height;
            _settings.WindowLeft = bounds.Left;
            _settings.WindowTop = bounds.Top;
        }

        _settings.WindowMaximized = !_fullscreen && WindowState == FormWindowState.Maximized;
        _settings.StartFullscreen = _fullscreen;
        _settings.Muted = _muted;
        _settings.InstructionBudget = _instructionsPerFrame;
        _settings.NormalizeSession();
    }

    private void ApplyInitialDirectory(FileDialog dialog)
    {
        string? directory = CurrentFileDialogDirectory();
        if (!string.IsNullOrWhiteSpace(directory))
        {
            dialog.InitialDirectory = directory;
        }
    }

    private void ApplyRomInitialDirectory(FileDialog dialog)
    {
        string? directory = !string.IsNullOrWhiteSpace(_settings.DefaultRomDirectory) && Directory.Exists(_settings.DefaultRomDirectory)
            ? _settings.DefaultRomDirectory
            : CurrentFileDialogDirectory();
        if (!string.IsNullOrWhiteSpace(directory))
        {
            dialog.InitialDirectory = directory;
        }
    }

    private void ApplyStateInitialDirectory(FileDialog dialog)
    {
        string? directory = !string.IsNullOrWhiteSpace(_settings.StateDirectory) && Directory.Exists(_settings.StateDirectory)
            ? _settings.StateDirectory
            : CurrentFileDialogDirectory();
        if (!string.IsNullOrWhiteSpace(directory))
        {
            dialog.InitialDirectory = directory;
        }
    }

    private string StateStorageDirectory()
    {
        return !string.IsNullOrWhiteSpace(_settings.StateDirectory)
            ? Path.GetFullPath(_settings.StateDirectory)
            : DesktopPaths.StateDirectory;
    }

    private string? CurrentFileDialogDirectory()
    {
        string? directory = _romPath is null
            ? _settings.LastRomDirectory
            : Path.GetDirectoryName(Path.GetFullPath(_romPath));
        return !string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory)
            ? directory
            : null;
    }

    private static string DisplayName(CartridgeImage cartridge)
    {
        if (!string.IsNullOrWhiteSpace(cartridge.Header.DomesticName))
        {
            return cartridge.Header.DomesticName.Trim();
        }

        return string.IsNullOrWhiteSpace(cartridge.Header.OverseasName)
            ? cartridge.Header.ProductCode.Trim()
            : cartridge.Header.OverseasName.Trim();
    }
}

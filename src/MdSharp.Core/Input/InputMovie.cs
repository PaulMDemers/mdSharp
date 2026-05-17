using MdSharp.Core.Cartridge;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MdSharp.Core.Input;

public sealed class InputMovie
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public int Version { get; set; } = 2;
    public string Emulator { get; set; } = "mdSharp";
    public string? RomPath { get; set; }
    public string? RomName { get; set; }
    public string? RomProductCode { get; set; }
    public string? RomSha256 { get; set; }
    public string? SaveRamBase64 { get; set; }
    public int InitialFrame { get; set; }
    public List<InputMovieFrame> Frames { get; set; } = [];

    [JsonIgnore]
    public int FrameCount => Frames.Count;

    public static InputMovie Create(string romPath, CartridgeImage cartridge)
    {
        return new InputMovie
        {
            RomPath = Path.GetFullPath(romPath),
            RomName = Path.GetFileName(romPath),
            RomProductCode = cartridge.Header.ProductCode.Trim(),
            RomSha256 = ComputeRomSha256(cartridge),
            SaveRamBase64 = Convert.ToBase64String(cartridge.CaptureSaveRam()),
        };
    }

    public static InputMovie Load(string path)
    {
        InputMovie? movie = JsonSerializer.Deserialize<InputMovie>(File.ReadAllText(path), JsonOptions);
        if (movie is null || movie.Version is not (1 or 2))
        {
            throw new InvalidDataException("Unsupported mdSharp input movie file.");
        }

        movie.Frames ??= [];
        movie.Frames.Sort((left, right) => left.Frame.CompareTo(right.Frame));
        foreach (InputMovieFrame frame in movie.Frames)
        {
            frame.NormalizeLegacyButtons();
        }

        return movie;
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
        File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
    }

    public GenesisButton GetButtons(int frame)
    {
        return GetButtons(frame, playerIndex: 0);
    }

    public GenesisButton GetButtons(int frame, int playerIndex)
    {
        int index = frame - InitialFrame;
        if ((uint)index >= Frames.Count)
        {
            return GenesisButton.None;
        }

        return Frames[index].GetButtons(playerIndex);
    }

    public void AddFrame(int frame, GenesisButton buttons)
    {
        AddFrame(frame, buttons, buttons);
    }

    public void AddFrame(int frame, GenesisButton player1Buttons, GenesisButton player2Buttons)
    {
        Frames.Add(new InputMovieFrame(frame, player1Buttons, player2Buttons));
    }

    public bool Matches(CartridgeImage cartridge)
    {
        return string.IsNullOrWhiteSpace(RomSha256)
            || string.Equals(RomSha256, ComputeRomSha256(cartridge), StringComparison.OrdinalIgnoreCase);
    }

    public void RestoreInitialSaveRam(CartridgeImage cartridge)
    {
        if (string.IsNullOrWhiteSpace(SaveRamBase64))
        {
            return;
        }

        cartridge.RestoreSaveRam(Convert.FromBase64String(SaveRamBase64));
    }

    public static string ComputeRomSha256(CartridgeImage cartridge)
    {
        return Convert.ToHexString(SHA256.HashData(cartridge.Rom.Span));
    }
}

public sealed class InputMovieFrame
{
    public InputMovieFrame()
    {
    }

    public InputMovieFrame(int frame, GenesisButton player1Buttons, GenesisButton player2Buttons)
    {
        Frame = frame;
        Player1Buttons = (int)player1Buttons;
        Player2Buttons = (int)player2Buttons;
    }

    public int Frame { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Buttons { get; set; }

    public int Player1Buttons { get; set; }
    public int Player2Buttons { get; set; }

    public GenesisButton GetButtons(int playerIndex)
    {
        return (GenesisButton)(playerIndex == 1 ? Player2Buttons : Player1Buttons);
    }

    public void NormalizeLegacyButtons()
    {
        if (Buttons == 0)
        {
            return;
        }

        if (Player1Buttons == 0)
        {
            Player1Buttons = Buttons;
        }

        if (Player2Buttons == 0)
        {
            Player2Buttons = Buttons;
        }
    }
}

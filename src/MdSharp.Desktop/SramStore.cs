using MdSharp.Core.Cartridge;
using System.Security.Cryptography;
using System.Text;

namespace MdSharp.Desktop;

internal static class SramStore
{
    public static string GetSavePath(string romPath, CartridgeImage cartridge, string? saveDirectory = null)
    {
        string saves = !string.IsNullOrWhiteSpace(saveDirectory)
            ? Path.GetFullPath(saveDirectory)
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "mdSharp",
                "saves");
        string identity = string.IsNullOrWhiteSpace(cartridge.Header.ProductCode)
            ? Path.GetFileNameWithoutExtension(romPath)
            : cartridge.Header.ProductCode.Trim();
        string hash = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(romPath).ToUpperInvariant())))[..8];
        string safeName = string.Concat(identity.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
        return Path.Combine(saves, $"{safeName}-{hash}.srm");
    }

    public static void Load(string romPath, CartridgeImage cartridge, string? saveDirectory = null)
    {
        string path = GetSavePath(romPath, cartridge, saveDirectory);
        if (!File.Exists(path))
        {
            return;
        }

        cartridge.RestoreSaveRam(File.ReadAllBytes(path));
    }

    public static void Save(string romPath, CartridgeImage cartridge, string? saveDirectory = null)
    {
        string path = GetSavePath(romPath, cartridge, saveDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        File.WriteAllBytes(path, cartridge.CaptureSaveRam());
    }
}

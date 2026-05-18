namespace MdSharp.Desktop;

internal static class DesktopPaths
{
    private const string PortableMarkerFile = "mdsharp-portable.json";
    private const string PortableFolderName = "portable";

    public static string RootDirectory
    {
        get
        {
            string baseDirectory = AppContext.BaseDirectory;
            string portableFolder = Path.Combine(baseDirectory, PortableFolderName);
            string portableMarker = Path.Combine(baseDirectory, PortableMarkerFile);
            if (Directory.Exists(portableFolder) || File.Exists(portableMarker))
            {
                return portableFolder;
            }

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "mdSharp");
        }
    }

    public static bool IsPortable => RootDirectory.Equals(Path.Combine(AppContext.BaseDirectory, PortableFolderName), StringComparison.OrdinalIgnoreCase);

    public static string SettingsPath => Path.Combine(RootDirectory, "desktop-settings.json");

    public static string SaveRamDirectory => Path.Combine(RootDirectory, "saves");

    public static string StateDirectory => Path.Combine(RootDirectory, "states");
}

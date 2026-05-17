using System.Reflection;

namespace MdSharp.Core;

public static class AppInfo
{
    public const string Name = "mdSharp";
    public const string RepositoryUrl = "https://github.com/PaulMDemers/mdSharp";
    public const string LicenseName = "MIT License";

    public static string Version
    {
        get
        {
            Assembly assembly = typeof(AppInfo).Assembly;
            return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? assembly.GetName().Version?.ToString()
                ?? "unknown";
        }
    }

    public static string DisplayVersion => Version.Split('+')[0];
}

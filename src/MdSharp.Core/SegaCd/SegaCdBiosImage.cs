using System.Security.Cryptography;

namespace MdSharp.Core.SegaCd;

public sealed record SegaCdBiosImage(
    SegaCdRegion Region,
    string Path,
    long Size,
    string Sha1,
    bool FromEnvironment)
{
    public string FileName => System.IO.Path.GetFileName(Path);

    public static SegaCdBiosImage FromFile(SegaCdRegion region, string path, bool fromEnvironment = false)
    {
        FileInfo info = new(path);
        using FileStream stream = File.OpenRead(path);
        byte[] hash = SHA1.HashData(stream);
        return new SegaCdBiosImage(
            region,
            info.FullName,
            info.Length,
            Convert.ToHexString(hash).ToLowerInvariant(),
            fromEnvironment);
    }
}

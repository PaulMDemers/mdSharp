using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace MdSharp.Core.SegaCd;

public sealed class DiscImage
{
    private DiscImage(string sourcePath, IReadOnlyList<DiscTrack> tracks)
    {
        SourcePath = sourcePath;
        Tracks = tracks;
    }

    public string SourcePath { get; }
    public IReadOnlyList<DiscTrack> Tracks { get; }
    public int LeadOutLba => Tracks.Count == 0 ? 0 : Tracks[^1].EndLbaExclusive;
    public bool HasAudioTracks => Tracks.Any(track => track.IsAudio);

    public bool TryGetTrackForLba(int lba, out DiscTrack track)
    {
        foreach (DiscTrack candidate in Tracks)
        {
            if (lba >= candidate.StartLba && lba < candidate.EndLbaExclusive)
            {
                track = candidate;
                return true;
            }
        }

        track = default!;
        return false;
    }

    public bool TryReadDataSector2048(int lba, Span<byte> destination)
    {
        if (destination.Length < 2048 ||
            !TryGetTrackForLba(lba, out DiscTrack track) ||
            track.Kind != DiscTrackKind.Data)
        {
            return false;
        }

        int relativeSector = lba - track.StartLba;
        long offset = track.FileOffsetBytes + ((long)relativeSector * track.SectorSize);
        Span<byte> raw = stackalloc byte[2352];
        if (!File.Exists(track.FilePath) || offset < 0)
        {
            return false;
        }

        using FileStream stream = File.OpenRead(track.FilePath);
        if (offset + track.SectorSize > stream.Length)
        {
            return false;
        }

        stream.Position = offset;
        stream.ReadExactly(raw[..track.SectorSize]);
        ReadOnlySpan<byte> userData = track.Mode switch
        {
            DiscTrackMode.Mode1_2048 => raw[..2048],
            DiscTrackMode.Mode1_2352 => raw.Slice(16, 2048),
            DiscTrackMode.Mode2_2336 => raw[..2048],
            DiscTrackMode.Mode2_2352 => raw.Slice(24, 2048),
            _ => default,
        };

        if (userData.Length < 2048)
        {
            return false;
        }

        userData[..2048].CopyTo(destination);
        return true;
    }

    public bool TryReadAudioSector2352(int lba, Span<byte> destination)
    {
        if (destination.Length < 2352 ||
            !TryGetTrackForLba(lba, out DiscTrack track) ||
            track.Kind != DiscTrackKind.Audio ||
            track.SectorSize != 2352)
        {
            return false;
        }

        int relativeSector = lba - track.StartLba;
        long offset = track.FileOffsetBytes + ((long)relativeSector * track.SectorSize);
        if (!File.Exists(track.FilePath) || offset < 0)
        {
            return false;
        }

        using FileStream stream = File.OpenRead(track.FilePath);
        if (offset + track.SectorSize > stream.Length)
        {
            return false;
        }

        stream.Position = offset;
        stream.ReadExactly(destination[..2352]);
        return true;
    }

    public bool TryReadIso9660File(string fileIdentifier, out byte[] data)
    {
        data = [];
        if (string.IsNullOrWhiteSpace(fileIdentifier))
        {
            return false;
        }

        Span<byte> sector = stackalloc byte[2048];
        if (!TryReadDataSector2048(16, sector) ||
            !sector[1..6].SequenceEqual("CD001"u8) ||
            sector[0] != 0x01)
        {
            return false;
        }

        if (!TryParseIsoDirectoryRecord(sector[156..], out IsoDirectoryRecord root) ||
            root.ExtentLba < 0 ||
            root.DataLength <= 0)
        {
            return false;
        }

        if (!TryReadIsoExtent(root.ExtentLba, root.DataLength, out byte[] rootDirectory))
        {
            return false;
        }

        string normalized = NormalizeIsoIdentifier(fileIdentifier);
        int offset = 0;
        while (offset < rootDirectory.Length)
        {
            int recordLength = rootDirectory[offset];
            if (recordLength == 0)
            {
                offset = ((offset / 2048) + 1) * 2048;
                continue;
            }

            if (offset + recordLength > rootDirectory.Length)
            {
                break;
            }

            if (TryParseIsoDirectoryRecord(rootDirectory.AsSpan(offset, recordLength), out IsoDirectoryRecord record) &&
                !record.IsDirectory &&
                IsoIdentifierMatches(record.Identifier, normalized) &&
                record.ExtentLba >= 0 &&
                record.DataLength >= 0)
            {
                return TryReadIsoExtent(record.ExtentLba, record.DataLength, out data);
            }

            offset += recordLength;
        }

        return false;
    }

    public static DiscImage FromFile(string path)
    {
        string fullPath = Path.GetFullPath(path);
        string extension = Path.GetExtension(fullPath).ToLowerInvariant();
        return extension switch
        {
            ".cue" => ParseCue(fullPath),
            ".iso" => FromIso(fullPath),
            ".chd" => ParseCue(ExtractChdToCue(fullPath)),
            _ => throw new NotSupportedException($"Unsupported Sega CD image extension '{extension}'. Use .cue, .iso, or .chd."),
        };
    }

    private static string ExtractChdToCue(string chdPath)
    {
        string? chdman = FindChdManExecutable();
        if (chdman is null)
        {
            throw new NotSupportedException("CHD Sega CD images require chdman. Put chdman beside mdSharp, place it on PATH, or set MDSHARP_CHDMAN to the chdman executable.");
        }

        FileInfo source = new(chdPath);
        string cacheRoot = GetChdCacheRoot();
        string cacheKey = $"{source.FullName}|{source.Length}|{source.LastWriteTimeUtc.Ticks}";
        string cacheId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(cacheKey)))[..24].ToLowerInvariant();
        string cacheDirectory = Path.Combine(cacheRoot, cacheId);
        Directory.CreateDirectory(cacheDirectory);
        string outputCue = Path.Combine(cacheDirectory, Path.GetFileNameWithoutExtension(source.Name) + ".cue");
        string outputBin = Path.Combine(cacheDirectory, Path.GetFileNameWithoutExtension(source.Name) + ".bin");

        if (File.Exists(outputCue) && File.Exists(outputBin))
        {
            return outputCue;
        }

        foreach (string stale in Directory.EnumerateFiles(cacheDirectory))
        {
            File.Delete(stale);
        }

        ProcessStartInfo startInfo = new()
        {
            FileName = chdman,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("extractcd");
        startInfo.ArgumentList.Add("-i");
        startInfo.ArgumentList.Add(source.FullName);
        startInfo.ArgumentList.Add("-o");
        startInfo.ArgumentList.Add(outputCue);
        startInfo.ArgumentList.Add("-ob");
        startInfo.ArgumentList.Add(outputBin);
        startInfo.ArgumentList.Add("-f");

        using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start chdman.");
        Task<string> standardOutputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> standardErrorTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30 * 60 * 1000))
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }

            throw new TimeoutException($"chdman timed out while extracting '{source.Name}'.");
        }

        string standardOutput = standardOutputTask.GetAwaiter().GetResult();
        string standardError = standardErrorTask.GetAwaiter().GetResult();
        if (process.ExitCode != 0 || !File.Exists(outputCue))
        {
            string detail = string.Join(Environment.NewLine, [standardOutput.Trim(), standardError.Trim()]).Trim();
            throw new InvalidOperationException($"chdman failed to extract '{source.Name}'." + (detail.Length == 0 ? string.Empty : Environment.NewLine + detail));
        }

        return outputCue;
    }

    private static string GetChdCacheRoot()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            localAppData = Path.GetTempPath();
        }

        return Path.Combine(localAppData, "MdSharp", "chd-cache");
    }

    private static string? FindChdManExecutable()
    {
        string executableName = OperatingSystem.IsWindows() ? "chdman.exe" : "chdman";
        string? env = Environment.GetEnvironmentVariable("MDSHARP_CHDMAN");
        if (!string.IsNullOrWhiteSpace(env) && File.Exists(env))
        {
            return Path.GetFullPath(env);
        }

        foreach (string directory in CandidateChdManDirectories())
        {
            string candidate = Path.Combine(directory, executableName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        string? path = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(path))
        {
            foreach (string directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                try
                {
                    string candidate = Path.Combine(directory, executableName);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
                catch (ArgumentException)
                {
                }
            }
        }

        return null;
    }

    private static IEnumerable<string> CandidateChdManDirectories()
    {
        yield return AppContext.BaseDirectory;
        yield return Environment.CurrentDirectory;
        yield return Path.Combine(Environment.CurrentDirectory, "render-output", "reference-emulators", "mame", "mame0287");
    }

    public static int MsfToFrames(string value)
    {
        string[] parts = value.Split(':');
        if (parts.Length != 3 ||
            !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int minutes) ||
            !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int seconds) ||
            !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out int frames) ||
            seconds is < 0 or >= 60 ||
            frames is < 0 or >= 75 ||
            minutes < 0)
        {
            throw new FormatException($"Invalid CUE MSF timestamp '{value}'.");
        }

        return ((minutes * 60) + seconds) * 75 + frames;
    }

    private static DiscImage FromIso(string path)
    {
        FileInfo info = new(path);
        int frames = checked((int)((info.Length + 2047) / 2048));
        DiscTrack track = new(
            Number: 1,
            Kind: DiscTrackKind.Data,
            Mode: DiscTrackMode.Mode1_2048,
            FilePath: info.FullName,
            FileOffsetBytes: 0,
            SectorSize: 2048,
            StartLba: 0,
            LengthFrames: frames,
            PregapFrames: 0);
        return new DiscImage(info.FullName, [track]);
    }

    private static DiscImage ParseCue(string path)
    {
        string cueDirectory = Path.GetDirectoryName(path) ?? Environment.CurrentDirectory;
        List<ParsedTrack> parsedTracks = [];
        string? currentFile = null;
        string currentFileType = "BINARY";
        ParsedTrack? currentTrack = null;

        foreach (string rawLine in File.ReadLines(path, Encoding.UTF8))
        {
            string line = StripComment(rawLine).Trim();
            if (line.Length == 0)
            {
                continue;
            }

            Match fileMatch = Regex.Match(line, "^FILE\\s+\"(?<file>[^\"]+)\"\\s+(?<type>\\S+)$", RegexOptions.IgnoreCase);
            if (fileMatch.Success)
            {
                currentFile = Path.GetFullPath(Path.Combine(cueDirectory, fileMatch.Groups["file"].Value));
                currentFileType = fileMatch.Groups["type"].Value.ToUpperInvariant();
                currentTrack = null;
                continue;
            }

            Match trackMatch = Regex.Match(line, "^TRACK\\s+(?<number>\\d+)\\s+(?<mode>\\S+)$", RegexOptions.IgnoreCase);
            if (trackMatch.Success)
            {
                if (currentFile is null)
                {
                    throw new FormatException("CUE TRACK appeared before FILE.");
                }

                int number = int.Parse(trackMatch.Groups["number"].Value, CultureInfo.InvariantCulture);
                DiscTrackMode mode = ParseTrackMode(trackMatch.Groups["mode"].Value);
                currentTrack = new ParsedTrack(number, currentFile, currentFileType, mode);
                parsedTracks.Add(currentTrack);
                continue;
            }

            Match indexMatch = Regex.Match(line, "^INDEX\\s+(?<index>\\d+)\\s+(?<msf>\\d{1,3}:\\d{2}:\\d{2})$", RegexOptions.IgnoreCase);
            if (indexMatch.Success)
            {
                if (currentTrack is null)
                {
                    throw new FormatException("CUE INDEX appeared before TRACK.");
                }

                int index = int.Parse(indexMatch.Groups["index"].Value, CultureInfo.InvariantCulture);
                int frames = MsfToFrames(indexMatch.Groups["msf"].Value);
                if (index == 0)
                {
                    currentTrack.Index00Frames = frames;
                }
                else if (index == 1)
                {
                    currentTrack.Index01Frames = frames;
                }

                continue;
            }

            Match pregapMatch = Regex.Match(line, "^PREGAP\\s+(?<msf>\\d{1,3}:\\d{2}:\\d{2})$", RegexOptions.IgnoreCase);
            if (pregapMatch.Success)
            {
                if (currentTrack is null)
                {
                    throw new FormatException("CUE PREGAP appeared before TRACK.");
                }

                currentTrack.PregapFrames = MsfToFrames(pregapMatch.Groups["msf"].Value);
            }
        }

        if (parsedTracks.Count == 0)
        {
            throw new FormatException("CUE file does not contain any tracks.");
        }

        List<DiscTrack> tracks = [];
        int lba = 0;
        for (int i = 0; i < parsedTracks.Count; i++)
        {
            ParsedTrack parsed = parsedTracks[i];
            if (parsed.Index01Frames is null)
            {
                throw new FormatException($"CUE track {parsed.Number:D2} is missing INDEX 01.");
            }

            int sectorSize = SectorSizeForMode(parsed.Mode);
            FilePayload payload = GetFilePayload(parsed.FilePath, parsed.FileType, parsed.Mode, sectorSize);
            long fileOffset = payload.OffsetBytes + ((long)parsed.Index01Frames.Value * sectorSize);
            int lengthFrames = ComputeTrackLength(parsedTracks, i, sectorSize, payload);
            DiscTrackKind kind = parsed.Mode == DiscTrackMode.Audio ? DiscTrackKind.Audio : DiscTrackKind.Data;
            tracks.Add(new DiscTrack(
                parsed.Number,
                kind,
                parsed.Mode,
                parsed.FilePath,
                fileOffset,
                sectorSize,
                lba,
                lengthFrames,
                parsed.PregapFrames));
            lba += lengthFrames;
        }

        return new DiscImage(Path.GetFullPath(path), tracks);
    }

    private static int ComputeTrackLength(List<ParsedTrack> tracks, int index, int sectorSize, FilePayload payload)
    {
        ParsedTrack current = tracks[index];
        int start = current.Index01Frames ?? 0;
        for (int i = index + 1; i < tracks.Count; i++)
        {
            if (string.Equals(tracks[i].FilePath, current.FilePath, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(tracks[i].FileType, current.FileType, StringComparison.OrdinalIgnoreCase) &&
                tracks[i].Index01Frames is int nextStart)
            {
                return Math.Max(0, nextStart - start);
            }
        }

        if (!File.Exists(current.FilePath))
        {
            return 0;
        }

        long fileSectors = payload.LengthBytes / sectorSize;
        return checked((int)Math.Max(0, fileSectors - start));
    }

    private static FilePayload GetFilePayload(string path, string fileType, DiscTrackMode mode, int sectorSize)
    {
        if (!File.Exists(path))
        {
            return new FilePayload(0, 0);
        }

        if (mode == DiscTrackMode.Audio &&
            (fileType.Equals("WAVE", StringComparison.OrdinalIgnoreCase) ||
             fileType.Equals("WAV", StringComparison.OrdinalIgnoreCase) ||
             Path.GetExtension(path).Equals(".wav", StringComparison.OrdinalIgnoreCase)))
        {
            return ReadWavePayload(path);
        }

        return new FilePayload(0, new FileInfo(path).Length);
    }

    private static FilePayload ReadWavePayload(string path)
    {
        using FileStream stream = File.OpenRead(path);
        Span<byte> header = stackalloc byte[12];
        if (stream.Length < header.Length)
        {
            throw new FormatException($"WAV file '{path}' is too small.");
        }

        stream.ReadExactly(header);
        if (!header[..4].SequenceEqual("RIFF"u8) || !header[8..12].SequenceEqual("WAVE"u8))
        {
            throw new FormatException($"WAV file '{path}' does not contain a RIFF/WAVE header.");
        }

        bool sawPcmFormat = false;
        Span<byte> chunkHeader = stackalloc byte[8];
        Span<byte> fmt = stackalloc byte[16];
        while (stream.Position + 8 <= stream.Length)
        {
            stream.ReadExactly(chunkHeader);
            uint chunkSize = BinaryPrimitives.ReadUInt32LittleEndian(chunkHeader[4..8]);
            long chunkDataOffset = stream.Position;
            string chunkId = Encoding.ASCII.GetString(chunkHeader[..4]);

            if (chunkId == "fmt ")
            {
                if (chunkSize < fmt.Length)
                {
                    throw new FormatException($"WAV file '{path}' has a truncated fmt chunk.");
                }

                stream.ReadExactly(fmt);
                ushort formatTag = BinaryPrimitives.ReadUInt16LittleEndian(fmt[..2]);
                ushort channels = BinaryPrimitives.ReadUInt16LittleEndian(fmt[2..4]);
                uint sampleRate = BinaryPrimitives.ReadUInt32LittleEndian(fmt[4..8]);
                ushort bitsPerSample = BinaryPrimitives.ReadUInt16LittleEndian(fmt[14..16]);
                sawPcmFormat = formatTag == 1 && channels == 2 && sampleRate == 44_100 && bitsPerSample == 16;
                if (!sawPcmFormat)
                {
                    throw new FormatException($"WAV file '{path}' must be 44.1 kHz stereo 16-bit PCM for Sega CD audio tracks.");
                }
            }
            else if (chunkId == "data")
            {
                if (!sawPcmFormat)
                {
                    throw new FormatException($"WAV file '{path}' has a data chunk before a supported fmt chunk.");
                }

                return new FilePayload(chunkDataOffset, chunkSize);
            }

            long nextChunk = chunkDataOffset + chunkSize + (chunkSize & 1);
            if (nextChunk < stream.Position || nextChunk > stream.Length)
            {
                throw new FormatException($"WAV file '{path}' has an invalid chunk length.");
            }

            stream.Position = nextChunk;
        }

        throw new FormatException($"WAV file '{path}' does not contain a data chunk.");
    }

    private static DiscTrackMode ParseTrackMode(string text)
    {
        string normalized = text.ToUpperInvariant().Replace('/', '_');
        return normalized switch
        {
            "AUDIO" => DiscTrackMode.Audio,
            "MODE1_2048" => DiscTrackMode.Mode1_2048,
            "MODE1_2352" => DiscTrackMode.Mode1_2352,
            "MODE2_2336" => DiscTrackMode.Mode2_2336,
            "MODE2_2352" => DiscTrackMode.Mode2_2352,
            _ => DiscTrackMode.Unknown,
        };
    }

    private static int SectorSizeForMode(DiscTrackMode mode)
    {
        return mode switch
        {
            DiscTrackMode.Audio => 2352,
            DiscTrackMode.Mode1_2048 => 2048,
            DiscTrackMode.Mode1_2352 => 2352,
            DiscTrackMode.Mode2_2336 => 2336,
            DiscTrackMode.Mode2_2352 => 2352,
            _ => 2352,
        };
    }

    private static string StripComment(string line)
    {
        int index = line.IndexOf(';');
        return index >= 0 ? line[..index] : line;
    }

    private bool TryReadIsoExtent(int extentLba, int byteLength, out byte[] data)
    {
        data = [];
        if (extentLba < 0 || byteLength < 0)
        {
            return false;
        }

        data = new byte[byteLength];
        Span<byte> sector = stackalloc byte[2048];
        int copied = 0;
        for (int lba = extentLba; copied < byteLength; lba++)
        {
            if (!TryReadDataSector2048(lba, sector))
            {
                data = [];
                return false;
            }

            int copyLength = Math.Min(2048, byteLength - copied);
            sector[..copyLength].CopyTo(data.AsSpan(copied, copyLength));
            copied += copyLength;
        }

        return true;
    }

    private static bool TryParseIsoDirectoryRecord(ReadOnlySpan<byte> source, out IsoDirectoryRecord record)
    {
        record = default;
        if (source.Length < 34 || source[0] < 34 || source[0] > source.Length)
        {
            return false;
        }

        int nameLength = source[32];
        if (33 + nameLength > source[0])
        {
            return false;
        }

        string identifier = nameLength switch
        {
            1 when source[33] == 0 => ".",
            1 when source[33] == 1 => "..",
            _ => Encoding.ASCII.GetString(source.Slice(33, nameLength)),
        };

        record = new IsoDirectoryRecord(
            (int)BinaryPrimitives.ReadUInt32LittleEndian(source[2..6]),
            (int)BinaryPrimitives.ReadUInt32LittleEndian(source[10..14]),
            (source[25] & 0x02) != 0,
            identifier);
        return true;
    }

    private static bool IsoIdentifierMatches(string actual, string normalizedTarget)
    {
        string normalizedActual = NormalizeIsoIdentifier(actual);
        if (normalizedActual.Equals(normalizedTarget, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        int versionIndex = normalizedActual.IndexOf(';', StringComparison.Ordinal);
        string unversionedActual = versionIndex >= 0 ? normalizedActual[..versionIndex] : normalizedActual;
        int targetVersionIndex = normalizedTarget.IndexOf(';', StringComparison.Ordinal);
        string unversionedTarget = targetVersionIndex >= 0 ? normalizedTarget[..targetVersionIndex] : normalizedTarget;
        return unversionedActual.Equals(unversionedTarget, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeIsoIdentifier(string identifier)
    {
        return identifier.Trim().Replace('\\', '/').ToUpperInvariant();
    }

    private readonly record struct FilePayload(long OffsetBytes, long LengthBytes);
    private readonly record struct IsoDirectoryRecord(int ExtentLba, int DataLength, bool IsDirectory, string Identifier);

    private sealed class ParsedTrack(int number, string filePath, string fileType, DiscTrackMode mode)
    {
        public int Number { get; } = number;
        public string FilePath { get; } = filePath;
        public string FileType { get; } = fileType;
        public DiscTrackMode Mode { get; } = mode;
        public int? Index00Frames { get; set; }
        public int? Index01Frames { get; set; }
        public int PregapFrames { get; set; }
    }
}

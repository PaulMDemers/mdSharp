using System.Globalization;
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

    public static DiscImage FromFile(string path)
    {
        string fullPath = Path.GetFullPath(path);
        string extension = Path.GetExtension(fullPath).ToLowerInvariant();
        return extension switch
        {
            ".cue" => ParseCue(fullPath),
            ".iso" => FromIso(fullPath),
            _ => throw new NotSupportedException($"Unsupported Sega CD image extension '{extension}'. Use .cue or .iso for the first bring-up path."),
        };
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
                currentTrack = new ParsedTrack(number, currentFile, mode);
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
            long fileOffset = (long)parsed.Index01Frames.Value * sectorSize;
            int lengthFrames = ComputeTrackLength(parsedTracks, i, sectorSize);
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

    private static int ComputeTrackLength(List<ParsedTrack> tracks, int index, int sectorSize)
    {
        ParsedTrack current = tracks[index];
        int start = current.Index01Frames ?? 0;
        for (int i = index + 1; i < tracks.Count; i++)
        {
            if (string.Equals(tracks[i].FilePath, current.FilePath, StringComparison.OrdinalIgnoreCase) &&
                tracks[i].Index01Frames is int nextStart)
            {
                return Math.Max(0, nextStart - start);
            }
        }

        if (!File.Exists(current.FilePath))
        {
            return 0;
        }

        long fileSectors = new FileInfo(current.FilePath).Length / sectorSize;
        return checked((int)Math.Max(0, fileSectors - start));
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

    private sealed class ParsedTrack(int number, string filePath, DiscTrackMode mode)
    {
        public int Number { get; } = number;
        public string FilePath { get; } = filePath;
        public DiscTrackMode Mode { get; } = mode;
        public int? Index00Frames { get; set; }
        public int? Index01Frames { get; set; }
        public int PregapFrames { get; set; }
    }
}

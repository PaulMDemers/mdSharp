namespace MdSharp.Core.SegaCd;

public sealed record DiscTrack(
    int Number,
    DiscTrackKind Kind,
    DiscTrackMode Mode,
    string FilePath,
    long FileOffsetBytes,
    int SectorSize,
    int StartLba,
    int LengthFrames,
    int PregapFrames)
{
    public bool IsAudio => Kind == DiscTrackKind.Audio;
    public int EndLbaExclusive => StartLba + LengthFrames;
}

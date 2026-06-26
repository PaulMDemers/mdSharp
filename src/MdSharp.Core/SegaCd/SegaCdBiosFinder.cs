namespace MdSharp.Core.SegaCd;

public static class SegaCdBiosFinder
{
    private static readonly IReadOnlyDictionary<SegaCdRegion, string> EnvironmentVariables =
        new Dictionary<SegaCdRegion, string>
        {
            [SegaCdRegion.Usa] = "MDSHARP_SEGACD_BIOS_US",
            [SegaCdRegion.Europe] = "MDSHARP_SEGACD_BIOS_EU",
            [SegaCdRegion.Japan] = "MDSHARP_SEGACD_BIOS_JP",
        };

    private static readonly string[] LocalFolders =
    [
        "Sega CD BIOS",
        "Sega Mega CD BIOS Set v1",
    ];

    public static IReadOnlyList<SegaCdBiosImage> FindAll(string? baseDirectory = null)
    {
        string root = Path.GetFullPath(baseDirectory ?? Environment.CurrentDirectory);
        List<SegaCdBiosImage> results = [];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

        foreach ((SegaCdRegion region, string variable) in EnvironmentVariables)
        {
            string? path = Environment.GetEnvironmentVariable(variable);
            if (!string.IsNullOrWhiteSpace(path) && TryAdd(path, region, fromEnvironment: true, results, seen))
            {
                continue;
            }
        }

        foreach (string folder in LocalFolders)
        {
            string folderPath = Path.Combine(root, folder);
            if (!Directory.Exists(folderPath))
            {
                continue;
            }

            foreach (string file in Directory.EnumerateFiles(folderPath, "*.bin", SearchOption.AllDirectories))
            {
                if (TryInferRegion(file, out SegaCdRegion region))
                {
                    TryAdd(file, region, fromEnvironment: false, results, seen);
                }
            }
        }

        return results
            .OrderBy(candidate => candidate.Region)
            .ThenByDescending(candidate => candidate.FromEnvironment)
            .ThenBy(candidate => CandidateScore(candidate.Region, candidate.Path))
            .ThenBy(candidate => candidate.FileName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static SegaCdBiosImage? FindBest(SegaCdRegion region, string? baseDirectory = null)
    {
        return FindAll(baseDirectory)
            .Where(candidate => candidate.Region == region)
            .OrderByDescending(candidate => candidate.FromEnvironment)
            .ThenBy(candidate => CandidateScore(candidate.Region, candidate.Path))
            .ThenBy(candidate => candidate.FileName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    public static bool TryInferRegion(string path, out SegaCdRegion region)
    {
        string name = Path.GetFileName(path).ToUpperInvariant();
        if (name.Contains("BIOS_CD_U", StringComparison.Ordinal) ||
            name.Contains("SEGA CD (U)", StringComparison.Ordinal) ||
            name.Contains("(USA", StringComparison.Ordinal) ||
            name.Contains("(U)", StringComparison.Ordinal) ||
            name.Contains(" USA", StringComparison.Ordinal))
        {
            region = SegaCdRegion.Usa;
            return true;
        }

        if (name.Contains("BIOS_CD_E", StringComparison.Ordinal) ||
            name.Contains("MEGA CD (E", StringComparison.Ordinal) ||
            name.Contains("(EUROPE", StringComparison.Ordinal) ||
            name.Contains("(E)", StringComparison.Ordinal) ||
            name.Contains(" EUROPE", StringComparison.Ordinal))
        {
            region = SegaCdRegion.Europe;
            return true;
        }

        if (name.Contains("BIOS_CD_J", StringComparison.Ordinal) ||
            name.Contains("MEGA CD (J", StringComparison.Ordinal) ||
            name.Contains("(JAPAN", StringComparison.Ordinal) ||
            name.Contains("(J)", StringComparison.Ordinal) ||
            name.Contains(" JAPAN", StringComparison.Ordinal))
        {
            region = SegaCdRegion.Japan;
            return true;
        }

        region = default;
        return false;
    }

    private static bool TryAdd(string path, SegaCdRegion region, bool fromEnvironment, List<SegaCdBiosImage> results, HashSet<string> seen)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return false;
        }

        string fullPath = Path.GetFullPath(path);
        FileInfo info = new(fullPath);
        if (info.Length is not (128 * 1024 or 256 * 1024) || !seen.Add(fullPath))
        {
            return false;
        }

        results.Add(SegaCdBiosImage.FromFile(region, fullPath, fromEnvironment));
        return true;
    }

    private static int CandidateScore(SegaCdRegion region, string path)
    {
        string name = Path.GetFileName(path).ToUpperInvariant();
        int score = 0;
        if (name.Contains("NON-WORKING", StringComparison.Ordinal))
        {
            score += 1000;
        }

        if (name.Contains("M1", StringComparison.Ordinal))
        {
            score -= 30;
        }

        if (name.Contains("V1.", StringComparison.Ordinal))
        {
            score -= 20;
        }

        if (name.Contains("M2", StringComparison.Ordinal))
        {
            score += 20;
        }

        if (name.Contains("V2.", StringComparison.Ordinal))
        {
            score += 10;
        }

        if (region == SegaCdRegion.Japan)
        {
            if (name.Contains("J(UE)", StringComparison.Ordinal) || name.Contains("J(E)", StringComparison.Ordinal) || name.Contains("X'EYE", StringComparison.Ordinal))
            {
                score += 50;
            }

            if (name.Contains("MEGA CD (J)", StringComparison.Ordinal) || name.Contains("BIOS_CD_J", StringComparison.Ordinal))
            {
                score -= 40;
            }
        }

        return score;
    }
}

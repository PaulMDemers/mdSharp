param(
    [string]$Root = (Join-Path $PSScriptRoot "..")
)

$ErrorActionPreference = "Stop"

$rootPath = (Resolve-Path $Root).Path
$blockedRoots = @(
    "artifacts/",
    "render-output/",
    "roms/",
    "TestRoms/",
    "movies/"
)
$blockedExtensions = @(
    ".32x",
    ".bin",
    ".eep",
    ".flac",
    ".gen",
    ".gg",
    ".mp3",
    ".ogg",
    ".raw",
    ".rom",
    ".sav",
    ".sms",
    ".smd",
    ".srm",
    ".wav",
    ".mdss"
)
$requiredFiles = @(
    ".gitignore",
    "LICENSE",
    "README.md",
    "artifacts/.gitkeep",
    "render-output/.gitkeep",
    "roms/.gitkeep",
    "TestRoms/.gitkeep",
    "movies/.gitkeep"
)
$requiredIgnorePatterns = @(
    "artifacts/",
    "render-output/",
    "roms/",
    "TestRoms/",
    "movies/",
    "*.flac",
    "*.mp3",
    "*.ogg",
    "svp.bin",
    "*.srm",
    "*.mdss"
)

function Normalize-PathText([string]$Path) {
    return $Path.Replace('\', '/')
}

function Is-AllowedPlaceholder([string]$Path) {
    foreach ($root in $blockedRoots) {
        if ($Path -eq "${root}.gitkeep") {
            return $true
        }
    }

    return $false
}

function Is-AllowedTrackedAsset([string]$Path) {
    return $Path.StartsWith("docs/assets/input-movies/", [StringComparison]::OrdinalIgnoreCase) -and
        ($Path.EndsWith(".mdmovie", [StringComparison]::OrdinalIgnoreCase) -or
            $Path.EndsWith(".mdcheckpoints.json", [StringComparison]::OrdinalIgnoreCase))
}

Push-Location $rootPath
try {
    $trackedFiles = @(git ls-files)
    if ($LASTEXITCODE -ne 0) {
        throw "git ls-files failed."
    }

    $failures = [System.Collections.Generic.List[string]]::new()
    foreach ($path in $trackedFiles) {
        $normalized = Normalize-PathText $path
        if (Is-AllowedPlaceholder $normalized -or Is-AllowedTrackedAsset $normalized) {
            continue
        }

        foreach ($root in $blockedRoots) {
            if ($normalized.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) {
                $failures.Add("Tracked generated/local file under ${root}: $normalized")
                break
            }
        }

        $extension = [System.IO.Path]::GetExtension($normalized)
        if ($blockedExtensions -contains $extension.ToLowerInvariant()) {
            $failures.Add("Tracked local media/runtime file: $normalized")
        }
    }

    foreach ($file in $requiredFiles) {
        if (-not (Test-Path -LiteralPath $file)) {
            $failures.Add("Missing required repository file: $file")
        }
    }

    $gitignore = Get-Content -Path ".gitignore" -Raw
    foreach ($pattern in $requiredIgnorePatterns) {
        if (-not $gitignore.Contains($pattern)) {
            $failures.Add("Missing .gitignore pattern: $pattern")
        }
    }

    if ($failures.Count -gt 0) {
        Write-Error ("Repository hygiene check failed:`n- " + ($failures -join "`n- "))
    }

    Write-Host "Repository hygiene check passed for $($trackedFiles.Count) tracked files."
}
finally {
    Pop-Location
}

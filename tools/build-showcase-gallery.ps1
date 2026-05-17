param(
    [string]$OutputFolder = "docs\assets\showcase",
    [string]$SourceRoot = "render-output",
    [string]$ManifestPath = "render-output\showcase\showcase-manifest.csv"
)

$ErrorActionPreference = "Stop"

$items = @(
    @{
        Source = "release-gate-smoke\visual-checkpoints\screenshots\sonic1-start.bmp"
        Target = "sonic1-green-hill.png"
        Title = "Sonic the Hedgehog"
    },
    @{
        Source = "release-gate-smoke\visual-checkpoints\screenshots\sonic2-idle-split.bmp"
        Target = "sonic2-split-screen.png"
        Title = "Sonic the Hedgehog 2 split-screen demo"
    },
    @{
        Source = "release-gate-smoke\visual-checkpoints\screenshots\sonic3-special-preview.bmp"
        Target = "sonic3-special-stage.png"
        Title = "Sonic the Hedgehog 3 special-stage path"
    },
    @{
        Source = "release-gate-smoke\visual-checkpoints\screenshots\streets-hud.bmp"
        Target = "streets-of-rage-intro.png"
        Title = "Streets of Rage intro/HUD path"
    },
    @{
        Source = "bloodlines-bench-shiftfix\gameplay-run.bmp"
        Target = "castlevania-bloodlines-gameplay.png"
        Title = "Castlevania: Bloodlines gameplay"
    },
    @{
        Source = "release-gate-smoke\visual-checkpoints\screenshots\aladdin-gameplay.bmp"
        Target = "aladdin-gameplay.png"
        Title = "Disney's Aladdin gameplay"
    },
    @{
        Source = "release-gate-smoke\visual-checkpoints\screenshots\toy-story-bedroom.bmp"
        Target = "toy-story-bedroom.png"
        Title = "Disney's Toy Story gameplay"
    },
    @{
        Source = "release-gate-smoke\visual-checkpoints\screenshots\toy-story-travelers-tales.bmp"
        Target = "toy-story-travelers-tales.png"
        Title = "Toy Story Traveler's Tales intro"
    },
    @{
        Source = "release-gate-smoke\visual-checkpoints\screenshots\aladdin-genie-logo.bmp"
        Target = "aladdin-genie-logo.png"
        Title = "Aladdin Genie logo animation"
    },
    @{
        Source = "release-gate-smoke\visual-checkpoints\screenshots\virtua-racing-gameplay.bmp"
        Target = "virtua-racing-gameplay.png"
        Title = "Virtua Racing SVP gameplay"
    }
)

New-Item -ItemType Directory -Path $OutputFolder -Force | Out-Null
$outputFolderPath = (Resolve-Path -LiteralPath $OutputFolder).ProviderPath

Add-Type -AssemblyName System.Drawing

$written = @()
foreach ($item in $items) {
    $source = Join-Path $SourceRoot $item.Source
    $target = Join-Path $outputFolderPath $item.Target
    if (-not (Test-Path -LiteralPath $source)) {
        Write-Warning "Missing showcase source: $source"
        continue
    }

    $sourcePath = (Resolve-Path -LiteralPath $source).ProviderPath
    $image = [System.Drawing.Image]::FromFile($sourcePath)
    try {
        $image.Save($target, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $image.Dispose()
    }

    $written += [pscustomobject]@{
        Title = $item.Title
        Source = $sourcePath
        Output = $target
    }
}

if ($ManifestPath.Length -gt 0) {
    $manifestFolder = Split-Path -Parent $ManifestPath
    if ($manifestFolder.Length -gt 0) {
        New-Item -ItemType Directory -Path $manifestFolder -Force | Out-Null
    }

    $written | Export-Csv -Path $ManifestPath -NoTypeInformation
}

Write-Host "Wrote $($written.Count) showcase image(s) to $outputFolderPath"
if ($ManifestPath.Length -gt 0) {
    Write-Host "Manifest: $ManifestPath"
}

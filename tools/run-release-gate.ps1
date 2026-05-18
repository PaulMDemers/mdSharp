param(
    [string]$RomFolder = "roms",
    [string]$OutputFolder = "render-output\release-gate",
    [int]$CompatibilityFrames = 600,
    [int]$PerfFrames = 600,
    [int]$InstructionsPerFrame = 300000,
    [string]$SonicGreenHillReference = "Sonic the Hedgehog-Green Hill Zone Theme.mp3",
    [switch]$SkipSvp,
    [switch]$SkipCompatibility,
    [switch]$SkipAudio,
    [switch]$SkipMovies,
    [switch]$SkipPerf
)

$ErrorActionPreference = "Stop"

function Invoke-Step {
    param(
        [string]$Name,
        [scriptblock]$Command
    )

    Write-Host ""
    Write-Host "== $Name =="
    $global:LASTEXITCODE = 0
    & $Command
    if ($global:LASTEXITCODE -ne 0) {
        throw "$Name failed with exit code $global:LASTEXITCODE"
    }
}

function Add-ReportPath {
    param(
        [System.Text.StringBuilder]$Builder,
        [string]$Label,
        [string]$Path
    )

    if (Test-Path $Path) {
        $resolved = (Resolve-Path $Path).Path
        [void]$Builder.AppendLine("- ${Label}: $resolved")
    }
    else {
        [void]$Builder.AppendLine("- ${Label}: not generated")
    }
}

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
Push-Location $root
try {
    New-Item -ItemType Directory -Force -Path $OutputFolder | Out-Null

    Invoke-Step "Repository hygiene" {
        & (Join-Path $PSScriptRoot "check-repo-hygiene.ps1")
    }

    Invoke-Step "Build" {
        dotnet build .\mdSharp.sln -c Release --no-restore -v minimal
    }

    Invoke-Step "Tests" {
        dotnet test .\mdSharp.sln -c Release --no-restore -v minimal
    }

    $virtuaRom = Join-Path $RomFolder "Virtua Racing (USA).md"
    if ($SkipSvp) {
        Write-Warning "Skipping Virtua Racing SVP gate by request."
    }
    elseif (Test-Path $virtuaRom) {
        Invoke-Step "Virtua Racing SVP gate" {
            dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release --no-build -- --virtua-racing-layout-check $virtuaRom (Join-Path $OutputFolder "virtua-racing") virtua-racing-drive 7200 $InstructionsPerFrame render-output\svp-research\svp_bsd\svp\imageformat.txt --fail-on-mismatch
        }
    }
    else {
        Write-Warning "Skipping Virtua Racing SVP gate; ROM not found at $virtuaRom"
    }

    Invoke-Step "Visual checkpoints" {
        dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release --no-build -- --visual-checkpoints $RomFolder (Join-Path $OutputFolder "visual-checkpoints") $InstructionsPerFrame
    }

    if (-not $SkipPerf) {
        Invoke-Step "Performance suite" {
            dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release --no-build -- --perf-suite $RomFolder (Join-Path $OutputFolder "perf-suite") $PerfFrames $InstructionsPerFrame --frame-profile
        }
    }

    if (-not $SkipCompatibility) {
        Invoke-Step "Compatibility sweep" {
            dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release --no-build -- --compat $RomFolder (Join-Path $OutputFolder "compat") $CompatibilityFrames $InstructionsPerFrame --screenshots --resume
        }

        $compatCsv = Join-Path $OutputFolder "compat\compatibility.csv"
        if (Test-Path $compatCsv) {
            Invoke-Step "Near-blank classification" {
                powershell -ExecutionPolicy Bypass -File tools\classify-near-blank.ps1 -CompatibilityCsv $compatCsv -RomFolder $RomFolder -OutputFolder (Join-Path $OutputFolder "near-blank") -Frames "3000,6001,9000" -InstructionsPerFrame $InstructionsPerFrame
            }
        }
    }

    if (-not $SkipAudio) {
        if (Test-Path $SonicGreenHillReference) {
            Invoke-Step "Audio regression" {
                dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release --no-build -- --audio-regression $RomFolder (Join-Path $OutputFolder "audio-regression") $SonicGreenHillReference $InstructionsPerFrame
            }
        }
        else {
            Write-Warning "Skipping audio regression; reference not found at $SonicGreenHillReference"
        }
    }

    if (-not $SkipMovies -and (Test-Path "movies")) {
        Invoke-Step "Movie regression" {
            dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release --no-build -- --movie-regress movies $RomFolder (Join-Path $OutputFolder "movie-regress") $InstructionsPerFrame
        }
    }
    elseif (-not $SkipMovies) {
        Write-Warning "Skipping movie regression; no movies folder found."
    }

    $summaryPath = Join-Path $OutputFolder "release-gate-summary.md"
    $summary = [System.Text.StringBuilder]::new()
    [void]$summary.AppendLine("# mdSharp Release Gate")
    [void]$summary.AppendLine()
    [void]$summary.AppendLine("- ROM folder: $RomFolder")
    [void]$summary.AppendLine("- Compatibility frames: $CompatibilityFrames")
    [void]$summary.AppendLine("- Perf frames: $PerfFrames")
    [void]$summary.AppendLine("- Instructions per frame: $InstructionsPerFrame")
    [void]$summary.AppendLine()
    [void]$summary.AppendLine("## Reports")
    Add-ReportPath $summary "Visual checkpoints" (Join-Path $OutputFolder "visual-checkpoints\visual-checkpoints.md")
    Add-ReportPath $summary "Performance suite" (Join-Path $OutputFolder "perf-suite\perf-suite.md")
    Add-ReportPath $summary "Compatibility summary" (Join-Path $OutputFolder "compat\summary.md")
    Add-ReportPath $summary "Compatibility dashboard" (Join-Path $OutputFolder "compat\index.html")
    Add-ReportPath $summary "Near-blank classification" (Join-Path $OutputFolder "near-blank\near-blank-classification.md")
    Add-ReportPath $summary "Audio regression" (Join-Path $OutputFolder "audio-regression\audio-regression.md")
    Add-ReportPath $summary "Movie regression" (Join-Path $OutputFolder "movie-regress\index.html")
    Add-ReportPath $summary "Virtua Racing SVP gate" (Join-Path $OutputFolder "virtua-racing\virtua-racing-layout-report.md")
    Set-Content -Path $summaryPath -Value $summary.ToString() -Encoding UTF8

    Write-Host ""
    Write-Host "Release gate summary: $((Resolve-Path $summaryPath).Path)"
    Write-Host "Release gate complete: $((Resolve-Path $OutputFolder).Path)"
}
finally {
    Pop-Location
}

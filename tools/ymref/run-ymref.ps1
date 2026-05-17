param(
    [string]$Configuration = "Release",
    [string]$OutputRoot = "render-output\ymref-suite",
    [ValidateSet("pins", "internal")]
    [string]$ReferenceOutput = "pins"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$ymrefExe = Join-Path $PSScriptRoot "ymref.exe"
$nukedSource = Join-Path $repoRoot "tools\Nuked-OPN2\ym3438.c"
$ymrefSource = Join-Path $PSScriptRoot "ymref.c"
$scripts = Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot "scripts") -Filter "*.txt" | Sort-Object Name
$outputRootFull = Join-Path $repoRoot $OutputRoot

New-Item -ItemType Directory -Force -Path $outputRootFull | Out-Null
gcc -O2 -std=c99 -I"$repoRoot\tools\Nuked-OPN2" "$ymrefSource" "$nukedSource" -o "$ymrefExe"
dotnet build (Join-Path $repoRoot "mdSharp.sln") -c $Configuration --no-restore

$summary = @()
foreach ($script in $scripts) {
    $id = [IO.Path]::GetFileNameWithoutExtension($script.Name)
    $caseFolder = Join-Path $outputRootFull $id
    New-Item -ItemType Directory -Force -Path $caseFolder | Out-Null
    $nukedWav = Join-Path $caseFolder "$id-nuked.wav"
    $mdsharpWav = Join-Path $caseFolder "$id-mdsharp.wav"
    $compareFolder = Join-Path $caseFolder "compare"

    & $ymrefExe $script.FullName $nukedWav $ReferenceOutput
    dotnet run --project (Join-Path $repoRoot "src\MdSharp.App\MdSharp.App.csproj") -c $Configuration --no-build -- --ym-script-render $script.FullName $mdsharpWav
    dotnet run --project (Join-Path $repoRoot "src\MdSharp.App\MdSharp.App.csproj") -c $Configuration --no-build -- --audio-file-compare $nukedWav $mdsharpWav $compareFolder $id 2

    $report = Join-Path $compareFolder "$id-audio-file-compare.md"
    $corr = "n/a"
    $melody = "n/a"
    $sparkle = "n/a"
    foreach ($line in Get-Content -LiteralPath $report) {
        if ($line -match "Envelope correlation:\s+([-0-9.]+)") {
            $corr = $Matches[1]
        } elseif ($line -match "\| Melody relative dB \|\s+([-0-9.]+)\s+\|") {
            $melody = $Matches[1]
        } elseif ($line -match "\| Sparkle relative dB \|\s+([-0-9.]+)\s+\|") {
            $sparkle = $Matches[1]
        }
    }

    $summary += [pscustomobject]@{
        Case = $id
        Correlation = $corr
        MelodyRelativeDb = $melody
        SparkleRelativeDb = $sparkle
        Report = $report
    }
}

$summaryPath = Join-Path $outputRootFull "ymref-summary.md"
$lines = @("# YM2612 Reference Probe Summary", "", ("Reference output: ``{0}``" -f $ReferenceOutput), "", "| Case | Correlation | Melody relative dB | Sparkle relative dB | Report |", "| --- | ---: | ---: | ---: | --- |")
foreach ($row in $summary) {
    $relativeReport = Resolve-Path -LiteralPath $row.Report -Relative
    $lines += "| $($row.Case) | $($row.Correlation) | $($row.MelodyRelativeDb) | $($row.SparkleRelativeDb) | [$($row.Case)]($relativeReport) |"
}

Set-Content -LiteralPath $summaryPath -Value $lines -Encoding UTF8
Write-Host "Wrote summary to $summaryPath"

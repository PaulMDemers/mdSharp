param(
    [Parameter(Mandatory = $true)]
    [string]$CompatibilityCsv,
    [string]$RomFolder = "roms",
    [string]$OutputFolder = "render-output\near-blank-classification",
    [string]$Frames = "3000",
    [int]$InstructionsPerFrame = 300000,
    [int]$BlankThreshold = 64
)

$ErrorActionPreference = "Stop"

function Escape-CsvField {
    param([string]$Value)
    if ($null -eq $Value) {
        return ""
    }

    if ($Value.Contains('"') -or $Value.Contains(',') -or $Value.Contains("`n") -or $Value.Contains("`r")) {
        return '"' + $Value.Replace('"', '""') + '"'
    }

    return $Value
}

function Resolve-RomPath {
    param([string]$RelativeRom)

    $candidate = Join-Path $RomFolder $RelativeRom
    if (Test-Path $candidate) {
        return $candidate
    }

    $name = [IO.Path]::GetFileName($RelativeRom)
    $matches = Get-ChildItem -Path $RomFolder -Recurse -File | Where-Object { $_.Name -eq $name }
    if ($matches.Count -gt 0) {
        return $matches[0].FullName
    }

    return $null
}

function Get-RelativePathCompat {
    param(
        [string]$BasePath,
        [string]$TargetPath
    )

    $baseUri = New-Object Uri ((Resolve-Path $BasePath).Path.TrimEnd('\') + '\')
    $targetUri = New-Object Uri ((Resolve-Path $TargetPath).Path)
    return [Uri]::UnescapeDataString($baseUri.MakeRelativeUri($targetUri).ToString()).Replace('/', '\')
}

function Parse-RenderOutput {
    param([string[]]$Lines)

    $result = [ordered]@{
        renderMode = ""
        nonBackgroundPixels = ""
        pc = ""
        maxPixels = ""
        sprites = ""
        detail = ""
    }

    foreach ($line in $Lines) {
        if ($line -match 'Rendered frame mode=([^ ]+) nonBackgroundPixels=([0-9,]+)') {
            $result.renderMode = $Matches[1]
            $result.nonBackgroundPixels = $Matches[2].Replace(',', '')
        }
        elseif ($line -match '^PC=(\$[0-9A-F]+)') {
            $result.pc = $Matches[1]
        }
        elseif ($line -match 'best=\$[0-9A-F]+/([0-9,]+)') {
            $result.maxPixels = $Matches[1].Replace(',', '')
        }
        elseif ($line -match 'sprites=([0-9]+)') {
            $result.sprites = $Matches[1]
        }
    }

    return $result
}

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
Push-Location $root
try {
    $csvPath = Resolve-Path $CompatibilityCsv
    $frameList = $Frames -split '[,; ]+' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | ForEach-Object { [int]$_ }
    New-Item -ItemType Directory -Force -Path $OutputFolder | Out-Null
    $screenshotFolder = Join-Path $OutputFolder "screenshots"
    New-Item -ItemType Directory -Force -Path $screenshotFolder | Out-Null

    $rows = Import-Csv $csvPath | Where-Object {
        $_.status -eq "ok" -and ([int]$_.nonBackgroundPixels) -le $BlankThreshold
    }

    $outputCsv = Join-Path $OutputFolder "near-blank-classification.csv"
    $summaryPath = Join-Path $OutputFolder "near-blank-classification.md"
    $csvLines = New-Object System.Collections.Generic.List[string]
    $csvLines.Add("rom,initialPixels,initialMaxPixels,initialRenderMode,checkedFrames,visibleFrame,laterStatus,laterPixels,laterRenderMode,laterPc,laterSprites,bmp,detail")

    $summaryRows = New-Object System.Collections.Generic.List[object]
    $index = 0
    foreach ($row in $rows) {
        $index++
        $rom = Resolve-RomPath $row.rom
        if ($null -eq $rom) {
            $summaryRows.Add([pscustomobject]@{
                Rom = $row.rom
                InitialPixels = [int]$row.nonBackgroundPixels
                InitialMaxPixels = [int]$row.maxNonBackgroundPixels
                LaterStatus = "missing"
                LaterPixels = 0
                LaterMode = ""
                Bmp = ""
                Detail = "ROM not found"
            })
            continue
        }

        $baseName = [IO.Path]::GetFileNameWithoutExtension($row.rom) -replace '[^\w\-.]+', '_'
        $bestParsed = $null
        $bestBmpPath = ""
        $bestDetail = ""
        $visibleFrame = 0
        $laterStatus = "still-blank"
        $laterPixels = 0
        Write-Host ("{0,3}/{1}: {2}" -f $index, $rows.Count, $row.rom)
        foreach ($frame in $frameList) {
            $ppmPath = Join-Path $OutputFolder "$baseName-frame-$frame.ppm"
            $output = & dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release --no-build -- --render $rom $ppmPath $frame $InstructionsPerFrame 2>&1
            $parsed = Parse-RenderOutput $output
            $pixels = if ($parsed.nonBackgroundPixels -ne "") { [int]$parsed.nonBackgroundPixels } else { 0 }
            $bmpPath = [IO.Path]::ChangeExtension($ppmPath, ".bmp")
            if (Test-Path $bmpPath) {
                Move-Item -Force -LiteralPath $bmpPath -Destination (Join-Path $screenshotFolder ([IO.Path]::GetFileName($bmpPath)))
                $bmpPath = Join-Path $screenshotFolder ([IO.Path]::GetFileName($bmpPath))
            }
            else {
                $bmpPath = ""
            }

            $bestParsed = $parsed
            $bestBmpPath = $bmpPath
            $bestDetail = if ($LASTEXITCODE -ne 0) { ($output -join " ").Trim() } else { "" }
            $laterPixels = $pixels
            if ($LASTEXITCODE -ne 0) {
                $laterStatus = "error"
                break
            }

            if ($pixels -gt $BlankThreshold) {
                $laterStatus = "visible"
                $visibleFrame = $frame
                break
            }
        }

        $summaryRows.Add([pscustomobject]@{
            Rom = $row.rom
            InitialPixels = [int]$row.nonBackgroundPixels
            InitialMaxPixels = [int]$row.maxNonBackgroundPixels
            LaterStatus = $laterStatus
            LaterPixels = $laterPixels
            LaterMode = $bestParsed.renderMode
            LaterPc = $bestParsed.pc
            LaterSprites = $bestParsed.sprites
            VisibleFrame = $visibleFrame
            Bmp = $bestBmpPath
            Detail = $bestDetail
        })

        $csvLines.Add((
            @(
                (Escape-CsvField $row.rom),
                $row.nonBackgroundPixels,
                $row.maxNonBackgroundPixels,
                $row.renderMode,
                (Escape-CsvField ($frameList -join ";")),
                $visibleFrame,
                $laterStatus,
                $laterPixels,
                $bestParsed.renderMode,
                $bestParsed.pc,
                $bestParsed.sprites,
                (Escape-CsvField $bestBmpPath),
                (Escape-CsvField $bestDetail)
            ) -join ","))
    }

    Set-Content -Path $outputCsv -Value $csvLines -Encoding UTF8

    $visible = @($summaryRows | Where-Object LaterStatus -eq "visible").Count
    $stillBlank = @($summaryRows | Where-Object LaterStatus -eq "still-blank").Count
    $missing = @($summaryRows | Where-Object LaterStatus -eq "missing").Count
    $errors = @($summaryRows | Where-Object LaterStatus -eq "error").Count

    $markdown = New-Object System.Collections.Generic.List[string]
    $markdown.Add("# Near-Blank Compatibility Follow-Up")
    $markdown.Add("")
    $markdown.Add("Source: ``$csvPath``")
    $markdown.Add("Follow-up frames: ``$($frameList -join ', ')``")
    $markdown.Add("")
    $markdown.Add("- Candidates: ``$($summaryRows.Count)``")
    $markdown.Add("- Visible by follow-up: ``$visible``")
    $markdown.Add("- Still blank: ``$stillBlank``")
    $markdown.Add("- Missing ROMs: ``$missing``")
    $markdown.Add("- Errors: ``$errors``")
    $markdown.Add("")
    $markdown.Add("| ROM | Initial max pixels | Follow-up status | Visible frame | Follow-up pixels | Render mode | Screenshot | Detail |")
    $markdown.Add("| --- | ---: | --- | ---: | ---: | --- | --- | --- |")
    foreach ($item in $summaryRows) {
        $screenshot = if ($item.Bmp) { "[bmp]($(Get-RelativePathCompat $OutputFolder $item.Bmp | ForEach-Object { $_.Replace('\', '/') }))" } else { "" }
        $markdown.Add("| ``$($item.Rom.Replace('|', '\|'))`` | $($item.InitialMaxPixels) | ``$($item.LaterStatus)`` | $($item.VisibleFrame) | $($item.LaterPixels) | ``$($item.LaterMode)`` | $screenshot | $($item.Detail.Replace('|', '\|')) |")
    }

    Set-Content -Path $summaryPath -Value $markdown -Encoding UTF8
    Write-Host "Wrote $summaryPath"
    Write-Host "Wrote $outputCsv"
}
finally {
    Pop-Location
}

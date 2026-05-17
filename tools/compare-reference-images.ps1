param(
    [Parameter(Mandatory = $true)]
    [string]$ReferenceImage,

    [Parameter(Mandatory = $true)]
    [string]$CurrentImage,

    [string]$OutputFolder = "render-output\virtua-layout\reference-compare",

    [switch]$ScaleReferenceToCurrent
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName WindowsBase

function Load-BgraImage([string]$Path)
{
    $resolved = Resolve-Path -LiteralPath $Path
    $stream = [System.IO.File]::OpenRead($resolved.Path)
    try
    {
        $decoder = [System.Windows.Media.Imaging.BitmapDecoder]::Create(
            $stream,
            [System.Windows.Media.Imaging.BitmapCreateOptions]::PreservePixelFormat,
            [System.Windows.Media.Imaging.BitmapCacheOption]::OnLoad)
        $frame = $decoder.Frames[0]
        $converted = [System.Windows.Media.Imaging.FormatConvertedBitmap]::new(
            $frame,
            [System.Windows.Media.PixelFormats]::Bgra32,
            $null,
            0)
        $width = $converted.PixelWidth
        $height = $converted.PixelHeight
        $stride = $width * 4
        $pixels = [byte[]]::new($stride * $height)
        $converted.CopyPixels($pixels, $stride, 0)
        return [pscustomobject]@{
            Path = $resolved.Path
            Width = $width
            Height = $height
            Stride = $stride
            Pixels = $pixels
        }
    }
    finally
    {
        $stream.Dispose()
    }
}

function Resize-NearestBgra($Image, [int]$Width, [int]$Height)
{
    if ($Image.Width -eq $Width -and $Image.Height -eq $Height)
    {
        return $Image
    }

    $pixels = [byte[]]::new($Width * $Height * 4)
    for ($y = 0; $y -lt $Height; $y++)
    {
        $sourceY = [Math]::Min($Image.Height - 1, [int][Math]::Floor(($y * $Image.Height) / [double]$Height))
        for ($x = 0; $x -lt $Width; $x++)
        {
            $sourceX = [Math]::Min($Image.Width - 1, [int][Math]::Floor(($x * $Image.Width) / [double]$Width))
            $source = ($sourceY * $Image.Stride) + ($sourceX * 4)
            $dest = (($y * $Width) + $x) * 4
            $pixels[$dest] = $Image.Pixels[$source]
            $pixels[$dest + 1] = $Image.Pixels[$source + 1]
            $pixels[$dest + 2] = $Image.Pixels[$source + 2]
            $pixels[$dest + 3] = $Image.Pixels[$source + 3]
        }
    }

    return [pscustomobject]@{
        Path = $Image.Path
        Width = $Width
        Height = $Height
        Stride = $Width * 4
        Pixels = $pixels
    }
}

function Save-BgraPng([string]$Path, [int]$Width, [int]$Height, [byte[]]$Pixels)
{
    $bitmap = [System.Windows.Media.Imaging.BitmapSource]::Create(
        $Width,
        $Height,
        96,
        96,
        [System.Windows.Media.PixelFormats]::Bgra32,
        $null,
        $Pixels,
        $Width * 4)

    $encoder = [System.Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = [System.IO.File]::Create($Path)
    try
    {
        $encoder.Save($stream)
    }
    finally
    {
        $stream.Dispose()
    }
}

function Copy-Image($Source, [byte[]]$Dest, [int]$DestWidth, [int]$XOffset)
{
    for ($y = 0; $y -lt $Source.Height; $y++)
    {
        for ($x = 0; $x -lt $Source.Width; $x++)
        {
            $sourceOffset = ($y * $Source.Stride) + ($x * 4)
            $destOffset = (($y * $DestWidth) + $XOffset + $x) * 4
            $Dest[$destOffset] = $Source.Pixels[$sourceOffset]
            $Dest[$destOffset + 1] = $Source.Pixels[$sourceOffset + 1]
            $Dest[$destOffset + 2] = $Source.Pixels[$sourceOffset + 2]
            $Dest[$destOffset + 3] = 255
        }
    }
}

$reference = Load-BgraImage $ReferenceImage
$current = Load-BgraImage $CurrentImage
if (($reference.Width -ne $current.Width -or $reference.Height -ne $current.Height) -and $ScaleReferenceToCurrent)
{
    $reference = Resize-NearestBgra $reference $current.Width $current.Height
}

if ($reference.Width -ne $current.Width -or $reference.Height -ne $current.Height)
{
    throw "Image sizes differ: reference is $($reference.Width)x$($reference.Height), current is $($current.Width)x$($current.Height). Rerun with -ScaleReferenceToCurrent if this is expected."
}

$resolvedOutputFolder = New-Item -ItemType Directory -Force $OutputFolder
$width = $current.Width
$height = $current.Height
$diff = [byte[]]::new($width * $height * 4)
$totalDelta = [int64]0
$maxDelta = 0
$differentPixels = 0

for ($y = 0; $y -lt $height; $y++)
{
    for ($x = 0; $x -lt $width; $x++)
    {
        $offset = ($y * $width + $x) * 4
        $db = [Math]::Abs([int]$reference.Pixels[$offset] - [int]$current.Pixels[$offset])
        $dg = [Math]::Abs([int]$reference.Pixels[$offset + 1] - [int]$current.Pixels[$offset + 1])
        $dr = [Math]::Abs([int]$reference.Pixels[$offset + 2] - [int]$current.Pixels[$offset + 2])
        $delta = $dr + $dg + $db
        if ($delta -ne 0)
        {
            $differentPixels++
        }

        $totalDelta += $delta
        $maxDelta = [Math]::Max($maxDelta, $delta)
        $intensity = [Math]::Min(255, $delta)
        $diff[$offset] = 0
        $diff[$offset + 1] = [byte]$intensity
        $diff[$offset + 2] = [byte]$intensity
        $diff[$offset + 3] = 255
    }
}

$sideBySideWidth = ($width * 3) + 16
$sideBySide = [byte[]]::new($sideBySideWidth * $height * 4)
for ($i = 0; $i -lt $sideBySide.Length; $i += 4)
{
    $sideBySide[$i] = 32
    $sideBySide[$i + 1] = 32
    $sideBySide[$i + 2] = 32
    $sideBySide[$i + 3] = 255
}

Copy-Image $reference $sideBySide $sideBySideWidth 0
Copy-Image $current $sideBySide $sideBySideWidth ($width + 8)
$diffImage = [pscustomobject]@{ Width = $width; Height = $height; Stride = $width * 4; Pixels = $diff }
Copy-Image $diffImage $sideBySide $sideBySideWidth (($width * 2) + 16)

$diffPath = Join-Path $resolvedOutputFolder "diff.png"
$sideBySidePath = Join-Path $resolvedOutputFolder "side-by-side.png"
$reportPath = Join-Path $resolvedOutputFolder "comparison-report.md"
Save-BgraPng $diffPath $width $height $diff
Save-BgraPng $sideBySidePath $sideBySideWidth $height $sideBySide

$pixelCount = $width * $height
$percentDifferent = if ($pixelCount -eq 0) { 0 } else { ($differentPixels * 100.0) / $pixelCount }
$meanDelta = if ($pixelCount -eq 0) { 0 } else { $totalDelta / [double]$pixelCount }

Set-Content -LiteralPath $reportPath -Encoding UTF8 -Value @"
# Reference Image Comparison

Reference: ``$($reference.Path)``
Current: ``$($current.Path)``
Size: ``$width x $height``

- Different pixels: ``$differentPixels / $pixelCount`` (``$($percentDifferent.ToString('0.###'))%``)
- Mean RGB absolute delta: ``$($meanDelta.ToString('0.###'))``
- Max RGB absolute delta: ``$maxDelta``
- Diff: ``$diffPath``
- Side-by-side: ``$sideBySidePath``
"@

Write-Host "Comparison report: $reportPath"
Write-Host "Different pixels: $differentPixels / $pixelCount ($($percentDifferent.ToString('0.###'))%)"
Write-Host "Mean RGB absolute delta: $($meanDelta.ToString('0.###'))"
Write-Host "Max RGB absolute delta: $maxDelta"

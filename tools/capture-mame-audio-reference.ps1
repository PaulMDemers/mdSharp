param(
    [Parameter(Mandatory = $true)]
    [string]$RomPath,

    [string]$MameDir = "render-output\reference-emulators\mame\mame0287",

    [string]$OutputWav,

    [ValidateSet("None", "PressStart")]
    [string]$InputPreset = "None",

    [int]$SecondsToRun = 30,

    [int]$SampleRate = 44100,

    [int]$StartFrame = 360,

    [int]$PressFrames = 24
)

$ErrorActionPreference = "Stop"

$resolvedMameDir = Resolve-Path -LiteralPath $MameDir
$resolvedRomPath = Resolve-Path -LiteralPath $RomPath
$mame = Join-Path $resolvedMameDir "mame.exe"

if (-not (Test-Path -LiteralPath $mame))
{
    throw "MAME executable not found at $mame"
}

if ([string]::IsNullOrWhiteSpace($OutputWav))
{
    $safeName = [IO.Path]::GetFileNameWithoutExtension($resolvedRomPath.Path)
    foreach ($invalid in [IO.Path]::GetInvalidFileNameChars())
    {
        $safeName = $safeName.Replace($invalid, '-')
    }

    $OutputWav = Join-Path "render-output\audio-reference-suite\mame" "$safeName.wav"
}

$outputItem = New-Item -ItemType Directory -Force (Split-Path -Parent $OutputWav)
$resolvedOutputFolder = Resolve-Path -LiteralPath $outputItem.FullName
$outputPath = Join-Path $resolvedOutputFolder.Path ([IO.Path]::GetFileName($OutputWav))
Remove-Item -LiteralPath $outputPath -Force -ErrorAction SilentlyContinue

$args = @(
    "genesis",
    "-cart", $resolvedRomPath.Path,
    "-video", "none",
    "-window",
    "-nothrottle",
    "-seconds_to_run", $SecondsToRun.ToString(),
    "-samplerate", $SampleRate.ToString(),
    "-wavwrite", $outputPath
)

$scriptPath = $null
if ($InputPreset -eq "PressStart")
{
    $scriptPath = Join-Path $resolvedOutputFolder.Path "mame-input-$([IO.Path]::GetFileNameWithoutExtension($outputPath)).lua"
    $lua = @"
local frame = 0
local fields = nil
local start_frame = $StartFrame
local press_frames = $PressFrames

local function contains(value, needle)
    return value and string.find(string.lower(value), string.lower(needle), 1, true) ~= nil
end

local function find_field(predicate)
    for tag, port in pairs(manager.machine.ioport.ports) do
        for name, field in pairs(port.fields) do
            if predicate(tag, name, field) then
                return field
            end
        end
    end

    return nil
end

local function set_field(field, pressed)
    if field then
        if pressed then
            field:set_value(1)
        else
            field:clear_value()
        end
    end
end

local function resolve_fields()
    return {
        start = find_field(function(tag, name, field) return contains(name, 'P1 Start') end),
    }
end

emu.add_machine_frame_notifier(function()
    if fields == nil then
        fields = resolve_fields()
    end

    local start = frame >= start_frame and frame < (start_frame + press_frames)
    set_field(fields.start, start)
    frame = frame + 1
end)
"@
    Set-Content -LiteralPath $scriptPath -Value $lua -Encoding UTF8
    $args += @("-autoboot_script", $scriptPath)
}

Write-Host "Capturing MAME reference WAV:"
Write-Host "  ROM:    $($resolvedRomPath.Path)"
Write-Host "  WAV:    $outputPath"
Write-Host "  Input:  $InputPreset"
Write-Host "  MAME:   $mame"

& $mame @args
if ($LASTEXITCODE -ne 0)
{
    exit $LASTEXITCODE
}

if (-not (Test-Path -LiteralPath $outputPath))
{
    throw "MAME completed but did not create $outputPath"
}

Get-Item -LiteralPath $outputPath | Select-Object FullName, Length, LastWriteTime

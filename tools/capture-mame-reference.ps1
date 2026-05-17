param(
    [Parameter(Mandatory = $true)]
    [string]$RomPath,

    [string]$MameDir = "render-output\reference-emulators\mame\mame0287",

    [string]$OutputFolder = "render-output\virtua-layout\mame-reference",

    [int]$SecondsToRun = 0,

    [int]$SnapshotFrame = 7200,

    [string]$SvpBinPath,

    [switch]$RecordAvi,

    [switch]$UseSoftwareList,

    [switch]$DumpMemory
)

$ErrorActionPreference = "Stop"

$resolvedMameDir = Resolve-Path -LiteralPath $MameDir
$resolvedRomPath = Resolve-Path -LiteralPath $RomPath
$mame = Join-Path $resolvedMameDir "mame.exe"
$resolvedOutputFolder = New-Item -ItemType Directory -Force $OutputFolder
$manifestPath = Join-Path $resolvedOutputFolder "mame-reference-manifest.txt"

$svpFolder = Join-Path $resolvedMameDir "roms\md_rom_svp"
$svpBin = Join-Path $svpFolder "svp.bin"
$svpZip = Join-Path $resolvedMameDir "roms\md_rom_svp.zip"

if (-not [string]::IsNullOrWhiteSpace($SvpBinPath))
{
    $resolvedSvpBinPath = Resolve-Path -LiteralPath $SvpBinPath
    $providedItem = Get-Item -LiteralPath $resolvedSvpBinPath.Path
    $providedSha1 = (Get-FileHash -LiteralPath $resolvedSvpBinPath.Path -Algorithm SHA1).Hash.ToLowerInvariant()
    if ($providedItem.Length -ne 2048 -or $providedSha1 -ne "0b951ea9c6094b3c34e4f0b64d031c75c237564f")
    {
        throw "The supplied SVP ROM is not the expected file. Expected size 2048 and SHA1 0b951ea9c6094b3c34e4f0b64d031c75c237564f, got size $($providedItem.Length) and SHA1 $providedSha1."
    }

    New-Item -ItemType Directory -Force $svpFolder | Out-Null
    Copy-Item -LiteralPath $resolvedSvpBinPath.Path -Destination $svpBin -Force
    Write-Host "Staged verified svp.bin at $svpBin"
}

if (-not (Test-Path -LiteralPath $svpBin) -and -not (Test-Path -LiteralPath $svpZip))
{
    $message = @"
MAME can run Virtua Racing through the md_rom_svp device, but it requires the
SVP internal ROM:

  file: svp.bin
  size: 2048 bytes
  CRC:  2421ec7e
  SHA1: 0b951ea9c6094b3c34e4f0b64d031c75c237564f

Place it at either:

  $svpBin

or zip it as:

  $svpZip

Then rerun:

  powershell -ExecutionPolicy Bypass -File tools\capture-mame-reference.ps1 -RomPath "$($resolvedRomPath.Path)" -SnapshotFrame $SnapshotFrame

Or stage it from another location in one step:

  powershell -ExecutionPolicy Bypass -File tools\capture-mame-reference.ps1 -RomPath "$($resolvedRomPath.Path)" -SnapshotFrame $SnapshotFrame -SvpBinPath "C:\path\to\svp.bin"
"@

    $reportPath = Join-Path $resolvedOutputFolder "missing-svp-bin.txt"
    Set-Content -LiteralPath $reportPath -Value $message -Encoding UTF8
    Write-Host $message
    exit 2
}

if (Test-Path -LiteralPath $svpBin)
{
    $svpItem = Get-Item -LiteralPath $svpBin
    $svpSha1 = (Get-FileHash -LiteralPath $svpBin -Algorithm SHA1).Hash.ToLowerInvariant()
    if ($svpItem.Length -ne 2048 -or $svpSha1 -ne "0b951ea9c6094b3c34e4f0b64d031c75c237564f")
    {
        throw "Unexpected svp.bin. Expected size 2048 and SHA1 0b951ea9c6094b3c34e4f0b64d031c75c237564f, got size $($svpItem.Length) and SHA1 $svpSha1."
    }
}

$cartArgument = $resolvedRomPath.Path
if ($UseSoftwareList)
{
    $softwareZip = Join-Path $resolvedMameDir "roms\vru.zip"
    if (-not (Test-Path -LiteralPath $softwareZip))
    {
        Compress-Archive -LiteralPath $resolvedRomPath.Path -DestinationPath $softwareZip -Force
    }

    $cartArgument = "megadriv:vru"
}

$aviPath = Join-Path $resolvedOutputFolder "virtua-racing-mame-reference.avi"
$snapshotPath = Join-Path $resolvedOutputFolder "virtua-racing-mame-frame.png"
$snapshotDir = Join-Path $resolvedOutputFolder "snapshots"
$inputScriptPath = Join-Path $resolvedOutputFolder "virtua-racing-input.lua"
$inputLogPath = Join-Path $resolvedOutputFolder "virtua-racing-input-fields.log"
$memoryDumpPrefix = Join-Path $resolvedOutputFolder "virtua-racing-mame"
New-Item -ItemType Directory -Force $snapshotDir | Out-Null
Remove-Item -LiteralPath $aviPath -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $snapshotPath -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $inputLogPath -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath "$memoryDumpPrefix-svp-dram.bin" -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath "$memoryDumpPrefix-vdp-vram.bin" -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath "$memoryDumpPrefix-vdp-vsram.bin" -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath "$memoryDumpPrefix-vdp-regs.bin" -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath "$memoryDumpPrefix-memory-inventory.txt" -Force -ErrorAction SilentlyContinue
$defaultMameSnapshotFolder = Join-Path $resolvedMameDir "snap\genesis"
if (Test-Path -LiteralPath $defaultMameSnapshotFolder)
{
    Get-ChildItem -LiteralPath $defaultMameSnapshotFolder -Filter "*.png" -ErrorAction SilentlyContinue |
        Remove-Item -Force -ErrorAction SilentlyContinue
}

if ($SecondsToRun -le 0)
{
    $SecondsToRun = [Math]::Ceiling(($SnapshotFrame + 180) / 60.0)
}

$luaInputLogPath = ($inputLogPath -replace "\\", "/") -replace "'", "\\'"
$luaSnapshotPath = ($snapshotPath -replace "\\", "/") -replace "'", "\\'"
$luaMemoryDumpPrefix = ($memoryDumpPrefix -replace "\\", "/") -replace "'", "\\'"
$luaDumpMemory = if ($DumpMemory) { "true" } else { "false" }
$luaScript = @"
local frame = 0
local fields = nil
local log_path = '$luaInputLogPath'
local snapshot_path = '$luaSnapshotPath'
local memory_dump_prefix = '$luaMemoryDumpPrefix'
local dump_memory_enabled = $luaDumpMemory
local snapshot_frame = $SnapshotFrame
local snapshot_taken = false
local memory_dump_taken = false
local progress_log = nil
_G.mdsharp_reference_frame_subscription = nil
_G.mdsharp_reference_stop_subscription = nil

local function log_line(value)
    if progress_log == nil then
        progress_log = io.open(log_path .. '.progress', 'w')
    end
    if progress_log then
        progress_log:write(value .. '\n')
        progress_log:flush()
    end
end

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

local function resolve_fields()
    local log = io.open(log_path, 'w')
    if log then
        log:write('tag,name,mask,player,type\n')
        for tag, port in pairs(manager.machine.ioport.ports) do
            for name, field in pairs(port.fields) do
                log:write(string.format('%s,%s,%s,%s,%s\n', tostring(tag), tostring(name), tostring(field.mask), tostring(field.player), tostring(field.type)))
            end
        end
    end

    local result = {
        start = find_field(function(tag, name, field) return contains(name, 'P1 Start') end),
        right = find_field(function(tag, name, field) return contains(name, 'P1 Right') end),
        b = find_field(function(tag, name, field) return contains(name, 'P1 B') end),
        c = find_field(function(tag, name, field) return contains(name, 'P1 C') end),
    }

    if log then
        log:write('\nresolved\n')
        for key, field in pairs(result) do
            log:write(string.format('%s=%s\n', key, field and tostring(field.name) or 'missing'))
        end
        log:close()
    end

    return result
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

local function write_u16_be(file, value)
    local high = math.floor(value / 256) % 256
    local low = value % 256
    file:write(string.char(high, low))
end

local function dump_memory_inventory(path)
    local log = io.open(path, 'w')
    if not log then
        return
    end

    log:write('devices\n')
    for tag, device in pairs(manager.machine.devices) do
        log:write(string.format('%s,%s,%s\n', tostring(tag), tostring(device.name), tostring(device.shortname)))
        for name, _ in pairs(device.spaces) do
            log:write(string.format('  space,%s\n', tostring(name)))
        end
        if device.items then
            for name, index in pairs(device.items) do
                log:write(string.format('  item,%s,%s\n', tostring(name), tostring(index)))
            end
        end
    end

    log:write('\nmemory shares\n')
    for tag, share in pairs(manager.machine.memory.shares) do
        log:write(string.format('%s,size=%s,length=%s,bitwidth=%s,endianness=%s\n', tostring(tag), tostring(share.size), tostring(share.length), tostring(share.bitwidth), tostring(share.endianness)))
    end

    log:write('\nmemory regions\n')
    for tag, region in pairs(manager.machine.memory.regions) do
        log:write(string.format('%s,size=%s,length=%s,bitwidth=%s,endianness=%s\n', tostring(tag), tostring(region.size), tostring(region.length), tostring(region.bitwidth), tostring(region.endianness)))
    end

    log:close()
end

local function dump_svp_dram(path)
    local cpu = manager.machine.devices[':maincpu']
    if not cpu then
        log_line('memory dump failed: :maincpu missing')
        return
    end

    local space = cpu.spaces['program']
    if not space then
        log_line('memory dump failed: :maincpu program space missing')
        return
    end

    local file = io.open(path, 'wb')
    if not file then
        log_line('memory dump failed: could not open ' .. path)
        return
    end

    for offset = 0, 0x1fffe, 2 do
        local value = space:read_u16(0x300000 + offset)
        write_u16_be(file, value)
    end

    file:close()
    log_line('memory dump saved: ' .. path)
end

local function write_save_item(path, item_key, expected_count)
    local vdp = manager.machine.devices[':gen_vdp']
    if not vdp or not vdp.items or not vdp.items[item_key] then
        log_line('save item dump skipped: missing ' .. item_key)
        return
    end

    local item = emu.item(vdp.items[item_key])
    if not item then
        log_line('save item dump failed: emu.item missing for ' .. item_key)
        return
    end

    local file = io.open(path, 'wb')
    if not file then
        log_line('save item dump failed: could not open ' .. path)
        return
    end

    local count = expected_count or item.count
    for i = 0, count - 1 do
        local value = item:read(i)
        if item.size == 1 then
            file:write(string.char(value % 256))
        elseif item.size == 2 then
            write_u16_be(file, value)
        else
            file:write(string.char(
                math.floor(value / 0x1000000) % 256,
                math.floor(value / 0x10000) % 256,
                math.floor(value / 0x100) % 256,
                value % 256))
        end
    end

    file:close()
    log_line('save item dump saved: ' .. path .. ' key=' .. item_key .. ' size=' .. tostring(item.size) .. ' count=' .. tostring(item.count))
end

local function dump_reference_memory()
    if memory_dump_taken or not dump_memory_enabled then
        return
    end

    dump_memory_inventory(memory_dump_prefix .. '-memory-inventory.txt')
    dump_svp_dram(memory_dump_prefix .. '-svp-dram.bin')
    write_save_item(memory_dump_prefix .. '-vdp-vram.bin', '0/m_vram', nil)
    write_save_item(memory_dump_prefix .. '-vdp-vsram.bin', '0/m_vsram', nil)
    write_save_item(memory_dump_prefix .. '-vdp-regs.bin', '0/m_regs', nil)
    memory_dump_taken = true
end

_G.mdsharp_reference_frame_subscription = emu.add_machine_frame_notifier(function()
    if fields == nil then
        fields = resolve_fields()
    end

    local start = (frame >= 300 and frame < 330) or (frame >= 900 and frame < 930) or (frame >= 1500 and frame < 1530)
    local c = frame >= 2400 and frame < 4200
    local b = frame >= 4200
    local right = frame >= 3200 and (frame % 240) < 120

    set_field(fields.start, start)
    set_field(fields.c, c)
    set_field(fields.b, b)
    set_field(fields.right, right)

    if (not snapshot_taken) and frame >= snapshot_frame then
        log_line('taking snapshot at lua frame ' .. tostring(frame))
        local screen = manager.machine.screens:at(1)
        if screen then
            local err = screen:snapshot(snapshot_path)
            if err then
                emu.print_error('snapshot failed: ' .. tostring(err))
                log_line('snapshot failed: ' .. tostring(err))
            else
                emu.print_info('snapshot saved: ' .. snapshot_path)
                log_line('snapshot saved: ' .. snapshot_path)
            end
        else
            manager.machine.video:snapshot()
            emu.print_info('snapshot saved via video manager')
            log_line('snapshot saved via video manager')
        end
        dump_reference_memory()
        snapshot_taken = true
    end

    if frame % 600 == 0 then
        log_line('lua frame ' .. tostring(frame))
    end

    frame = frame + 1
end)

_G.mdsharp_reference_stop_subscription = emu.add_machine_stop_notifier(function()
    log_line('stop at lua frame ' .. tostring(frame) .. ', snapshot_taken=' .. tostring(snapshot_taken))
    if progress_log then
        progress_log:close()
    end
end)
"@

Set-Content -LiteralPath $inputScriptPath -Value $luaScript -Encoding ASCII

$romSha1 = (Get-FileHash -LiteralPath $resolvedRomPath.Path -Algorithm SHA1).Hash.ToLowerInvariant()
Set-Content -LiteralPath $manifestPath -Encoding UTF8 -Value @"
MAME Virtua Racing reference capture
ROM: $($resolvedRomPath.Path)
ROM SHA1: $romSha1
MAME: $mame
MAME cart argument: $cartArgument
Use software list: $UseSoftwareList
Seconds: $SecondsToRun
Snapshot frame: $SnapshotFrame
Input script: $inputScriptPath
Input field log: $inputLogPath
Snapshot: $snapshotPath
AVI: $aviPath
Snapshots: $snapshotDir

Compare it with:
powershell -ExecutionPolicy Bypass -File tools\compare-reference-images.ps1 -ReferenceImage "$snapshotPath" -CurrentImage "render-output\virtua-layout\virtua-racing-layout-frame.bmp" -OutputFolder "render-output\virtua-layout\reference-compare" -ScaleReferenceToCurrent
"@

$mameArgs = @(
    "genesis",
    "-cart1", $cartArgument,
    "-rompath", (Join-Path $resolvedMameDir "roms"),
    "-seconds_to_run", $SecondsToRun,
    "-nothrottle",
    "-video", "gdi",
    "-sound", "none",
    "-skip_gameinfo",
    "-snapshot_directory", $snapshotDir,
    "-snapname", "virtua-racing-mame-%i",
    "-snapsize", "auto",
    "-snapview", "native",
    "-nosnapbilinear",
    "-autoboot_delay", "0",
    "-autoboot_script", $inputScriptPath
)

if ($RecordAvi)
{
    $mameArgs += @("-aviwrite", $aviPath)
}

Push-Location -LiteralPath $resolvedMameDir
try
{
    & $mame @mameArgs
}
finally
{
    Pop-Location
}

if (-not (Test-Path -LiteralPath $snapshotPath))
{
    $fallbackSnapshot = Get-ChildItem -LiteralPath $defaultMameSnapshotFolder -Filter "*.png" -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if ($fallbackSnapshot)
    {
        Copy-Item -LiteralPath $fallbackSnapshot.FullName -Destination $snapshotPath -Force
        Write-Host "Copied MAME fallback snapshot $($fallbackSnapshot.FullName) to $snapshotPath"
    }
}

if (Test-Path -LiteralPath $snapshotPath)
{
    Write-Host "MAME reference snapshot: $snapshotPath"
}
else
{
    Write-Warning "MAME finished without writing $snapshotPath"
}

if ($RecordAvi)
{
    Write-Host "MAME reference AVI: $aviPath"
}
Write-Host "MAME manifest: $manifestPath"

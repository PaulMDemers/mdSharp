param(
    [Parameter(Mandatory = $true)]
    [string]$RomPath,

    [string]$BlastEmDir = "render-output\reference-emulators\blastem\blastem-win64-0.6.3-pre-2df04125ac78",

    [int]$WarmupSeconds = 3,

    [int]$RunSeconds = 30
)

$ErrorActionPreference = "Stop"

Add-Type -TypeDefinition @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

public static class TargetedWindowInput
{
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    public static IntPtr[] GetProcessWindows(int processId)
    {
        var result = new List<IntPtr>();
        EnumWindows((hWnd, lParam) =>
        {
            uint owner;
            GetWindowThreadProcessId(hWnd, out owner);
            if (owner == processId && IsWindowVisible(hWnd))
            {
                result.Add(hWnd);
            }

            return true;
        }, IntPtr.Zero);

        return result.ToArray();
    }

    public static string GetTitle(IntPtr hWnd)
    {
        var builder = new StringBuilder(512);
        GetWindowText(hWnd, builder, builder.Capacity);
        return builder.ToString();
    }
}
"@

function ConvertTo-CommandLineArgument([string]$Value)
{
    '"' + ($Value -replace '"', '\"') + '"'
}

function Get-BlastEmWindow([System.Diagnostics.Process]$Process)
{
    for ($attempt = 0; $attempt -lt 40; $attempt++)
    {
        $Process.Refresh()
        if ($Process.HasExited)
        {
            return [IntPtr]::Zero
        }

        $windows = [TargetedWindowInput]::GetProcessWindows($Process.Id)
        foreach ($window in $windows)
        {
            $title = [TargetedWindowInput]::GetTitle($window)
            if ($title -and $title -ne "Fatal Error")
            {
                return $window
            }
        }

        if ($windows.Length -gt 0)
        {
            return $windows[0]
        }

        Start-Sleep -Milliseconds 250
    }

    return [IntPtr]::Zero
}

function Send-TargetedKey([IntPtr]$Window, [int]$VirtualKey, [int]$Repeat = 1, [int]$DelayMs = 100)
{
    $wmKeyDown = 0x0100
    $wmKeyUp = 0x0101
    for ($i = 0; $i -lt $Repeat; $i++)
    {
        [void][TargetedWindowInput]::PostMessage($Window, $wmKeyDown, [IntPtr]$VirtualKey, [IntPtr]1)
        Start-Sleep -Milliseconds 35
        [void][TargetedWindowInput]::PostMessage($Window, $wmKeyUp, [IntPtr]$VirtualKey, [IntPtr]0)
        Start-Sleep -Milliseconds $DelayMs
    }
}

function Close-TargetedWindow([IntPtr]$Window)
{
    $wmClose = 0x0010
    [void][TargetedWindowInput]::PostMessage($Window, $wmClose, [IntPtr]::Zero, [IntPtr]::Zero)
}

$resolvedBlastEmDir = Resolve-Path -LiteralPath $BlastEmDir
$resolvedRomPath = Resolve-Path -LiteralPath $RomPath
$exe = Join-Path $resolvedBlastEmDir "blastem.exe"
$screenshots = Join-Path $resolvedBlastEmDir "screenshots"
New-Item -ItemType Directory -Force $screenshots | Out-Null

# Start-Process -ArgumentList flattens arrays on Windows PowerShell, which breaks
# ROM paths containing spaces. Quote explicitly so BlastEm receives one ROM path.
$psi = [System.Diagnostics.ProcessStartInfo]::new()
$psi.FileName = $exe
$psi.WorkingDirectory = $resolvedBlastEmDir.Path
$psi.UseShellExecute = $false
$psi.Arguments = "-m gen $(ConvertTo-CommandLineArgument $resolvedRomPath.Path) 640 480"
$psi.EnvironmentVariables["LOCALAPPDATA"] = (Resolve-Path -LiteralPath "render-output\reference-emulators").Path
$psi.EnvironmentVariables["HOME"] = $resolvedBlastEmDir.Path

$process = [System.Diagnostics.Process]::Start($psi)
Start-Sleep -Seconds $WarmupSeconds
$process.Refresh()
Write-Host "BlastEm PID=$($process.Id) exited=$($process.HasExited) title='$($process.MainWindowTitle)'"
if ($process.HasExited)
{
    exit $process.ExitCode
}

$window = Get-BlastEmWindow $process
if ($window -eq [IntPtr]::Zero)
{
    throw "Unable to find a BlastEm window for PID $($process.Id)."
}

Write-Host "Targeting BlastEm window handle=$window title='$([TargetedWindowInput]::GetTitle($window))'"
Start-Sleep -Milliseconds 700
Send-TargetedKey $window 0x34
Start-Sleep -Milliseconds 1800
Send-TargetedKey $window 0x0D
Start-Sleep -Milliseconds 1800
Send-TargetedKey $window 0x0D
Start-Sleep -Seconds 8
Send-TargetedKey $window 0x44 -Repeat 30 -DelayMs 100

Start-Sleep -Seconds $RunSeconds
Send-TargetedKey $window 0x50
Start-Sleep -Seconds 2
Close-TargetedWindow $window

try
{
    Wait-Process -Id $process.Id -Timeout 8 -ErrorAction Stop
}
catch
{
    $process.Refresh()
    if (-not $process.HasExited)
    {
        Stop-Process -Id $process.Id -Force
    }
}

Get-ChildItem -LiteralPath $screenshots -Filter "virtua_racing_*.png" |
    Sort-Object LastWriteTime |
    Select-Object FullName, Length, LastWriteTime

param(
    [string]$Version = "dev",
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SkipTests,
    [switch]$SkipSelfContained
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).ProviderPath
$artifactsRoot = Join-Path $repoRoot "artifacts"
$packageRoot = Join-Path $artifactsRoot "packages"
$desktopProject = Join-Path $repoRoot "src\MdSharp.Desktop\MdSharp.Desktop.csproj"
$nugetSource = "https://api.nuget.org/v3/index.json"
$buildVersion = if ($Version -match '^\d+\.\d+\.\d+([-.][0-9A-Za-z.-]+)?$') { $Version } else { "0.0.0-$Version" }
$assemblyVersion = ($buildVersion -replace '[-+].*$', '')
$versionProperties = @(
    "/p:Version=$buildVersion",
    "/p:AssemblyVersion=${assemblyVersion}.0",
    "/p:FileVersion=${assemblyVersion}.0"
)

function Invoke-Checked([string]$file, [string[]]$arguments) {
    & $file @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code ${LASTEXITCODE}: $file $($arguments -join ' ')"
    }
}

function Remove-IfExists([string]$path) {
    if (Test-Path -LiteralPath $path) {
        Remove-Item -LiteralPath $path -Recurse -Force
    }
}

function New-Zip([string]$sourceFolder, [string]$zipPath) {
    if (Test-Path -LiteralPath $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }

    Compress-Archive -Path (Join-Path $sourceFolder "*") -DestinationPath $zipPath -CompressionLevel Optimal
}

function Copy-ReleaseDocs([string]$targetFolder) {
    Copy-Item -LiteralPath (Join-Path $repoRoot "README.md") -Destination $targetFolder
    Copy-Item -LiteralPath (Join-Path $repoRoot "LICENSE") -Destination $targetFolder

    $notice = @"
mdSharp release package

This package contains mdSharp binaries and documentation only.
It does not include game ROMs, BIOS files, coprocessor blobs, save files,
save states, reference audio, or generated regression output.

Use only ROM images and reference material that are legally obtained.
"@
    Set-Content -Path (Join-Path $targetFolder "NOTICE.txt") -Value $notice -Encoding UTF8
}

Push-Location $repoRoot
try {
    New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null

    Invoke-Checked "dotnet" @("clean", "mdSharp.sln", "-c", $Configuration)
    $buildArgs = @("build", "mdSharp.sln", "-c", $Configuration) + $versionProperties
    Invoke-Checked "dotnet" $buildArgs
    if (-not $SkipTests) {
        Invoke-Checked "dotnet" @("test", "mdSharp.sln", "-c", $Configuration, "--no-build")
    }

    $frameworkFolder = Join-Path $artifactsRoot "mdSharp-desktop-$Version-framework-dependent"
    Remove-IfExists $frameworkFolder
    $frameworkPublishArgs = @("publish", $desktopProject, "-c", $Configuration, "-o", $frameworkFolder) + $versionProperties
    Invoke-Checked "dotnet" $frameworkPublishArgs
    Copy-ReleaseDocs $frameworkFolder
    New-Zip $frameworkFolder (Join-Path $packageRoot "mdSharp-desktop-$Version-framework-dependent.zip")

    if (-not $SkipSelfContained) {
        $selfContainedFolder = Join-Path $artifactsRoot "mdSharp-desktop-$Version-$Runtime"
        Remove-IfExists $selfContainedFolder
        $selfContainedPublishArgs = @(
            "publish", $desktopProject,
            "-c", $Configuration,
            "-r", $Runtime,
            "--self-contained", "true",
            "-o", $selfContainedFolder,
            "--source", $nugetSource
        ) + $versionProperties
        Invoke-Checked "dotnet" $selfContainedPublishArgs
        Copy-ReleaseDocs $selfContainedFolder
        New-Zip $selfContainedFolder (Join-Path $packageRoot "mdSharp-desktop-$Version-$Runtime.zip")
    }

    Write-Host "Release packages written to $packageRoot"
}
finally {
    Pop-Location
}

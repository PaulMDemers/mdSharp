param(
    [string]$Version = "dev",
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SkipTests,
    [switch]$SkipSelfContained,
    [switch]$Portable
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
$buildDateUtc = [DateTimeOffset]::UtcNow.ToString("o")

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

function Get-GitCommit() {
    $commit = (& git -C $repoRoot rev-parse --short HEAD 2>$null)
    if ($LASTEXITCODE -ne 0) {
        return $null
    }

    return ($commit | Select-Object -First 1)
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

function Get-RelativePath([string]$basePath, [string]$targetPath) {
    $baseFullPath = (Resolve-Path -LiteralPath $basePath).ProviderPath
    $targetFullPath = (Resolve-Path -LiteralPath $targetPath).ProviderPath
    if (-not $baseFullPath.EndsWith([System.IO.Path]::DirectorySeparatorChar)) {
        $baseFullPath += [System.IO.Path]::DirectorySeparatorChar
    }

    $baseUri = [System.Uri]::new($baseFullPath)
    $targetUri = [System.Uri]::new($targetFullPath)
    return [System.Uri]::UnescapeDataString($baseUri.MakeRelativeUri($targetUri).ToString()).Replace('/', '\')
}

function Enable-PortableMode([string]$targetFolder) {
    if (-not $Portable) {
        return
    }

    $portableFolder = Join-Path $targetFolder "portable"
    New-Item -ItemType Directory -Path $portableFolder -Force | Out-Null
    $marker = [pscustomobject]@{
        portable = $true
        settingsFolder = "portable"
        savesFolder = "portable\saves"
        statesFolder = "portable\states"
    }
    $marker | ConvertTo-Json -Depth 4 | Set-Content -Path (Join-Path $targetFolder "mdsharp-portable.json") -Encoding UTF8
}

function New-ReleaseManifest([string]$targetFolder, [string]$packageName, [string]$runtimeMode) {
    $manifestPath = Join-Path $targetFolder "manifest.json"
    if (Test-Path -LiteralPath $manifestPath) {
        Remove-Item -LiteralPath $manifestPath -Force
    }

    $files = Get-ChildItem -LiteralPath $targetFolder -File -Recurse |
        Sort-Object FullName |
        ForEach-Object {
            $relative = (Get-RelativePath $targetFolder $_.FullName).Replace('\', '/')
            $hash = Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256
            [pscustomobject]@{
                path = $relative
                bytes = $_.Length
                sha256 = $hash.Hash.ToLowerInvariant()
            }
        }

    $manifest = [pscustomobject]@{
        name = $packageName
        version = $Version
        buildVersion = $buildVersion
        commit = Get-GitCommit
        buildDateUtc = $buildDateUtc
        runtimeMode = $runtimeMode
        runtime = if ($runtimeMode -eq "self-contained") { $Runtime } else { $null }
        portable = [bool]$Portable
        files = $files
    }
    $manifest | ConvertTo-Json -Depth 8 | Set-Content -Path $manifestPath -Encoding UTF8
}

function Test-PackageFolder([string]$targetFolder) {
    $required = @("MdSharp.Desktop.exe", "README.md", "LICENSE", "NOTICE.txt", "manifest.json")
    foreach ($name in $required) {
        $path = Join-Path $targetFolder $name
        if (-not (Test-Path -LiteralPath $path)) {
            throw "Package is missing required file: $name"
        }
    }

    $forbiddenPatterns = @(
        "*.md",
        "*.bin",
        "*.gen",
        "*.smd",
        "*.rom",
        "*.srm",
        "*.mdss",
        "*.wav",
        "*.mp3",
        "*.flac",
        "*.ogg"
    )

    foreach ($pattern in $forbiddenPatterns) {
        $matches = Get-ChildItem -LiteralPath $targetFolder -Recurse -File -Filter $pattern |
            Where-Object { $_.Name -notin @("README.md") }
        if ($matches) {
            throw "Package contains forbidden file type '$pattern': $($matches[0].FullName)"
        }
    }

    $forbiddenFolders = @("roms", "TestRoms", "render-output", "movies", "cfg", "nvram", "snap")
    foreach ($folder in $forbiddenFolders) {
        $matches = Get-ChildItem -LiteralPath $targetFolder -Directory -Recurse |
            Where-Object { $_.Name.Equals($folder, [StringComparison]::OrdinalIgnoreCase) }
        if ($matches) {
            throw "Package contains forbidden folder '$folder': $($matches[0].FullName)"
        }
    }
}

function Test-ZipPackage([string]$zipPath) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        $entries = $zip.Entries | ForEach-Object { $_.FullName.Replace('\', '/') }
        foreach ($name in @("MdSharp.Desktop.exe", "README.md", "LICENSE", "NOTICE.txt", "manifest.json")) {
            if (-not ($entries -contains $name)) {
                throw "Zip package is missing required file: $name"
            }
        }

        foreach ($entry in $entries) {
            if ($entry -match '(^|/)(roms|TestRoms|render-output|movies|cfg|nvram|snap)(/|$)') {
                throw "Zip package contains forbidden folder entry: $entry"
            }
            if ($entry -match '\.(bin|gen|smd|rom|srm|mdss|wav|mp3|flac|ogg)$') {
                throw "Zip package contains forbidden file entry: $entry"
            }
        }
    }
    finally {
        $zip.Dispose()
    }
}

function Complete-Package([string]$folder, [string]$zipPath, [string]$packageName, [string]$runtimeMode) {
    Copy-ReleaseDocs $folder
    Enable-PortableMode $folder
    New-ReleaseManifest $folder $packageName $runtimeMode
    Test-PackageFolder $folder
    New-Zip $folder $zipPath
    Test-ZipPackage $zipPath
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
    Complete-Package $frameworkFolder (Join-Path $packageRoot "mdSharp-desktop-$Version-framework-dependent.zip") "mdSharp-desktop-$Version-framework-dependent" "framework-dependent"

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
        Complete-Package $selfContainedFolder (Join-Path $packageRoot "mdSharp-desktop-$Version-$Runtime.zip") "mdSharp-desktop-$Version-$Runtime" "self-contained"
    }

    Write-Host "Release packages written to $packageRoot"
}
finally {
    Pop-Location
}

<#
.SYNOPSIS
    Builds the downloadable versions of AutoPlay into the downloads folder.

.DESCRIPTION
    Publishes two Windows x64 builds of AutoPlay and zips them into the downloads folder:
    - AutoPlay-<version>-win-x64.zip: needs the .NET 10 Desktop Runtime on the PC;
    - AutoPlay-<version>-win-x64-self-contained.zip: a single AutoPlay.exe that runs without installing anything.

    The version comes from Directory.Build.props. The previous versions are removed from the folder (they stay in
    the Git history), and SHA256SUMS.txt lists the checksums of the zip files.

    Runs on Windows, Linux or macOS with the .NET 10 SDK and PowerShell 7.

.EXAMPLE
    pwsh scripts/publish.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src/AutoPlay.App/AutoPlay.App.csproj'
$downloads = Join-Path $root 'downloads'
$work = Join-Path ([IO.Path]::GetTempPath()) "autoplay-publish-$([guid]::NewGuid())"

$version = (& dotnet msbuild $project -getProperty:Version).Trim()
if ($LASTEXITCODE -ne 0 -or -not $version) {
    throw 'The version could not be read from Directory.Build.props.'
}

function Publish-Build([string] $name, [string[]] $options) {
    $output = Join-Path $work $name
    Write-Host "Publishing $name..."
    & dotnet publish $project --configuration Release --runtime win-x64 -p:DebugType=none --output $output @options
    if ($LASTEXITCODE -ne 0) {
        throw "The build $name failed."
    }

    Compress-Archive -Path (Join-Path $output '*') -DestinationPath (Join-Path $downloads "$name.zip") -CompressionLevel Optimal
}

try {
    New-Item -ItemType Directory -Force -Path $downloads | Out-Null
    Get-ChildItem -Path $downloads -Filter 'AutoPlay-*.zip' | Remove-Item

    # Small build: the .NET runtime is installed once on the PC and shared by the .NET applications.
    Publish-Build "AutoPlay-$version-win-x64" @('--self-contained', 'false')

    # Standalone build: the .NET runtime is included and everything is packed into one compressed AutoPlay.exe.
    # The native OpenCV library is extracted next to it on first launch (in %TEMP%\.net).
    Publish-Build "AutoPlay-$version-win-x64-self-contained" @(
        '--self-contained', 'true',
        '-p:PublishSingleFile=true',
        '-p:EnableCompressionInSingleFile=true',
        '-p:IncludeNativeLibrariesForSelfExtract=true')

    $checksums = Get-ChildItem -Path $downloads -Filter 'AutoPlay-*.zip' | Sort-Object Name | ForEach-Object {
        "$((Get-FileHash -Algorithm SHA256 -Path $_.FullName).Hash.ToLowerInvariant())  $($_.Name)"
    }
    Set-Content -Path (Join-Path $downloads 'SHA256SUMS.txt') -Value $checksums

    Get-ChildItem -Path $downloads -Filter 'AutoPlay-*.zip' | ForEach-Object {
        Write-Host ('{0}: {1:N1} MB' -f $_.Name, ($_.Length / 1MB))
    }
}
finally {
    if (Test-Path $work) {
        Remove-Item -Recurse -Force $work
    }
}

# Auto Forestry Designations
# Copyright (c) 2026 Kayser
# Licensed under the MIT License.
#
# Unofficial mod for Captain of Industry. Captain of Industry, MaFi Games, and
# related trademarks, code, and assets belong to MaFi Games. This repository is
# intended to contain only original mod code/configuration; if MaFi Games material
# is included by mistake, I intend to correct it promptly upon discovery or notice.
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$Package
)

$ErrorActionPreference = 'Stop'
$solution = Join-Path $PSScriptRoot 'AutoForestryDesignations.sln'
$manifestPath = Join-Path $PSScriptRoot 'manifest.json'
$artifactsDir = Join-Path $PSScriptRoot 'artifacts'
$stagingDir = Join-Path $artifactsDir 'package'
$archiveDir = Join-Path $PSScriptRoot 'archive'

if (-not $PSBoundParameters.ContainsKey('Package')) {
    $Package = $Configuration -eq 'Release'
}

Write-Host "Building AutoForestryDesignations ($Configuration)..."
dotnet build $solution -c $Configuration
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

if (-not $Package) {
    Write-Host 'Build completed.'
    exit 0
}

if (-not (Test-Path $manifestPath)) {
    throw 'manifest.json was not found.'
}

$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$packageId = if ($manifest.id) { [string]$manifest.id } else { 'AutoForestryDesignations' }
$packageVersion = if ($manifest.version) { [string]$manifest.version } else { 'dev' }

$packageRootName = 'AutoForestryDesignations'
$zipPath = Join-Path $PSScriptRoot ("{0}-{1}.zip" -f $packageId, $packageVersion)
$packageRootDir = Join-Path $stagingDir $packageRootName

# Archive old zip files
New-Item -ItemType Directory -Path $archiveDir -Force | Out-Null
$oldZips = Get-ChildItem -Path $PSScriptRoot -Filter "$packageId-*.zip" -File -ErrorAction SilentlyContinue
foreach ($oldZip in $oldZips) {
    $archivePath = Join-Path $archiveDir $oldZip.Name
    Move-Item -Path $oldZip.FullName -Destination $archivePath -Force
    Write-Host "Archived: $($oldZip.Name)"
}

New-Item -ItemType Directory -Path $artifactsDir -Force | Out-Null
if (Test-Path $stagingDir) {
    Remove-Item $stagingDir -Recurse -Force
}
New-Item -ItemType Directory -Path $packageRootDir -Force | Out-Null

$filesToInclude = @(
    'manifest.json',
    'config.json',
    'AutoForestryDesignations.dll',
    '0Harmony.dll',
    'Thumbnail.png',
    'changelog.txt',
    'readme.md',
    'LICENSE'
)

foreach ($file in $filesToInclude) {
    $sourcePath = Join-Path $PSScriptRoot $file
    if (Test-Path $sourcePath) {
        Copy-Item $sourcePath -Destination (Join-Path $packageRootDir $file) -Force
    }
}

$translationsDir = Join-Path $PSScriptRoot 'translations'
if (Test-Path $translationsDir) {
    Copy-Item $translationsDir -Destination (Join-Path $packageRootDir 'translations') -Recurse -Force
}

if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

# ZIP entry names must use forward slashes. Compress-Archive has historically
# emitted Windows-style backslashes in some host/module combinations, which
# makes the package fail to extract correctly with some Linux archive tools.
$zipStream = [System.IO.File]::Open($zipPath, [System.IO.FileMode]::CreateNew)
try {
    $zip = [System.IO.Compression.ZipArchive]::new(
        $zipStream,
        [System.IO.Compression.ZipArchiveMode]::Create,
        $false)
    try {
        Get-ChildItem -Path $stagingDir -File -Recurse | ForEach-Object {
            $entryName = $_.FullName.Substring($stagingDir.Length).
                Replace('\', '/').
                Replace([System.IO.Path]::AltDirectorySeparatorChar, '/').
                TrimStart('/')
            $entry = $zip.CreateEntry($entryName, [System.IO.Compression.CompressionLevel]::Optimal)
            $entryStream = $entry.Open()
            try {
                $sourceStream = [System.IO.File]::OpenRead($_.FullName)
                try {
                    $sourceStream.CopyTo($entryStream)
                }
                finally {
                    $sourceStream.Dispose()
                }
            }
            finally {
                $entryStream.Dispose()
            }
        }
    }
    finally {
        $zip.Dispose()
    }
}
finally {
    $zipStream.Dispose()
}

$readZip = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
try {
    $invalidEntry = $readZip.Entries |
        Where-Object { $_.FullName.Contains('\') } |
        Select-Object -First 1
}
finally {
    $readZip.Dispose()
}
if ($invalidEntry) {
    throw "Package contains a non-portable ZIP entry name: $($invalidEntry.FullName)"
}

Write-Host "Created package: $zipPath"
exit 0

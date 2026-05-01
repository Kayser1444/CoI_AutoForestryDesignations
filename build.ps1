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
    'AutoForestryDesignations.dll',
    '0Harmony.dll',
    'Thumbnail.png',
    'change log.md',
    'readme.md',
    'LICENSE',
    'settings.json'
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

Compress-Archive -Path $packageRootDir -DestinationPath $zipPath -Force
Write-Host "Created package: $zipPath"
exit 0

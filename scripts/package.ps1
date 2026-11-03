param(
    [ValidateSet("win-x64")]
    [string]$RuntimeIdentifier = "win-x64"
)

$ErrorActionPreference = "Stop"
$ProjectDirectory = Split-Path -Parent $PSScriptRoot
$ArtifactDirectory = Join-Path $ProjectDirectory "artifacts"
$PackageDirectory = Join-Path $ArtifactDirectory "Jalil-$RuntimeIdentifier"
$ArchivePath = Join-Path $ArtifactDirectory "Jalil-$RuntimeIdentifier.zip"

if (Test-Path $PackageDirectory) {
    Remove-Item -Recurse -Force $PackageDirectory
}

if (Test-Path $ArchivePath) {
    Remove-Item -Force $ArchivePath
}

New-Item -ItemType Directory -Force -Path $PackageDirectory | Out-Null

dotnet publish (Join-Path $ProjectDirectory "src/Jalil.App/Jalil.App.csproj") `
    --configuration Release `
    --runtime $RuntimeIdentifier `
    --self-contained true `
    --output $PackageDirectory `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None

Copy-Item (Join-Path $ProjectDirectory "distribution/Start Jalil.cmd") $PackageDirectory
Copy-Item (Join-Path $ProjectDirectory "distribution/QUICK_START.txt") $PackageDirectory
Compress-Archive -Path "$PackageDirectory\*" -DestinationPath $ArchivePath
Write-Host "Created $ArchivePath"

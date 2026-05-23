<#
.SYNOPSIS
    Publish WallpaperApp and package it as an NSIS installer.

.EXAMPLE
    .\build-installer.ps1
    .\build-installer.ps1 -Version 1.2.0
    .\build-installer.ps1 -Version 1.2.0 -SkipPublish
#>
[CmdletBinding()]
param(
    [string]$Version    = "1.0.0",
    [string]$Project    = "WallpaperApp\WallpaperApp.csproj",
    [string]$PublishDir = "publish\app",
    [string]$NsisScript = "installer\WallpaperApp.nsi",
    [string]$NsisPath   = "C:\Program Files (x86)\NSIS\Bin\makensis.exe",
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $NsisPath)) {
    throw "makensis.exe not found at '$NsisPath'. Install NSIS or pass -NsisPath."
}

$InstallerDir = Split-Path -Parent $PublishDir
if (-not (Test-Path $InstallerDir)) {
    New-Item -ItemType Directory -Path $InstallerDir | Out-Null
}

if (-not $SkipPublish) {
    Write-Host "==> Cleaning $PublishDir" -ForegroundColor Cyan
    if (Test-Path $PublishDir) {
        Remove-Item $PublishDir -Recurse -Force
    }

    Write-Host "==> dotnet publish (Version=$Version)" -ForegroundColor Cyan
    dotnet publish $Project `
        -c Release `
        -o $PublishDir `
        -p:Version=$Version `
        -p:AssemblyVersion="$Version.0" `
        -p:FileVersion="$Version.0"

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed (exit $LASTEXITCODE)."
    }
}

Write-Host "==> makensis $NsisScript" -ForegroundColor Cyan
& $NsisPath "/DAPP_VERSION=$Version" $NsisScript

if ($LASTEXITCODE -ne 0) {
    throw "makensis failed (exit $LASTEXITCODE)."
}

$installer = Join-Path $InstallerDir "WallpaperApp-Setup-$Version.exe"
if (Test-Path $installer) {
    $size = [math]::Round((Get-Item $installer).Length / 1MB, 1)
    Write-Host ""
    Write-Host "Installer ready: $installer ($size MB)" -ForegroundColor Green
}
else {
    throw "Expected installer not found: $installer"
}

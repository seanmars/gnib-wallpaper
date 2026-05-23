<#
.SYNOPSIS
    Rebuild installer/setup.ico from setup.svg.

.DESCRIPTION
    Run this whenever setup.svg is edited. Requires ImageMagick 7+ on PATH.
#>
[CmdletBinding()]
param(
    [string]$Svg = "$PSScriptRoot\setup.svg",
    [string]$Ico = "$PSScriptRoot\setup.ico"
)

$ErrorActionPreference = "Stop"

if (-not (Get-Command magick -ErrorAction SilentlyContinue)) {
    throw "ImageMagick (magick.exe) not found on PATH."
}

if (-not (Test-Path $Svg)) {
    throw "Source SVG not found: $Svg"
}

Write-Host "==> magick $Svg -> $Ico" -ForegroundColor Cyan
magick -background none $Svg `
    -define icon:auto-resize=16,24,32,48,64,128,256 `
    $Ico

if ($LASTEXITCODE -ne 0) {
    throw "magick failed (exit $LASTEXITCODE)."
}

$size = [math]::Round((Get-Item $Ico).Length / 1KB, 1)
Write-Host "OK: $Ico ($size KB)" -ForegroundColor Green

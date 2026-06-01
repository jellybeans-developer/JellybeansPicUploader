# PicX-WPF publish script
# Usage: run from PicX-WPF folder -> .\publish.ps1

$ErrorActionPreference = "Stop"
$ProjectPath = Join-Path $PSScriptRoot "PicX.Wpf\PicX.Wpf.csproj"
$DistRoot = Join-Path $PSScriptRoot "dist"
$Version = "1.0.0"

Write-Host "Cleaning dist folder..." -ForegroundColor Cyan
if (Test-Path $DistRoot) {
    Remove-Item -Path $DistRoot -Recurse -Force
}

Write-Host "Publishing self-contained win-x64..." -ForegroundColor Cyan
dotnet publish $ProjectPath `
    -p:PublishProfile=win-x64-self-contained `
    -c Release

Write-Host "Publishing framework-dependent win-x64..." -ForegroundColor Cyan
dotnet publish $ProjectPath `
    -p:PublishProfile=win-x64-framework-dependent `
    -c Release

Write-Host "Creating ZIP archives..." -ForegroundColor Cyan
$SelfContainedDir = Join-Path $DistRoot "PicX-WPF-win-x64-self-contained"
$FrameworkDependentDir = Join-Path $DistRoot "PicX-WPF-win-x64-framework-dependent"

if (Test-Path $SelfContainedDir) {
    $SelfContainedZip = Join-Path $DistRoot "PicX-WPF-$Version-win-x64-self-contained.zip"
    Compress-Archive -Path (Join-Path $SelfContainedDir "*") -DestinationPath $SelfContainedZip -Force
    Write-Host "Created: $SelfContainedZip" -ForegroundColor Green
}

if (Test-Path $FrameworkDependentDir) {
    $FrameworkZip = Join-Path $DistRoot "PicX-WPF-$Version-win-x64-framework-dependent.zip"
    Compress-Archive -Path (Join-Path $FrameworkDependentDir "*") -DestinationPath $FrameworkZip -Force
    Write-Host "Created: $FrameworkZip" -ForegroundColor Green
}

Write-Host ""
Write-Host "Done. Output: $DistRoot" -ForegroundColor Green

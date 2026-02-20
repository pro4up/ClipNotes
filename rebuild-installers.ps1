# rebuild-installers.ps1
# Пересобирает установочники ClipNotes из текущего репозитория.
# Запускать из папки source\ (рядом с build.ps1).
#
# Использование:
#   .\rebuild-installers.ps1              # Online + Portable
#   .\rebuild-installers.ps1 -Offline    # Online + Portable + Offline (~450 MB)
#   .\rebuild-installers.ps1 -PortableOnly
#
param(
    [switch]$Offline,      # Собрать также офлайн-установочник (требует интернет для CUDA whisper)
    [switch]$PortableOnly  # Собрать только portable ZIP
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot

Write-Host ""
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "  ClipNotes Installer Rebuild" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Output: $scriptDir\..\Setup\" -ForegroundColor White
Write-Host ""

if ($PortableOnly) {
    Write-Host "Mode: Portable ZIP only" -ForegroundColor Yellow
    & "$scriptDir\build.ps1" -SkipDependencies -SkipModel -BuildPortable
}
elseif ($Offline) {
    Write-Host "Mode: Online Setup + Portable + Offline Setup" -ForegroundColor Yellow
    Write-Host "  Note: Offline build downloads CUDA whisper (~400 MB)" -ForegroundColor DarkYellow
    Write-Host ""
    & "$scriptDir\build.ps1" -SkipDependencies -SkipModel -BuildSetup -BuildPortable -BuildOfflineSetup
}
else {
    Write-Host "Mode: Online Setup + Portable ZIP" -ForegroundColor Yellow
    Write-Host "  Tip: Use -Offline to also build the offline installer" -ForegroundColor DarkGray
    Write-Host ""
    & "$scriptDir\build.ps1" -SkipDependencies -SkipModel -BuildSetup -BuildPortable
}

Write-Host ""
Write-Host "=====================================" -ForegroundColor Green
Write-Host "  Done! Files in Setup\ folder:" -ForegroundColor Green
Write-Host "=====================================" -ForegroundColor Green
$setupDir = "$scriptDir\..\Setup"
if (Test-Path $setupDir) {
    Get-ChildItem $setupDir -File | ForEach-Object {
        $mb = [math]::Round($_.Length / 1MB, 1)
        Write-Host ("  {0,-35} {1,7} MB" -f $_.Name, $mb) -ForegroundColor White
    }
}

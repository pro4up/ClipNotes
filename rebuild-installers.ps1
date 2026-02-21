# rebuild-installers.ps1
# Пересобирает установщик ClipNotes из текущего репозитория.
# Запускать из папки source\ (рядом с build.ps1).

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot

Write-Host ""
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "  ClipNotes Installer Rebuild" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Output: $scriptDir\..\Setup\" -ForegroundColor White
Write-Host ""

& "$scriptDir\build.ps1" -SkipDependencies -SkipModel -BuildSetup

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

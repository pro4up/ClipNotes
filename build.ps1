# ClipNotes Build Script
# Builds the entire project: downloads dependencies, compiles app, packages for distribution
param(
    [switch]$SkipDependencies,
    [switch]$SkipModel,
    [string]$Model = "large-v3-turbo",
    [string]$Configuration = "Release",
    [string]$Backend = "cpu",           # cpu | cuda  (whisper backend)
    [switch]$BuildSetup,                # Собрать Online Setup (~10 MB)
    [switch]$BuildOfflineSetup          # Собрать Offline Setup (~450 MB, включает bundle)
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$sourceDir = "$scriptDir\ClipNotes"
$setupSourceDir = "$scriptDir\ClipNotes.Setup"
$compileDir = "$scriptDir\..\app"
$appDir     = "$compileDir\app"       # ClipNotes.exe + Uninstaller.exe go here
$setupOutputDir = "$scriptDir\..\Setup"
$toolsOutputDir = "$compileDir\tools"
$modelsOutputDir = "$compileDir\models"
$licensesDir = "$compileDir\licenses"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  ClipNotes Build System" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Ensure output directories
foreach ($dir in @($compileDir, $appDir, $toolsOutputDir, $modelsOutputDir, $licensesDir)) {
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
}

# Step 1: Download dependencies
if (-not $SkipDependencies) {
    Write-Host "[1/6] Downloading FFmpeg..." -ForegroundColor Yellow
    & "$scriptDir\tools\download-ffmpeg.ps1" -OutputDir $toolsOutputDir

    Write-Host ""
    Write-Host "[2/6] Building/Downloading whisper-cli..." -ForegroundColor Yellow
    & "$scriptDir\tools\download-whisper.ps1" -OutputDir $toolsOutputDir -Backend $Backend
} else {
    Write-Host "[1-2/6] Skipping dependency downloads." -ForegroundColor DarkGray
}

# Step 3: Download model
if (-not $SkipModel) {
    Write-Host ""
    Write-Host "[3/6] Downloading whisper model ($Model)..." -ForegroundColor Yellow
    & "$scriptDir\tools\download-model.ps1" -ModelName $Model -OutputDir $modelsOutputDir
} else {
    Write-Host "[3/6] Skipping model download." -ForegroundColor DarkGray
}

# Step 4: Restore and build
Write-Host ""
Write-Host "[4/6] Restoring NuGet packages..." -ForegroundColor Yellow
dotnet restore "$sourceDir\ClipNotes.csproj"
if ($LASTEXITCODE -ne 0) { throw "NuGet restore failed" }

Write-Host ""
Write-Host "[5/6] Publishing application..." -ForegroundColor Yellow
dotnet publish "$sourceDir\ClipNotes.csproj" `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:IncludeNativeLibrariesForSelfExtract=false `
    -o "$appDir" `
    --nologo
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

Write-Host ""
Write-Host "[5b/6] Publishing Uninstaller..." -ForegroundColor Yellow
$uninstallerDir = "$scriptDir\ClipNotes.Uninstaller"
if (Test-Path $uninstallerDir) {
    dotnet publish "$uninstallerDir\ClipNotes.Uninstaller.csproj" `
        -c $Configuration `
        -r win-x64 --self-contained true `
        -p:PublishSingleFile=false `
        -o "$appDir" --nologo
    if ($LASTEXITCODE -ne 0) { throw "Uninstaller build failed" }
} else {
    Write-Host "  [skip] ClipNotes.Uninstaller project not found" -ForegroundColor DarkGray
}

# Step 5c: Move lang/ from appDir up to compileDir (new hierarchy: lang/ at root, not inside app/)
# Use Copy+Delete instead of Move-Item to avoid PowerShell timing issues where Move-Item
# places the folder *inside* the destination if the destination still exists on disk.
Write-Host ""
Write-Host "[5c/6] Moving lang/ to root output dir..." -ForegroundColor Yellow
if (Test-Path "$appDir\lang") {
    if (Test-Path "$compileDir\lang") { Remove-Item "$compileDir\lang" -Recurse -Force }
    Copy-Item "$appDir\lang" "$compileDir\lang" -Recurse
    Remove-Item "$appDir\lang" -Recurse -Force
    Write-Host "  [OK] lang/ moved to $compileDir\lang" -ForegroundColor Green
} else {
    Write-Host "  [skip] lang/ not found in app output" -ForegroundColor DarkGray
}

# Step 6: Create licenses
Write-Host ""
Write-Host "[6/6] Creating license files..." -ForegroundColor Yellow

@"
ClipNotes Licenses
==================

This application bundles the following third-party components:

1. FFmpeg (https://ffmpeg.org)
   License: LGPL v2.1+ / GPL v2+
   FFmpeg is a trademark of Fabrice Bellard.
   See: https://ffmpeg.org/legal.html

2. whisper.cpp (https://github.com/ggerganov/whisper.cpp)
   License: MIT
   Copyright (c) 2023-2024 Georgi Gerganov

3. ClosedXML (https://github.com/ClosedXML/ClosedXML)
   License: MIT
   Copyright (c) ClosedXML contributors

4. CommunityToolkit.Mvvm (https://github.com/CommunityToolkit/dotnet)
   License: MIT
   Copyright (c) .NET Foundation and Contributors

5. OpenAI Whisper Models
   License: MIT
   Copyright (c) 2022 OpenAI
"@ | Out-File -FilePath "$licensesDir\LICENSES.txt" -Encoding UTF8

@"
MIT License

Copyright (c) 2023-2024 Georgi Gerganov

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
"@ | Out-File -FilePath "$licensesDir\whisper.cpp-MIT.txt" -Encoding UTF8

@"
MIT License

Copyright (c) ClosedXML contributors

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
"@ | Out-File -FilePath "$licensesDir\ClosedXML-MIT.txt" -Encoding UTF8

@"
FFmpeg License Notes
====================

FFmpeg is licensed under the GNU Lesser General Public License (LGPL) version 2.1
or later. Some optional components are licensed under the GNU General Public
License (GPL) version 2 or later.

The FFmpeg binaries included with this distribution are from:
https://www.gyan.dev/ffmpeg/builds/

For full license text, see:
- LGPL: https://www.gnu.org/licenses/old-licenses/lgpl-2.1.html
- GPL: https://www.gnu.org/licenses/old-licenses/gpl-2.0.html

FFmpeg source code: https://ffmpeg.org/download.html
"@ | Out-File -FilePath "$licensesDir\FFmpeg-LICENSE.txt" -Encoding UTF8

# ── Setup: Online ──────────────────────────────────────────────────────────────
if ($BuildSetup) {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "  Building Online Setup..." -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan

    if (-not (Test-Path $setupOutputDir)) { New-Item -ItemType Directory -Path $setupOutputDir -Force | Out-Null }

    # Создать ClipNotes-bundle.zip — архив приложения для загрузки online-установщиком
    $bundleZip = "$setupOutputDir\ClipNotes-bundle.zip"
    if (Test-Path $bundleZip) { Remove-Item $bundleZip }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [System.IO.Compression.ZipFile]::Open($bundleZip, 'Create')
    try {
        $addFile = {
            param($srcPath, $entryName)
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $srcPath, $entryName, [System.IO.Compression.CompressionLevel]::Fastest) | Out-Null
        }
        # App exe/dll/json under ClipNotes/app/
        Get-ChildItem "$appDir" -File | Where-Object { $_.Extension -in '.exe','.dll','.json' } | ForEach-Object {
            & $addFile $_.FullName "ClipNotes/app/$($_.Name)"
        }
        # licenses/ at root level: ClipNotes/licenses/
        if (Test-Path "$licensesDir") {
            Get-ChildItem "$licensesDir" -File | ForEach-Object {
                & $addFile $_.FullName "ClipNotes/licenses/$($_.Name)"
            }
        }
        # lang/ at root level: ClipNotes/lang/ru/lang.json etc.
        if (Test-Path "$compileDir\lang") {
            $resolvedLangDir = (Resolve-Path "$compileDir\lang").Path
            Get-ChildItem "$compileDir\lang" -Recurse -File | ForEach-Object {
                $relPath = $_.FullName.Substring("$resolvedLangDir\".Length).Replace('\', '/')
                & $addFile $_.FullName "ClipNotes/lang/$relPath"
            }
        }
    } finally {
        $zip.Dispose()
    }
    $bundleSize = [math]::Round((Get-Item $bundleZip).Length / 1MB, 1)
    Write-Host "[OK] ClipNotes-bundle.zip ($bundleSize MB)" -ForegroundColor Green

    dotnet publish "$setupSourceDir\ClipNotes.Setup.csproj" `
        -c Release -r win-x64 --self-contained false `
        -p:PublishSingleFile=true `
        -o "$setupOutputDir\Online" --nologo
    if ($LASTEXITCODE -ne 0) { throw "Setup build failed" }

    $onlineExe = "$setupOutputDir\Online\ClipNotes.Setup.exe"
    $destExe   = "$setupOutputDir\ClipNotes-Setup.exe"
    if (Test-Path $destExe) {
        Get-Process "ClipNotes.Setup" -ErrorAction SilentlyContinue | Stop-Process -Force
        Start-Sleep -Milliseconds 500
        Remove-Item $destExe -Force
    }
    Move-Item $onlineExe $destExe
    Remove-Item "$setupOutputDir\Online" -Recurse -Force -ErrorAction SilentlyContinue

    Write-Host "[OK] ClipNotes-Setup.exe" -ForegroundColor Green
}

# ── Setup: Offline ─────────────────────────────────────────────────────────────
if ($BuildOfflineSetup) {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "  Building Offline Setup..." -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan

    if (-not (Test-Path $setupOutputDir)) { New-Item -ItemType Directory -Path $setupOutputDir -Force | Out-Null }

    # Скачиваем все модели для offline bundle
    $allModels = @("base", "small", "medium", "large-v3-turbo", "large-v3")
    Write-Host "  Downloading all whisper models..." -ForegroundColor Yellow
    foreach ($m in $allModels) {
        Write-Host "    model: $m" -ForegroundColor DarkYellow
        & "$scriptDir\tools\download-model.ps1" -ModelName $m -OutputDir $modelsOutputDir
    }

    # Скачиваем CUDA whisper для offline bundle (CPU уже в app/tools/)
    $cudaTempDir = "$setupSourceDir\Resources\whisper-cuda-temp"
    if (-not (Test-Path $cudaTempDir)) { New-Item -ItemType Directory -Path $cudaTempDir -Force | Out-Null }
    Write-Host "  Downloading CUDA whisper for offline bundle..." -ForegroundColor Yellow
    & "$scriptDir\tools\download-whisper.ps1" -OutputDir $cudaTempDir -Backend "cuda"

    # Создать bundle с чёткой структурой папок
    $bundleZip = "$setupSourceDir\Resources\ClipNotes-offline-bundle.zip"
    Write-Host "  Creating ClipNotes-offline-bundle.zip..." -ForegroundColor Yellow

    if (Test-Path $bundleZip) { Remove-Item $bundleZip }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [System.IO.Compression.ZipFile]::Open($bundleZip, 'Create')
    try {
        $addFile = {
            param($srcPath, $entryName)
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $srcPath, $entryName, [System.IO.Compression.CompressionLevel]::Fastest) | Out-Null
        }

        # app/ — app + uninstaller exe/dll from $appDir (forward slashes for ZIP compatibility)
        Get-ChildItem "$appDir" -File | Where-Object { $_.Extension -in '.exe','.dll','.json' } | ForEach-Object {
            & $addFile $_.FullName "app/$($_.Name)"
        }

        # tools/ — только FFmpeg
        @("ffmpeg.exe", "ffprobe.exe") | ForEach-Object {
            $f = Join-Path $toolsOutputDir $_
            if (Test-Path $f) { & $addFile $f "tools/$_" }
        }

        # whisper-cpu/ — CPU бэкенд whisper
        if (Test-Path $toolsOutputDir) {
            Get-ChildItem $toolsOutputDir -File | Where-Object { $_.Name -notin @("ffmpeg.exe","ffprobe.exe") } | ForEach-Object {
                Write-Host "    + whisper-cpu/$($_.Name)" -ForegroundColor DarkGray
                & $addFile $_.FullName "whisper-cpu/$($_.Name)"
            }
        }

        # whisper-cuda/ — CUDA бэкенд whisper
        if (Test-Path $cudaTempDir) {
            Get-ChildItem $cudaTempDir -File | ForEach-Object {
                Write-Host "    + whisper-cuda/$($_.Name)" -ForegroundColor DarkGray
                & $addFile $_.FullName "whisper-cuda/$($_.Name)"
            }
        }

        # models/ — все whisper-модели
        if (Test-Path $modelsOutputDir) {
            Get-ChildItem $modelsOutputDir -File | ForEach-Object {
                Write-Host "    + models/$($_.Name)" -ForegroundColor DarkGray
                & $addFile $_.FullName "models/$($_.Name)"
            }
        }

        # licenses/
        if (Test-Path "$licensesDir") {
            Get-ChildItem "$licensesDir" -File | ForEach-Object {
                & $addFile $_.FullName "licenses/$($_.Name)"
            }
        }

        # lang/ — локализация (lang/ru/lang.json, lang/en/lang.json, ...)
        if (Test-Path "$compileDir\lang") {
            $resolvedLangDir = (Resolve-Path "$compileDir\lang").Path
            Get-ChildItem "$compileDir\lang" -Recurse -File | ForEach-Object {
                $relPath = $_.FullName.Substring("$resolvedLangDir\".Length).Replace('\', '/')
                & $addFile $_.FullName "lang/$relPath"
            }
        }
    } finally {
        $zip.Dispose()
        Remove-Item $cudaTempDir -Recurse -Force -ErrorAction SilentlyContinue
    }

    $bundleSize = [math]::Round((Get-Item $bundleZip).Length / 1MB, 1)
    Write-Host "  ClipNotes-offline-bundle.zip: $bundleSize MB" -ForegroundColor Yellow

    dotnet publish "$setupSourceDir\ClipNotes.Setup.csproj" `
        -c Release -r win-x64 --self-contained false `
        -p:PublishSingleFile=true `
        -p:DefineConstants="OFFLINE_BUILD" `
        -o "$setupOutputDir\Offline" --nologo
    if ($LASTEXITCODE -ne 0) { throw "Offline setup build failed" }

    $offlineExe = "$setupOutputDir\Offline\ClipNotes.Setup.exe"
    $destExe    = "$setupOutputDir\ClipNotes-Setup-Offline.exe"
    $destBundle = "$setupOutputDir\ClipNotes-offline-bundle.zip"
    if (Test-Path $destExe) { Remove-Item $destExe }
    if (Test-Path $destBundle) { Remove-Item $destBundle }
    Move-Item $offlineExe $destExe
    Copy-Item $bundleZip $destBundle
    Remove-Item "$setupOutputDir\Offline" -Recurse -Force -ErrorAction SilentlyContinue
    # Удаляем временный bundle из Resources и старый app-bundle.zip
    Remove-Item $bundleZip -ErrorAction SilentlyContinue
    Remove-Item "$setupOutputDir\app-bundle.zip" -ErrorAction SilentlyContinue

    Write-Host "[OK] ClipNotes-Setup-Offline.exe + ClipNotes-offline-bundle.zip" -ForegroundColor Green
}

# ── SHA256 ─────────────────────────────────────────────────────────────────────
if (($BuildSetup -or $BuildOfflineSetup) -and (Test-Path $setupOutputDir)) {
    $shaFile = "$setupOutputDir\SHA256SUMS.txt"
    if (Test-Path $shaFile) { Remove-Item $shaFile }
    Get-ChildItem $setupOutputDir -File -Filter "*.exe" | ForEach-Object {
        $hash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash
        "$hash  $($_.Name)" | Add-Content $shaFile
    }
    Get-ChildItem $setupOutputDir -File -Filter "*.zip" | ForEach-Object {
        $hash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash
        "$hash  $($_.Name)" | Add-Content $shaFile
    }
    if (Test-Path $shaFile) { Write-Host "[OK] SHA256SUMS.txt" -ForegroundColor Green }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Build Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Output root: $compileDir" -ForegroundColor White
Write-Host "App dir:     $appDir" -ForegroundColor White
Write-Host ""
Write-Host "Contents:" -ForegroundColor White
if (Test-Path "$appDir\ClipNotes.exe") { Write-Host "  [OK] app\ClipNotes.exe" -ForegroundColor Green }
else { Write-Host "  [!!] app\ClipNotes.exe MISSING" -ForegroundColor Red }

if (Test-Path "$toolsOutputDir\ffmpeg.exe") { Write-Host "  [OK] tools\ffmpeg.exe" -ForegroundColor Green }
else { Write-Host "  [!!] tools\ffmpeg.exe MISSING" -ForegroundColor Red }

if (Test-Path "$toolsOutputDir\ffprobe.exe") { Write-Host "  [OK] tools\ffprobe.exe" -ForegroundColor Green }
else { Write-Host "  [!!] tools\ffprobe.exe MISSING" -ForegroundColor Red }

if (Test-Path "$toolsOutputDir\whisper-cli.exe") { Write-Host "  [OK] tools\whisper-cli.exe" -ForegroundColor Green }
else { Write-Host "  [!!] tools\whisper-cli.exe MISSING" -ForegroundColor Red }

if (Test-Path "$appDir\ClipNotes.Uninstaller.exe") { Write-Host "  [OK] app\ClipNotes.Uninstaller.exe" -ForegroundColor Green }
else { Write-Host "  [!!] app\ClipNotes.Uninstaller.exe MISSING" -ForegroundColor Yellow }

$modelFile = Get-ChildItem -Path $modelsOutputDir -Filter "ggml-*.bin" -ErrorAction SilentlyContinue | Select-Object -First 1
if ($modelFile) { Write-Host "  [OK] models\$($modelFile.Name)" -ForegroundColor Green }
else { Write-Host "  [!!] Model file MISSING" -ForegroundColor Red }

if (Test-Path "$compileDir\lang") { Write-Host "  [OK] lang\" -ForegroundColor Green }
else { Write-Host "  [!!] lang\ MISSING" -ForegroundColor Red }

Write-Host "  [OK] licenses\" -ForegroundColor Green
Write-Host ""

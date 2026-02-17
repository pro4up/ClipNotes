# ClipNotes Build Script
# Builds the entire project: downloads dependencies, compiles app, packages for distribution
param(
    [switch]$SkipDependencies,
    [switch]$SkipModel,
    [string]$Model = "large-v3-turbo",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$sourceDir = "$scriptDir\ClipNotes"
$compileDir = "$scriptDir\..\app"
$toolsOutputDir = "$compileDir\tools"
$modelsOutputDir = "$compileDir\models"
$licensesDir = "$compileDir\licenses"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  ClipNotes Build System" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Ensure output directories
foreach ($dir in @($compileDir, $toolsOutputDir, $modelsOutputDir, $licensesDir)) {
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
}

# Step 1: Download dependencies
if (-not $SkipDependencies) {
    Write-Host "[1/6] Downloading FFmpeg..." -ForegroundColor Yellow
    & "$scriptDir\tools\download-ffmpeg.ps1" -OutputDir $toolsOutputDir

    Write-Host ""
    Write-Host "[2/6] Building/Downloading whisper-cli..." -ForegroundColor Yellow
    & "$scriptDir\tools\download-whisper.ps1" -OutputDir $toolsOutputDir
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
    -o "$compileDir" `
    --nologo
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

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

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Build Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Output directory: $compileDir" -ForegroundColor White
Write-Host ""
Write-Host "Contents:" -ForegroundColor White
if (Test-Path "$compileDir\ClipNotes.exe") { Write-Host "  [OK] ClipNotes.exe" -ForegroundColor Green }
else { Write-Host "  [!!] ClipNotes.exe MISSING" -ForegroundColor Red }

if (Test-Path "$toolsOutputDir\ffmpeg.exe") { Write-Host "  [OK] tools\ffmpeg.exe" -ForegroundColor Green }
else { Write-Host "  [!!] tools\ffmpeg.exe MISSING" -ForegroundColor Red }

if (Test-Path "$toolsOutputDir\ffprobe.exe") { Write-Host "  [OK] tools\ffprobe.exe" -ForegroundColor Green }
else { Write-Host "  [!!] tools\ffprobe.exe MISSING" -ForegroundColor Red }

if (Test-Path "$toolsOutputDir\whisper-cli.exe") { Write-Host "  [OK] tools\whisper-cli.exe" -ForegroundColor Green }
else { Write-Host "  [!!] tools\whisper-cli.exe MISSING" -ForegroundColor Red }

$modelFile = Get-ChildItem -Path $modelsOutputDir -Filter "ggml-*.bin" -ErrorAction SilentlyContinue | Select-Object -First 1
if ($modelFile) { Write-Host "  [OK] models\$($modelFile.Name)" -ForegroundColor Green }
else { Write-Host "  [!!] Model file MISSING" -ForegroundColor Red }

Write-Host "  [OK] licenses\" -ForegroundColor Green
Write-Host ""

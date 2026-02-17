# Download FFmpeg essentials build from gyan.dev
param(
    [string]$OutputDir = "$PSScriptRoot\..\compile\ClipNotes-win-x64\tools"
)

$ErrorActionPreference = "Stop"

$ffmpegUrl = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip"
$cacheDir = "$PSScriptRoot\.cache"
$cacheZip = "$cacheDir\ffmpeg-essentials.zip"

if (-not (Test-Path $OutputDir)) { New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null }
if (-not (Test-Path $cacheDir)) { New-Item -ItemType Directory -Path $cacheDir -Force | Out-Null }

# Check if already extracted
if ((Test-Path "$OutputDir\ffmpeg.exe") -and (Test-Path "$OutputDir\ffprobe.exe")) {
    Write-Host "[FFmpeg] Already present, skipping download."
    return
}

# Download if not cached
if (-not (Test-Path $cacheZip)) {
    Write-Host "[FFmpeg] Downloading from gyan.dev..."
    Invoke-WebRequest -Uri $ffmpegUrl -OutFile $cacheZip -UseBasicParsing
    Write-Host "[FFmpeg] Download complete."
} else {
    Write-Host "[FFmpeg] Using cached archive."
}

# Extract
Write-Host "[FFmpeg] Extracting..."
$extractDir = "$cacheDir\ffmpeg-extract"
if (Test-Path $extractDir) { Remove-Item -Recurse -Force $extractDir }
Expand-Archive -Path $cacheZip -DestinationPath $extractDir -Force

# Find the binaries inside the extracted folder
$binDir = Get-ChildItem -Path $extractDir -Recurse -Directory -Filter "bin" | Select-Object -First 1

if ($binDir) {
    Copy-Item "$($binDir.FullName)\ffmpeg.exe" "$OutputDir\ffmpeg.exe" -Force
    Copy-Item "$($binDir.FullName)\ffprobe.exe" "$OutputDir\ffprobe.exe" -Force
    Write-Host "[FFmpeg] Installed to $OutputDir"
} else {
    throw "[FFmpeg] Could not find bin directory in archive"
}

# Cleanup extract dir
Remove-Item -Recurse -Force $extractDir

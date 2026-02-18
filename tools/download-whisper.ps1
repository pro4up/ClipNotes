# Download or build whisper-cli for ClipNotes
# Backends: cpu (default, OpenBLAS-optimized), cuda (NVIDIA GPU)
param(
    [string]$OutputDir  = "$PSScriptRoot\..\..\app\tools",
    [string]$Backend    = "cpu",      # cpu | cuda
    [string]$WhisperTag = "v1.8.3"
)

$ErrorActionPreference = "Stop"

$repoBase = "https://github.com/ggml-org/whisper.cpp/releases/download/$WhisperTag"

$assetMap = @{
    "cpu"  = "whisper-blas-bin-x64.zip"   # OpenBLAS — faster CPU, works on any hardware
    "cuda" = "whisper-cublas-12.4.0-bin-x64.zip"  # NVIDIA CUDA 12, requires CUDA Runtime
}

if (-not $assetMap.ContainsKey($Backend)) {
    Write-Host "[Whisper] Unknown backend '$Backend'. Use: cpu | cuda" -ForegroundColor Red
    exit 1
}

$assetName = $assetMap[$Backend]
$assetUrl  = "$repoBase/$assetName"

if (-not (Test-Path $OutputDir)) { New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null }
$cacheDir = "$PSScriptRoot\.cache"
if (-not (Test-Path $cacheDir)) { New-Item -ItemType Directory -Path $cacheDir -Force | Out-Null }

# Check if already present
if (Test-Path "$OutputDir\whisper-cli.exe") {
    Write-Host "[Whisper] whisper-cli.exe already present (backend: $Backend), skipping." -ForegroundColor DarkGray
    return
}

Write-Host "[Whisper] Downloading pre-built whisper-cli ($Backend / $WhisperTag)..." -ForegroundColor Yellow
Write-Host "[Whisper] Asset: $assetName"

$zipPath = "$cacheDir\$assetName"

# Download (skip if cached and same size)
$needDownload = $true
if (Test-Path $zipPath) {
    try {
        $req = [System.Net.WebRequest]::Create($assetUrl)
        $req.Method = "HEAD"
        $resp = $req.GetResponse()
        $remoteSize = $resp.ContentLength
        $resp.Close()
        $localSize = (Get-Item $zipPath).Length
        if ($remoteSize -gt 0 -and $localSize -ge $remoteSize) {
            Write-Host "[Whisper] Using cached $assetName ($localSize bytes)" -ForegroundColor DarkGray
            $needDownload = $false
        } else {
            Write-Host "[Whisper] Cached file incomplete ($localSize / $remoteSize), re-downloading..."
            Remove-Item $zipPath -Force
        }
    } catch { Remove-Item $zipPath -Force -ErrorAction SilentlyContinue }
}

if ($needDownload) {
    Write-Host "[Whisper] Downloading from $assetUrl ..."
    try {
        Invoke-WebRequest -Uri $assetUrl -OutFile $zipPath -UseBasicParsing
    } catch {
        Write-Host "[Whisper] Download failed: $_" -ForegroundColor Red
        Write-Host "[Whisper] Falling back to CPU build from source..." -ForegroundColor Yellow
        Build-FromSource $OutputDir $cacheDir
        return
    }
}

# Extract
$extractDir = "$cacheDir\whisper-extract-$Backend"
if (Test-Path $extractDir) { Remove-Item -Recurse -Force $extractDir }
Expand-Archive -Path $zipPath -DestinationPath $extractDir -Force

# Find whisper-cli.exe
$exe = Get-ChildItem -Path $extractDir -Recurse -Filter "whisper-cli.exe" | Select-Object -First 1
if (-not $exe) {
    $exe = Get-ChildItem -Path $extractDir -Recurse -Filter "main.exe" | Select-Object -First 1
}

if ($exe) {
    Copy-Item $exe.FullName "$OutputDir\whisper-cli.exe" -Force
    Write-Host "[Whisper] Installed whisper-cli.exe ($Backend)" -ForegroundColor Green
} else {
    Write-Host "[Whisper] whisper-cli.exe not found in archive!" -ForegroundColor Red
    exit 1
}

# Copy all required DLLs
$dlls = Get-ChildItem -Path $extractDir -Recurse -Filter "*.dll"
foreach ($dll in $dlls) {
    Copy-Item $dll.FullName "$OutputDir\$($dll.Name)" -Force
    Write-Host "[Whisper]   + $($dll.Name)" -ForegroundColor DarkGray
}

# Cleanup extract dir
Remove-Item -Recurse -Force $extractDir -ErrorAction SilentlyContinue

Write-Host "[Whisper] Done. Backend: $Backend" -ForegroundColor Green
if ($Backend -eq "cuda") {
    Write-Host "[Whisper] NOTE: CUDA backend requires NVIDIA GPU + CUDA 12 Runtime installed." -ForegroundColor Yellow
    Write-Host "[Whisper]       Download: https://developer.nvidia.com/cuda-downloads" -ForegroundColor Yellow
} elseif ($Backend -eq "cpu") {
    Write-Host "[Whisper] NOTE: OpenBLAS CPU backend — uses all CPU cores." -ForegroundColor DarkGray
    Write-Host "[Whisper]       For GPU acceleration: rebuild with -Backend cuda (NVIDIA) or" -ForegroundColor DarkGray
    Write-Host "[Whisper]       compile from source with -DGGML_VULKAN=ON (any GPU with Vulkan)." -ForegroundColor DarkGray
}

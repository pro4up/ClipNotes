# Build whisper-cli from source using CMake + MSVC, with fallback to pre-built binaries (GitHub releases)
param(
    [string]$OutputDir = "$PSScriptRoot\..\compile\ClipNotes-win-x64\tools"
)

$ErrorActionPreference = "Stop"

# Ensure TLS 1.2 for older Windows / PS
try { [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12 } catch {}

$cacheDir   = "$PSScriptRoot\.cache"
$whisperDir = "$cacheDir\whisper.cpp"
$whisperTag = "v1.7.4"   # adjust if you need a newer tag later

Write-Host "[Whisper] OutputDir : $OutputDir"
Write-Host "[Whisper] CacheDir  : $cacheDir"
Write-Host "[Whisper] Tag       : $whisperTag"

if (-not (Test-Path $OutputDir)) { New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null }
if (-not (Test-Path $cacheDir))  { New-Item -ItemType Directory -Path $cacheDir  -Force | Out-Null }

# Check if already built
if (Test-Path "$OutputDir\whisper-cli.exe") {
    Write-Host "[Whisper] whisper-cli.exe already present, skipping."
    return
}

# Clone or update whisper.cpp (repo moved to ggml-org)
if (-not (Test-Path $whisperDir)) {
    Write-Host "[Whisper] Cloning whisper.cpp $whisperTag..."
    git clone --depth 1 --branch $whisperTag https://github.com/ggml-org/whisper.cpp.git $whisperDir
} else {
    Write-Host "[Whisper] Using cached whisper.cpp source: $whisperDir"
}

# Build with CMake
$buildDir = "$whisperDir\build"
if (Test-Path $buildDir) { Remove-Item -Recurse -Force $buildDir }
New-Item -ItemType Directory -Path $buildDir -Force | Out-Null

Write-Host "[Whisper] Configuring with CMake..."
Push-Location $buildDir
try {
    cmake .. -G "Visual Studio 17 2022" -A x64 -DBUILD_SHARED_LIBS=OFF -DWHISPER_BUILD_EXAMPLES=ON -DWHISPER_BUILD_TESTS=OFF
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[Whisper] VS2022 generator failed, trying default generator..."
        cmake .. -DBUILD_SHARED_LIBS=OFF -DWHISPER_BUILD_EXAMPLES=ON -DWHISPER_BUILD_TESTS=OFF
    }

    Write-Host "[Whisper] Building..."
    cmake --build . --config Release --target whisper-cli
    if ($LASTEXITCODE -ne 0) {
        cmake --build . --config Release
    }
} finally {
    Pop-Location
}

# Find and copy the binary
$whisperExe = Get-ChildItem -Path $buildDir -Recurse -Filter "whisper-cli.exe" | Select-Object -First 1
if (-not $whisperExe) {
    # Try alternate name (older builds): main.exe
    $whisperExe = Get-ChildItem -Path $buildDir -Recurse -Filter "main.exe" | Select-Object -First 1
}

if ($whisperExe) {
    Copy-Item $whisperExe.FullName "$OutputDir\whisper-cli.exe" -Force
    Write-Host "[Whisper] Installed whisper-cli.exe to $OutputDir"
    return
}

Write-Host "[Whisper] WARNING: Could not find whisper-cli.exe after build."
Write-Host "[Whisper] Attempting to download pre-built release binaries..."

$releaseZip = "$cacheDir\whisper-bin.zip"
$releaseUrlCandidates = @(
    "https://github.com/ggml-org/whisper.cpp/releases/download/$whisperTag/whisper-bin-x64.zip",
    "https://github.com/ggml-org/whisper.cpp/releases/download/$whisperTag/whisper-blas-bin-x64.zip"
)

$downloaded = $false
foreach ($u in $releaseUrlCandidates) {
    try {
        Write-Host "[Whisper] Trying: $u"
        Invoke-WebRequest -Uri $u -OutFile $releaseZip -UseBasicParsing
        $downloaded = $true
        break
    } catch {
        Write-Host "[Whisper] Failed: $u"
    }
}

if (-not $downloaded) {
    throw "[Whisper] Could not download pre-built binaries from GitHub releases for tag $whisperTag"
}

$extractDir = "$cacheDir\whisper-bin-extract"
if (Test-Path $extractDir) { Remove-Item -Recurse -Force $extractDir }
Expand-Archive -Path $releaseZip -DestinationPath $extractDir -Force

$exe = Get-ChildItem -Path $extractDir -Recurse -Filter "whisper-cli.exe" | Select-Object -First 1
if (-not $exe) {
    $exe = Get-ChildItem -Path $extractDir -Recurse -Filter "main.exe" | Select-Object -First 1
}

if (-not $exe) {
    throw "[Whisper] Pre-built archive extracted, but whisper-cli.exe/main.exe not found."
}

Copy-Item $exe.FullName "$OutputDir\whisper-cli.exe" -Force
Write-Host "[Whisper] Installed pre-built whisper-cli.exe to $OutputDir"

# Copy any required DLLs next to the exe
Get-ChildItem -Path $extractDir -Recurse -Filter "*.dll" | ForEach-Object {
    Copy-Item $_.FullName "$OutputDir\$($_.Name)" -Force
}

# Cleanup
Remove-Item -Recurse -Force $extractDir -ErrorAction SilentlyContinue

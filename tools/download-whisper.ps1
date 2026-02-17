# Build whisper-cli from source using CMake + MSVC
param(
    [string]$OutputDir = "$PSScriptRoot\..\compile\ClipNotes-win-x64\tools"
)

$ErrorActionPreference = "Stop"

$cacheDir = "$PSScriptRoot\.cache"
$whisperDir = "$cacheDir\whisper.cpp"
$whisperTag = "v1.7.4"

if (-not (Test-Path $OutputDir)) { New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null }
if (-not (Test-Path $cacheDir)) { New-Item -ItemType Directory -Path $cacheDir -Force | Out-Null }

# Check if already built
if (Test-Path "$OutputDir\whisper-cli.exe") {
    Write-Host "[Whisper] whisper-cli.exe already present, skipping build."
    return
}

# Clone or update whisper.cpp
if (-not (Test-Path $whisperDir)) {
    Write-Host "[Whisper] Cloning whisper.cpp $whisperTag..."
    git clone --depth 1 --branch $whisperTag https://github.com/ggerganov/whisper.cpp.git $whisperDir
} else {
    Write-Host "[Whisper] Using cached whisper.cpp source."
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
        # Try with Ninja or default generator
        Write-Host "[Whisper] VS2022 not found, trying default generator..."
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
    # Try alternate name: main.exe
    $whisperExe = Get-ChildItem -Path $buildDir -Recurse -Filter "main.exe" | Select-Object -First 1
}

if ($whisperExe) {
    Copy-Item $whisperExe.FullName "$OutputDir\whisper-cli.exe" -Force
    Write-Host "[Whisper] Installed whisper-cli.exe to $OutputDir"
} else {
    Write-Host "[Whisper] WARNING: Could not find whisper-cli.exe. You may need to build manually or download a pre-built binary."
    Write-Host "[Whisper] Attempting to download pre-built release..."

    $releaseUrl = "https://github.com/ggerganov/whisper.cpp/releases/download/$whisperTag/whisper-bin-x64.zip"
    $releaseZip = "$cacheDir\whisper-bin.zip"

    try {
        Invoke-WebRequest -Uri $releaseUrl -OutFile $releaseZip -UseBasicParsing
        $extractDir = "$cacheDir\whisper-bin-extract"
        Expand-Archive -Path $releaseZip -DestinationPath $extractDir -Force

        $exe = Get-ChildItem -Path $extractDir -Recurse -Filter "whisper-cli.exe" | Select-Object -First 1
        if (-not $exe) {
            $exe = Get-ChildItem -Path $extractDir -Recurse -Filter "main.exe" | Select-Object -First 1
        }
        if ($exe) {
            Copy-Item $exe.FullName "$OutputDir\whisper-cli.exe" -Force
            Write-Host "[Whisper] Installed pre-built whisper-cli.exe to $OutputDir"
        }

        # Copy any required DLLs
        Get-ChildItem -Path $extractDir -Recurse -Filter "*.dll" | ForEach-Object {
            Copy-Item $_.FullName "$OutputDir\$($_.Name)" -Force
        }

        Remove-Item -Recurse -Force $extractDir -ErrorAction SilentlyContinue
    } catch {
        Write-Host "[Whisper] Failed to download pre-built binary: $_"
        Write-Host "[Whisper] Please build whisper-cli manually and place it in $OutputDir"
    }
}

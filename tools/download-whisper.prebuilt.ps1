<# 
download-whisper.prebuilt.ps1
- Downloads prebuilt whisper.cpp Windows binaries (no CMake / no Visual Studio required)
- Extracts them into ..\compile\ClipNotes-win-x64\tools (same layout as your other scripts)
- Adds small compatibility shims (creates main.exe if binaries were renamed in the release)

Usage:
  powershell -NoProfile -ExecutionPolicy Bypass -File .\download-whisper.prebuilt.ps1
  powershell -NoProfile -ExecutionPolicy Bypass -File .\download-whisper.prebuilt.ps1 -WhisperTag v1.8.3 -Variant blas

#>

[CmdletBinding()]
param(
  [string]$WhisperTag = "v1.7.4",
  [ValidateSet("bin","blas")]
  [string]$Variant = "bin",
  [switch]$Force
)

$ErrorActionPreference = "Stop"

# Ensure TLS 1.2 for older PowerShell/Windows setups
try { [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12 } catch {}

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$OutputDir = Join-Path $ScriptDir "..\compile\ClipNotes-win-x64\tools"
$CacheDir  = Join-Path $ScriptDir ".cache"
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
New-Item -ItemType Directory -Force -Path $CacheDir  | Out-Null

Write-Host "[Whisper] OutputDir : $OutputDir"
Write-Host "[Whisper] CacheDir  : $CacheDir"
Write-Host "[Whisper] Tag       : $WhisperTag"
Write-Host "[Whisper] Variant   : $Variant"

# Determine expected zip name(s)
$zipNames = @()
if ($Variant -eq "blas") {
  $zipNames += "whisper-blas-bin-x64.zip"
  # Some releases may include alternative BLAS naming; keep a fallback
  $zipNames += "whisper-blas-openblas-bin-x64.zip"
} else {
  $zipNames += "whisper-bin-x64.zip"
}

$zipPath = Join-Path $CacheDir ("whisper-{0}-{1}.zip" -f $WhisperTag, $Variant)

function Download-File([string]$Url, [string]$OutFile) {
  Write-Host "[Whisper] Downloading: $Url"
  Invoke-WebRequest -Uri $Url -OutFile $OutFile -UseBasicParsing
}

# Build URL candidates:
# 1) GitHub releases (primary)
# 2) SourceForge mirror (fallback) - sometimes easier behind restrictive networks
$urlCandidates = @()
foreach ($zn in $zipNames) {
  $urlCandidates += "https://github.com/ggml-org/whisper.cpp/releases/download/$WhisperTag/$zn"
}
foreach ($zn in $zipNames) {
  $urlCandidates += "https://sourceforge.net/projects/whisper-cpp.mirror/files/$WhisperTag/$zn/download"
}

$alreadyOk = $false
if (-not $Force -and (Test-Path $OutputDir)) {
  # Heuristic: if any known executable exists, consider it installed
  $known = @("main.exe","whisper-cli.exe","whisper.exe","server.exe","whisper-server.exe")
  foreach ($k in $known) {
    if (Test-Path (Join-Path $OutputDir $k)) { $alreadyOk = $true; break }
  }
}

if ($alreadyOk -and -not $Force) {
  Write-Host "[Whisper] Binaries already present in OutputDir, skipping. Use -Force to re-download."
  exit 0
}

# Download zip (reuse cache unless -Force)
if ((Test-Path $zipPath) -and -not $Force) {
  Write-Host "[Whisper] Using cached zip: $zipPath"
} else {
  if (Test-Path $zipPath) { Remove-Item $zipPath -Force -ErrorAction SilentlyContinue }
  $downloaded = $false
  foreach ($u in $urlCandidates) {
    try {
      Download-File -Url $u -OutFile $zipPath
      $downloaded = $true
      break
    } catch {
      Write-Host "[Whisper] Failed: $u"
    }
  }
  if (-not $downloaded) {
    throw "[Whisper] Could not download a prebuilt zip for tag '$WhisperTag'. Try another tag (e.g. v1.8.3) or check network/proxy."
  }
}

# Clean output dir (but keep other tools if any)
# We'll only remove previously extracted whisper-related files if -Force, otherwise just extract over.
if ($Force) {
  Write-Host "[Whisper] Force enabled: removing old whisper binaries (best-effort)..."
  $patterns = @("main.exe","whisper*.exe","*.dll","*.pdb","LICENSE*","README*","models","samples")
  foreach ($p in $patterns) {
    Get-ChildItem -Path $OutputDir -Filter $p -ErrorAction SilentlyContinue | Remove-Item -Force -Recurse -ErrorAction SilentlyContinue
  }
}

Write-Host "[Whisper] Extracting zip to OutputDir..."
Expand-Archive -Path $zipPath -DestinationPath $OutputDir -Force

# Some zips unpack into a nested folder; if so, flatten one level
$subDirs = Get-ChildItem -Path $OutputDir -Directory -ErrorAction SilentlyContinue
if ($subDirs.Count -eq 1) {
  $only = $subDirs[0].FullName
  # If the only directory contains executables, move contents up
  $exeInside = Get-ChildItem -Path $only -Filter "*.exe" -ErrorAction SilentlyContinue
  if ($exeInside.Count -gt 0) {
    Write-Host "[Whisper] Flattening nested folder: $only"
    Get-ChildItem -Path $only -Force | ForEach-Object {
      Move-Item -Path $_.FullName -Destination $OutputDir -Force
    }
    Remove-Item -Path $only -Force -Recurse
  }
}

# Compatibility shim: ensure main.exe exists (older scripts/tools expect it)
$mainPath = Join-Path $OutputDir "main.exe"
if (-not (Test-Path $mainPath)) {
  $candidates = @(
    Join-Path $OutputDir "whisper-cli.exe",
    Join-Path $OutputDir "whisper.exe"
  ) | Where-Object { Test-Path $_ }

  if ($candidates.Count -gt 0) {
    Write-Host "[Whisper] main.exe not found. Creating alias main.exe -> $(Split-Path -Leaf $candidates[0])"
    Copy-Item -Path $candidates[0] -Destination $mainPath -Force
  } else {
    # Last resort: pick any exe
    $anyExe = Get-ChildItem -Path $OutputDir -Filter "*.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($anyExe) {
      Write-Host "[Whisper] main.exe not found. Creating alias main.exe -> $($anyExe.Name)"
      Copy-Item -Path $anyExe.FullName -Destination $mainPath -Force
    }
  }
}

# Quick sanity check
$exeList = Get-ChildItem -Path $OutputDir -Filter "*.exe" -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Name
Write-Host "[Whisper] EXEs in OutputDir: $($exeList -join ', ')"

if (-not (Test-Path $mainPath)) {
  throw "[Whisper] Extraction completed but main.exe still not present. Check zip contents in OutputDir."
}

Write-Host "[Whisper] Done."

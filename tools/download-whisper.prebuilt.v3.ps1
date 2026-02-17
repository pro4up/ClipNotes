param(
  [ValidateSet("auto","bin","blas")]
  [string]$Variant = "auto",

  [string]$Tag = "latest",

  [switch]$Force
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

function Write-Info($msg){ Write-Host $msg }

# --- Paths (keep consistent with your other tools scripts) ---
$OutputDir = Join-Path $PSScriptRoot "..\compile\ClipNotes-win-x64\tools"
$CacheDir  = Join-Path $PSScriptRoot ".cache"
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
New-Item -ItemType Directory -Force -Path $CacheDir  | Out-Null

Write-Info "[Whisper] OutputDir : $OutputDir"
Write-Info "[Whisper] CacheDir  : $CacheDir"
Write-Info "[Whisper] Tag       : $Tag"
Write-Info "[Whisper] Variant   : $Variant"

# If already present and not forcing, skip (supports common names)
$existingExe = @(
  (Join-Path $OutputDir "main.exe"),
  (Join-Path $OutputDir "whisper-cli.exe"),
  (Join-Path $OutputDir "whisper.exe")
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if ($existingExe -and (-not $Force)) {
  Write-Info "[Whisper] Already present: $existingExe (use -Force to redownload)"
  exit 0
}

# --- GitHub API fetch ---
$api =
  if ($Tag -eq "latest") { "https://api.github.com/repos/ggml-org/whisper.cpp/releases/latest" }
  else { "https://api.github.com/repos/ggml-org/whisper.cpp/releases/tags/$Tag" }

Write-Info "[Whisper] GitHub API: $api"

$headers = @{
  "User-Agent" = "powershell"
  "Accept"     = "application/vnd.github+json"
}

try {
  $rel = Invoke-RestMethod -Uri $api -Headers $headers
} catch {
  throw "[Whisper] Failed to query GitHub API. If you're behind a proxy/VPN, try setting HTTPS_PROXY / HTTP_PROXY. Details: $($_.Exception.Message)"
}

# --- Asset selection ---
# We want Windows ZIPs, prefer x64/Win64, avoid Win32 unless nothing else exists.
$assets = @($rel.assets) | Where-Object {
  $_.name -match '\.zip$' -and $_.name -match 'win'  # windows-ish
}

if (-not $assets -or $assets.Count -eq 0) {
  throw "[Whisper] No zip assets found in release $($rel.tag_name)."
}

# Normalize preference by variant
$variantPattern =
  switch ($Variant) {
    "blas" { 'blas' }
    "bin"  { '(?<!blas-)bin' } # 'bin' not preceded by 'blas-' (best effort)
    default { 'blas|bin' }
  }

# Prefer x64/Win64, but allow others as fallback
$preferred = $assets | Where-Object { $_.name -match $variantPattern } | Sort-Object -Property name
$preferredX64 = $preferred | Where-Object { $_.name -match '(x64|win64|amd64)' -and $_.name -notmatch 'win32' }
$preferredNotWin32 = $preferred | Where-Object { $_.name -notmatch 'win32' }
$preferredWin32 = $preferred | Where-Object { $_.name -match 'win32' }

$chosen =
  ($preferredX64 | Select-Object -First 1) ??
  ($preferredNotWin32 | Select-Object -First 1) ??
  ($preferredWin32 | Select-Object -First 1)

if (-not $chosen) {
  # last resort: just take any windows zip
  $chosen = $assets | Select-Object -First 1
}

Write-Info "[Whisper] Release    : $($rel.tag_name)"
Write-Info "[Whisper] Using asset: $($chosen.name)"

$zipPath = Join-Path $CacheDir $chosen.name

# --- Download ---
Write-Info "[Whisper] Downloading: $($chosen.browser_download_url)"
Invoke-WebRequest -Uri $chosen.browser_download_url -OutFile $zipPath -Headers $headers -MaximumRedirection 5

# --- Validate ZIP signature (should be PK...) ---
$bytes = [System.IO.File]::ReadAllBytes($zipPath)
if ($bytes.Length -lt 4) { throw "[Whisper] Downloaded file too small to be a ZIP." }

# ZIP local file header: 50 4B 03 04 ; empty archives may start with 50 4B 05 06
$h0=$bytes[0]; $h1=$bytes[1]; $h2=$bytes[2]; $h3=$bytes[3]
$hex = ('{0:X2} {1:X2} {2:X2} {3:X2}' -f $h0,$h1,$h2,$h3)
$pk  = ($h0 -eq 0x50 -and $h1 -eq 0x4B)
if (-not $pk) {
  throw "[Whisper] Downloaded file does not look like a ZIP (first bytes: $hex). Likely HTML/proxy/captive portal."
}
Write-Info "[Whisper] ZIP signature OK (first bytes: $hex)"

# --- Extract ---
Write-Info "[Whisper] Extracting zip to OutputDir..."
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
Expand-Archive -Path $zipPath -DestinationPath $OutputDir -Force

# --- Provide compatibility exe name (main.exe) ---
$main = Join-Path $OutputDir "main.exe"
if (-not (Test-Path $main)) {
  $candidates = @(
    (Join-Path $OutputDir "whisper-cli.exe"),
    (Join-Path $OutputDir "whisper.exe")
  ) | Where-Object { Test-Path $_ }

  if ($candidates.Count -gt 0) {
    Copy-Item -Force -Path $candidates[0] -Destination $main
    Write-Info "[Whisper] Created compatibility main.exe from $([System.IO.Path]::GetFileName($candidates[0]))"
  } else {
    Write-Info "[Whisper] Warning: No whisper executable found after extraction."
  }
}

Write-Info "[Whisper] Done."

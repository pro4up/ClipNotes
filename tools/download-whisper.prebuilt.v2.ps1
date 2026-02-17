param(
  [string]$Tag = "latest",
  [ValidateSet("auto","bin","blas")]
  [string]$Variant = "auto",
  [switch]$Force
)

$ErrorActionPreference = "Stop"

# Ensure TLS1.2 for older PowerShell/.NET
try { [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12 } catch {}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$outputDir  = Join-Path $scriptRoot "..\compile\ClipNotes-win-x64\tools"
$cacheDir   = Join-Path $scriptRoot ".cache"
$tempDir    = Join-Path $cacheDir "whisper-prebuilt-extract"
$zipPath    = Join-Path $cacheDir "whisper-prebuilt.zip"

Write-Host "[Whisper] OutputDir : $outputDir"
Write-Host "[Whisper] CacheDir  : $cacheDir"
Write-Host "[Whisper] Tag       : $Tag"
Write-Host "[Whisper] Variant   : $Variant"

New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
New-Item -ItemType Directory -Force -Path $cacheDir | Out-Null

function Get-ReleaseJson {
  param([string]$tag)
  $headers = @{ "User-Agent" = "PowerShell" }
  if ($tag -eq "latest") {
    $url = "https://api.github.com/repos/ggml-org/whisper.cpp/releases/latest"
  } else {
    $url = "https://api.github.com/repos/ggml-org/whisper.cpp/releases/tags/$tag"
  }
  Write-Host "[Whisper] GitHub API: $url"
  return Invoke-RestMethod -Uri $url -Headers $headers
}

function Choose-Asset {
  param($release, [string]$variant)

  $assets = @($release.assets)
  if (-not $assets -or $assets.Count -eq 0) {
    throw "[Whisper] No assets found in release. Maybe GitHub rate limit or tag doesn't exist."
  }

  # Prefer Windows x64 zip assets, naming varies across releases.
  $candidates = $assets | Where-Object {
    $_.name -match '\.zip$' -and $_.name -match 'x64|amd64|win' 
  }

  if ($variant -eq "blas") {
    $candidates = $candidates | Where-Object { $_.name -match 'blas' }
  } elseif ($variant -eq "bin") {
    $candidates = $candidates | Where-Object { $_.name -notmatch 'blas' }
  }

  # Common names seen: whisper-bin-x64.zip, whisper-blas-bin-x64.zip, whisper-win-x64.zip, etc.
  # Rank by "bin" then by "blas" depending on variant.
  $ranked = $candidates | Sort-Object -Property @{
    Expression = {
      $n = $_.name.ToLower()
      $score = 0
      if ($n -match 'whisper') { $score += 10 }
      if ($n -match 'bin') { $score += 5 }
      if ($n -match 'windows|win') { $score += 3 }
      if ($n -match 'x64|amd64') { $score += 2 }
      if ($n -match 'blas') { $score += 1 }
      # If user wants blas, invert preference
      if ($variant -eq "blas") { if ($n -match 'blas') { $score += 20 } }
      if ($variant -eq "bin")  { if ($n -notmatch 'blas') { $score += 20 } }
      return -$score  # negative so Sort-Object ascending puts highest score first
    }
  }

  $asset = $ranked | Select-Object -First 1
  if (-not $asset) {
    $names = ($assets | Select-Object -ExpandProperty name) -join ", "
    throw "[Whisper] Couldn't find a suitable .zip asset for Windows x64 in release. Assets: $names"
  }
  return $asset
}

function Assert-ZipFile {
  param([string]$path)
  if (-not (Test-Path $path)) { throw "[Whisper] ZIP not found at $path" }
  $fs = [System.IO.File]::OpenRead($path)
  try {
    $buf = New-Object byte[] 4
    $read = $fs.Read($buf,0,4)
    $sig = [System.Text.Encoding]::ASCII.GetString($buf,0,$read)
    if ($sig -ne "PK`x03`x04" -and $sig -ne "PK`x05`x06" -and $sig -ne "PK`x07`x08") {
      # show first bytes hex for debugging
      $hex = ($buf | ForEach-Object { $_.ToString("X2") }) -join " "
      throw "[Whisper] Downloaded file is not a ZIP (signature=$sig hex=$hex). Likely an HTML error page or redirect."
    }
  } finally { $fs.Close() }
}

function Flatten-Copy {
  param([string]$fromDir, [string]$toDir)

  # Copy all files from extracted tree to outputDir (keep relative structure, but many releases already have flat layout)
  $items = Get-ChildItem -Path $fromDir -Recurse -Force -File
  foreach ($f in $items) {
    $rel = $f.FullName.Substring($fromDir.Length).TrimStart("\","/")
    $dest = Join-Path $toDir $rel
    $destParent = Split-Path -Parent $dest
    New-Item -ItemType Directory -Force -Path $destParent | Out-Null
    Copy-Item -Force -Path $f.FullName -Destination $dest
  }
}

# Skip if already present and not forcing
$existingExe = Get-ChildItem -Path $outputDir -Recurse -File -ErrorAction SilentlyContinue |
  Where-Object { $_.Name -in @("whisper-cli.exe","whisper.exe","main.exe") } |
  Select-Object -First 1

if ($existingExe -and -not $Force) {
  Write-Host "[Whisper] Found existing $($existingExe.Name) at $($existingExe.FullName). Use -Force to redownload."
  exit 0
}

# Fetch release info + choose asset
$release = Get-ReleaseJson -tag $Tag
if ($Tag -eq "latest") { $Tag = $release.tag_name }
$asset = Choose-Asset -release $release -variant $Variant
Write-Host "[Whisper] Using asset: $($asset.name)"
Write-Host "[Whisper] Downloading: $($asset.browser_download_url)"

# Download
Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $zipPath -Headers @{ "User-Agent"="PowerShell" } -MaximumRedirection 10
Assert-ZipFile -path $zipPath

# Extract
if (Test-Path $tempDir) { Remove-Item -Recurse -Force $tempDir }
New-Item -ItemType Directory -Force -Path $tempDir | Out-Null

Write-Host "[Whisper] Extracting..."
Expand-Archive -Path $zipPath -DestinationPath $tempDir -Force

Write-Host "[Whisper] Installing to OutputDir..."
# Some zips contain a single top folder; detect and flatten from there
$top = Get-ChildItem -Path $tempDir -Force | Select-Object -First 1
if ($top -and $top.PSIsContainer -and (Get-ChildItem -Path $tempDir | Measure-Object).Count -eq 1) {
  Flatten-Copy -fromDir $top.FullName -toDir $outputDir
} else {
  Flatten-Copy -fromDir $tempDir -toDir $outputDir
}

# Compatibility: ensure main.exe exists
$cli = Get-ChildItem -Path $outputDir -Recurse -File -ErrorAction SilentlyContinue |
  Where-Object { $_.Name -in @("whisper-cli.exe","whisper.exe") } | Select-Object -First 1
$main = Get-ChildItem -Path $outputDir -Recurse -File -ErrorAction SilentlyContinue |
  Where-Object { $_.Name -eq "main.exe" } | Select-Object -First 1

if (-not $main -and $cli) {
  $mainPath = Join-Path $cli.DirectoryName "main.exe"
  Copy-Item -Force -Path $cli.FullName -Destination $mainPath
  Write-Host "[Whisper] Created compatibility main.exe -> $($cli.Name)"
}

Write-Host "[Whisper] Done. Tag=$Tag"

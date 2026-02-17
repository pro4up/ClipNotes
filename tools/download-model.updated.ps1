# Download whisper.cpp GGML model from Hugging Face
param(
    [string]$ModelName = "large-v3-turbo",
    [string]$OutputDir = "$PSScriptRoot\..\compile\ClipNotes-win-x64\models"
)

$ErrorActionPreference = "Stop"

# Ensure TLS 1.2 for older Windows / PS
try { [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12 } catch {}

if (-not (Test-Path $OutputDir)) { New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null }

$fileName = "ggml-$ModelName.bin"
$outputPath = "$OutputDir\$fileName"

Write-Host "[Model] OutputDir: $OutputDir"
Write-Host "[Model] Output   : $outputPath"

if (Test-Path $outputPath) {
    Write-Host "[Model] $fileName already present, skipping download."
    return
}

$url = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/$fileName"

Write-Host "[Model] Downloading $fileName from Hugging Face..."
Write-Host "[Model] URL: $url"
Write-Host "[Model] This may take several minutes for large models..."

# Prefer WebClient (more tolerant), fallback to Invoke-WebRequest
try {
    $webClient = New-Object System.Net.WebClient
    $webClient.DownloadFile($url, $outputPath)
} catch {
    Write-Host "[Model] WebClient failed, trying Invoke-WebRequest..."
    Invoke-WebRequest -Uri $url -OutFile $outputPath -UseBasicParsing
} finally {
    if ($webClient) { $webClient.Dispose() }
}

if (-not (Test-Path $outputPath)) {
    throw "[Model] Download did not produce an output file: $outputPath"
}

$size = (Get-Item $outputPath).Length / 1MB
Write-Host ("[Model] Downloaded {0} ({1:N0} MB)" -f $fileName, $size)

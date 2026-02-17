# Download whisper.cpp GGML model from Hugging Face
param(
    [string]$ModelName = "large-v3-turbo",
    [string]$OutputDir = "$PSScriptRoot\..\compile\ClipNotes-win-x64\models"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $OutputDir)) { New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null }

$fileName = "ggml-$ModelName.bin"
$outputPath = "$OutputDir\$fileName"

if (Test-Path $outputPath) {
    Write-Host "[Model] $fileName already present, skipping download."
    return
}

$url = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/$fileName"

Write-Host "[Model] Downloading $fileName from Hugging Face..."
Write-Host "[Model] URL: $url"
Write-Host "[Model] This may take several minutes for large models..."

# Use .NET WebClient for better progress on large files
$webClient = New-Object System.Net.WebClient
try {
    $webClient.DownloadFile($url, $outputPath)
    $size = (Get-Item $outputPath).Length / 1MB
    Write-Host "[Model] Downloaded $fileName ({0:N0} MB)" -f $size
} catch {
    Write-Host "[Model] Failed to download: $_"
    Write-Host "[Model] You can manually download from: $url"
    Write-Host "[Model] Place it in: $OutputDir"
    if (Test-Path $outputPath) { Remove-Item $outputPath }
    throw
} finally {
    $webClient.Dispose()
}

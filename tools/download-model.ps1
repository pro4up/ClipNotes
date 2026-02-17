# Download whisper.cpp GGML model from Hugging Face
param(
    [string]$ModelName = "large-v3-turbo",
    [string]$OutputDir = "$PSScriptRoot\..\compile\ClipNotes-win-x64\models"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $OutputDir)) { New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null }

$fileName = "ggml-$ModelName.bin"
$outputPath = "$OutputDir\$fileName"
$url = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/$fileName"

# Check if file exists and has the correct size (verify complete download)
if (Test-Path $outputPath) {
    $localSize = (Get-Item $outputPath).Length
    try {
        $req = [System.Net.WebRequest]::Create($url)
        $req.Method = "HEAD"
        $req.Timeout = 10000
        $resp = $req.GetResponse()
        $remoteSize = $resp.ContentLength
        $resp.Close()

        if ($remoteSize -gt 0 -and $localSize -ge $remoteSize) {
            Write-Host "[Model] $fileName already present and complete ($([int]($localSize/1MB)) MB), skipping download."
            return
        } else {
            Write-Host "[Model] $fileName exists but is incomplete ($([int]($localSize/1MB)) MB / $([int]($remoteSize/1MB)) MB expected). Re-downloading..."
            Remove-Item $outputPath -Force
        }
    } catch {
        # Can't check remote size — skip re-download if file seems reasonably large (>100MB)
        if ($localSize -gt 100MB) {
            Write-Host "[Model] $fileName already present ($([int]($localSize/1MB)) MB), skipping download."
            return
        }
        Write-Host "[Model] $fileName is very small, re-downloading..."
        Remove-Item $outputPath -Force
    }
}

Write-Host "[Model] Downloading $fileName from Hugging Face..."
Write-Host "[Model] URL: $url"
Write-Host "[Model] This may take several minutes for large models..."

# Use .NET WebClient for better progress on large files
$webClient = New-Object System.Net.WebClient
try {
    $webClient.DownloadFile($url, $outputPath)
    $size = (Get-Item $outputPath).Length / 1MB
    Write-Host "[Model] Downloaded $fileName ($([int]$size) MB)"
} catch {
    Write-Host "[Model] Failed to download: $_"
    Write-Host "[Model] You can manually download from: $url"
    Write-Host "[Model] Place it in: $OutputDir"
    if (Test-Path $outputPath) { Remove-Item $outputPath }
    throw
} finally {
    $webClient.Dispose()
}

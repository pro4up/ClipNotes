param([switch]$DryRun)

Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public class CredMgr {
    [DllImport("advapi32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
    public static extern bool CredRead(string target, int type, int reserved, out IntPtr cred);
    [DllImport("advapi32.dll")]
    public static extern void CredFree(IntPtr buf);
    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)]
    public struct CRED {
        public int Flags, Type;
        public string TargetName, Comment;
        public long LastWritten;
        public int BlobSize;
        public IntPtr Blob;
        public int Persist, AttrCount;
        public IntPtr Attrs;
        public string Alias, UserName;
    }
    public static string Get(string target) {
        IntPtr ptr;
        if (!CredRead(target, 1, 0, out ptr)) return null;
        try {
            var c = (CRED)Marshal.PtrToStructure(ptr, typeof(CRED));
            if (c.BlobSize == 0) return null;
            byte[] buf = new byte[c.BlobSize];
            Marshal.Copy(c.Blob, buf, 0, c.BlobSize);
            return Encoding.Unicode.GetString(buf);
        } finally { CredFree(ptr); }
    }
}
'@

$token = [CredMgr]::Get('git:https://github.com')
if (-not $token) { throw "GitHub token not found in Credential Manager" }

$headers = @{
    Authorization = "token $token"
    Accept        = 'application/vnd.github.v3+json'
}
$repo = 'pro4up/ClipNotes'
$setupDir = 'E:\Claude Workstation\Projects\ClipNotes\Setup'

# Файлы для загрузки (без offline)
$uploadFiles = @(
    "$setupDir\ClipNotes-Setup.exe",
    "$setupDir\ClipNotes-bundle.zip",
    "$setupDir\SHA256SUMS.txt"
)

foreach ($f in $uploadFiles) {
    if (-not (Test-Path $f)) { throw "File not found: $f" }
}

# Получить последний релиз
Write-Host "Getting latest release..." -ForegroundColor Yellow
$releases = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases" -Headers $headers
if ($releases.Count -eq 0) { throw "No releases found" }
$release = $releases[0]
Write-Host "  Release: $($release.tag_name) — $($release.name)" -ForegroundColor Cyan
Write-Host "  ID: $($release.id)"

if ($DryRun) {
    Write-Host "[DryRun] Would upload:" -ForegroundColor Magenta
    $uploadFiles | ForEach-Object { Write-Host "  $_" }
    return
}

# Удалить существующие assets (кроме offline)
Write-Host ""
Write-Host "Removing old assets (non-offline)..." -ForegroundColor Yellow
$assetsToRemove = $release.assets | Where-Object {
    $_.name -notlike '*offline*' -and $_.name -notlike '*Offline*'
}
foreach ($asset in $assetsToRemove) {
    Write-Host "  Deleting: $($asset.name)" -ForegroundColor DarkGray
    Invoke-RestMethod -Method Delete `
        -Uri "https://api.github.com/repos/$repo/releases/assets/$($asset.id)" `
        -Headers $headers | Out-Null
}

# Загрузить новые файлы
Write-Host ""
Write-Host "Uploading new assets..." -ForegroundColor Yellow
$uploadUrl = $release.upload_url -replace '\{.*\}', ''

foreach ($filePath in $uploadFiles) {
    $fileName = [System.IO.Path]::GetFileName($filePath)
    $ext = [System.IO.Path]::GetExtension($filePath).ToLower()
    $contentType = switch ($ext) {
        '.exe' { 'application/octet-stream' }
        '.zip' { 'application/zip' }
        '.txt' { 'text/plain' }
        default { 'application/octet-stream' }
    }

    $fileSize = [math]::Round((Get-Item $filePath).Length / 1MB, 1)
    Write-Host "  Uploading $fileName ($fileSize MB)..." -ForegroundColor Cyan

    $uploadHeaders = $headers.Clone()
    $uploadHeaders['Content-Type'] = $contentType

    $bytes = [System.IO.File]::ReadAllBytes($filePath)
    $result = Invoke-RestMethod -Method Post `
        -Uri "${uploadUrl}?name=$([Uri]::EscapeDataString($fileName))" `
        -Headers $uploadHeaders `
        -Body $bytes
    Write-Host "    -> $($result.browser_download_url)" -ForegroundColor Green
}

Write-Host ""
Write-Host "Release updated successfully!" -ForegroundColor Green
Write-Host "https://github.com/$repo/releases/tag/$($release.tag_name)"

param(
    [string]$Version = "1.0.0",
    [string]$Repo    = "pro4up/ClipNotes"
)

$ErrorActionPreference = "Stop"
$setupDir = "$PSScriptRoot\..\..\Setup"

# ── Get token ────────────────────────────────────────────────────────────────
$tmp = [System.IO.Path]::GetTempFileName()
[System.IO.File]::WriteAllText($tmp, "protocol=https`nhost=github.com`n`n", [System.Text.Encoding]::ASCII)
$result = cmd /c "type `"$tmp`" | git credential fill" 2>&1
Remove-Item $tmp -ErrorAction SilentlyContinue
$tokenLine = ($result | Where-Object { $_ -match '^password=' }) | Select-Object -First 1
$token = "$tokenLine" -replace '^password=',''
$token = $token.Trim()
if (-not $token) { throw "Could not get GitHub token from credential manager" }

$headers = @{
    Authorization         = "Bearer $token"
    Accept                = 'application/vnd.github+json'
    'X-GitHub-Api-Version'= '2022-11-28'
}

# ── Check existing release ────────────────────────────────────────────────────
$tagName = "v$Version"
Write-Host "Checking release $tagName on $Repo..." -ForegroundColor Cyan
$releases = Invoke-RestMethod "https://api.github.com/repos/$Repo/releases" -Headers $headers
$existing = $releases | Where-Object { $_.tag_name -eq $tagName } | Select-Object -First 1

if ($existing) {
    Write-Host "Found existing release id=$($existing.id), deleting assets..." -ForegroundColor Yellow
    foreach ($asset in $existing.assets) {
        Write-Host "  Deleting asset: $($asset.name)" -ForegroundColor DarkGray
        Invoke-RestMethod "https://api.github.com/repos/$Repo/releases/assets/$($asset.id)" `
            -Method Delete -Headers $headers | Out-Null
    }
    $releaseId = $existing.id
    # Update release body/title
    $body = @{
        name       = "ClipNotes $tagName"
        body       = "## ClipNotes $tagName`n`n### Изменения`n- In-app updater: проверка обновлений, SHA-256 верификация bundle, EncodedCommand PowerShell`n- Исправления по code-review: FetchStringWithLimitAsync, _loadingSettings guard`n- Unit-тесты UpdateService (20 тестов)`n- Исправление ParseBundleHash для CRLF line endings"
        draft      = $false
        prerelease = $false
    } | ConvertTo-Json
    Invoke-RestMethod "https://api.github.com/repos/$Repo/releases/$releaseId" `
        -Method Patch -Headers $headers -Body $body -ContentType 'application/json' | Out-Null
    Write-Host "Updated release notes." -ForegroundColor Green
} else {
    Write-Host "Creating new release $tagName..." -ForegroundColor Yellow
    # Check/create tag
    try {
        Invoke-RestMethod "https://api.github.com/repos/$Repo/git/refs/tags/$tagName" -Headers $headers | Out-Null
        Write-Host "  Tag $tagName already exists." -ForegroundColor DarkGray
    } catch {
        # Get latest commit SHA
        $mainRef = Invoke-RestMethod "https://api.github.com/repos/$Repo/git/refs/heads/master" -Headers $headers
        $sha = $mainRef.object.sha
        $tagBody = @{ ref = "refs/tags/$tagName"; sha = $sha } | ConvertTo-Json
        Invoke-RestMethod "https://api.github.com/repos/$Repo/git/refs" `
            -Method Post -Headers $headers -Body $tagBody -ContentType 'application/json' | Out-Null
        Write-Host "  Created tag $tagName at $sha" -ForegroundColor Green
    }
    $releaseBody = @{
        tag_name   = $tagName
        name       = "ClipNotes $tagName"
        body       = "## ClipNotes $tagName`n`n### Изменения`n- In-app updater: проверка обновлений, SHA-256 верификация bundle, EncodedCommand PowerShell`n- Исправления по code-review: FetchStringWithLimitAsync, _loadingSettings guard`n- Unit-тесты UpdateService (20 тестов)`n- Исправление ParseBundleHash для CRLF line endings"
        draft      = $false
        prerelease = $false
    } | ConvertTo-Json
    $release = Invoke-RestMethod "https://api.github.com/repos/$Repo/releases" `
        -Method Post -Headers $headers -Body $releaseBody -ContentType 'application/json'
    $releaseId = $release.id
    Write-Host "  Created release id=$releaseId" -ForegroundColor Green
}

# ── Upload assets ────────────────────────────────────────────────────────────
$assets = @(
    @{ path = "$setupDir\ClipNotes-Setup.exe";    name = "ClipNotes-Setup.exe" },
    @{ path = "$setupDir\ClipNotes-bundle.zip";    name = "ClipNotes-bundle.zip" },
    @{ path = "$setupDir\SHA256SUMS.txt";          name = "SHA256SUMS.txt" }
)

$uploadHeaders = @{
    Authorization         = "Bearer $token"
    'X-GitHub-Api-Version'= '2022-11-28'
}

foreach ($asset in $assets) {
    $assetPath = $asset.path
    $assetName = $asset.name
    if (-not (Test-Path $assetPath)) {
        Write-Host "  [skip] $assetName not found" -ForegroundColor DarkGray
        continue
    }
    $size = [math]::Round((Get-Item $assetPath).Length / 1MB, 1)
    Write-Host "Uploading $assetName ($size MB)..." -ForegroundColor Yellow
    $uploadUrl = "https://uploads.github.com/repos/$Repo/releases/$releaseId/assets?name=$assetName"
    $fileBytes = [System.IO.File]::ReadAllBytes($assetPath)
    Invoke-RestMethod $uploadUrl -Method Post -Headers $uploadHeaders `
        -Body $fileBytes -ContentType 'application/octet-stream' | Out-Null
    Write-Host "  [OK] $assetName" -ForegroundColor Green
}

Write-Host ""
Write-Host "Release published: https://github.com/$Repo/releases/tag/$tagName" -ForegroundColor Cyan

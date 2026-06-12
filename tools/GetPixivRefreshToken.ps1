#Requires -Version 5.1
<#
.SYNOPSIS
    One-time Pixiv OAuth helper for Ez2Lazer. Writes refresh_token to pixiv_auth.json.

.DESCRIPTION
    Opens the official Pixiv login page in your system browser (PKCE).
    After login, copy the "code" query parameter from the pixiv:// redirect URL
    and paste it here. The script exchanges it for a refresh_token via Pixiv's
    official oauth.secure.pixiv.net endpoint.

.PARAMETER DataPath
    osu data directory (same folder as client.realm). Defaults to %AppData%\osu.

.EXAMPLE
    .\GetPixivRefreshToken.ps1
    .\GetPixivRefreshToken.ps1 -DataPath "D:\osu"
#>
[CmdletBinding()]
param(
    [string]$DataPath = (Join-Path $env:APPDATA 'osu')
)

$ErrorActionPreference = 'Stop'

$clientId = 'MOBrBDS8blbauo1uch9Z4AXbbf'
$clientSecret = 'ttIDt8NdJJMxTCWRMTtPArt'
$redirectUri = 'https://app-api.pixiv.net/web/v1/users/auth/pixiv/callback'
$tokenUrl = 'https://oauth.secure.pixiv.net/auth/token'
$userAgent = 'PixivAndroidApp/5.0.234 (Android 11; Pixel 5)'

function New-RandomUrlSafeString {
    param([int]$ByteCount = 32)
    $bytes = New-Object byte[] $ByteCount
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    [Convert]::ToBase64String($bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
}

function Get-Sha256Base64Url {
    param([string]$Text)
    $bytes = [System.Text.Encoding]::ASCII.GetBytes($Text)
    $hash = [System.Security.Cryptography.SHA256]::Create().ComputeHash($bytes)
    [Convert]::ToBase64String($hash).TrimEnd('=').Replace('+', '-').Replace('/', '_')
}

function Invoke-PixivTokenRequest {
    param([hashtable]$Body)

    $pairs = foreach ($key in $Body.Keys) {
        '{0}={1}' -f [uri]::EscapeDataString($key), [uri]::EscapeDataString([string]$Body[$key])
    }
    $payload = $pairs -join '&'

    return Invoke-RestMethod -Uri $tokenUrl -Method Post -ContentType 'application/x-www-form-urlencoded' `
        -Headers @{ 'User-Agent' = $userAgent } -Body $payload
}

Write-Host 'Ez2Lazer Pixiv OAuth helper' -ForegroundColor Cyan
Write-Host "Data path: $DataPath"
Write-Host ''

if (-not (Test-Path -LiteralPath $DataPath)) {
    New-Item -ItemType Directory -Path $DataPath | Out-Null
    Write-Host "Created data directory: $DataPath"
}

$codeVerifier = New-RandomUrlSafeString
$codeChallenge = Get-Sha256Base64Url -Text $codeVerifier

$loginUrl = 'https://app-api.pixiv.net/web/v1/login?code_challenge={0}&code_challenge_method=S256&client=pixiv-android' -f $codeChallenge

Write-Host 'Step 1: Opening official Pixiv login page in your browser...' -ForegroundColor Yellow
Start-Process $loginUrl

Write-Host ''
Write-Host 'Step 2: After login, the browser may show a blank page or fail to open pixiv://'
Write-Host 'Copy the full redirect URL (or just the code= value) from the address bar.'
Write-Host 'Example: pixiv://account/login?code=XXXXXXXX&via=login'
Write-Host ''

$rawInput = Read-Host 'Paste redirect URL or authorization code'
if ([string]::IsNullOrWhiteSpace($rawInput)) {
    throw 'No code provided.'
}

$code = $rawInput.Trim()
if ($code -match '(?:\?|&)code=([^&]+)') {
    $code = [uri]::UnescapeDataString($Matches[1])
}

Write-Host ''
Write-Host 'Step 3: Exchanging code for refresh_token...' -ForegroundColor Yellow

$response = Invoke-PixivTokenRequest -Body @{
    client_id     = $clientId
    client_secret = $clientSecret
    grant_type    = 'authorization_code'
    code          = $code
    code_verifier = $codeVerifier
    redirect_uri  = $redirectUri
    include_policy = 'true'
}

if (-not $response.refresh_token) {
    throw "Token response did not include refresh_token.`n$($response | ConvertTo-Json -Depth 4)"
}

$authFile = Join-Path $DataPath 'pixiv_auth.json'
$json = @{ refresh_token = $response.refresh_token } | ConvertTo-Json
Set-Content -LiteralPath $authFile -Value $json -Encoding UTF8

Write-Host ''
Write-Host "Success! Wrote: $authFile" -ForegroundColor Green
if ($response.user) {
    Write-Host ("Logged in as: @{0}" -f $response.user.account)
}
Write-Host ''
Write-Host 'You can now select "Pixiv follow feed" as the menu background source in Ez2Lazer.'

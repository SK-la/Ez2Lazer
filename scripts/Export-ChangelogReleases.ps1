# Builds changelog-releases.json for in-game changelog (release asset; no REST quota on clients).
param(
    [Parameter(Mandatory = $true)][string]$Repository,
    [Parameter(Mandatory = $true)][string]$OutputPath,
    [int]$PerPage = 100,
    [int]$MaxPages = 20
)

$ErrorActionPreference = 'Stop'

$headers = @{
    'Accept'               = 'application/vnd.github+json'
    'X-GitHub-Api-Version' = '2022-11-28'
}

if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_TOKEN)) {
    $headers['Authorization'] = "Bearer $env:GITHUB_TOKEN"
}

$all = [System.Collections.Generic.List[object]]::new()

for ($page = 1; $page -le $MaxPages; $page++) {
    $uri = "https://api.github.com/repos/$Repository/releases?per_page=$PerPage&page=$page"
    $batch = @(Invoke-RestMethod -Method Get -Uri $uri -Headers $headers -TimeoutSec 60)

    if ($batch.Count -eq 0) {
        break
    }

    foreach ($release in $batch) {
        $all.Add([ordered]@{
            tag_name     = $release.tag_name
            body         = $release.body
            published_at = $release.published_at
            prerelease   = [bool]$release.prerelease
            html_url     = $release.html_url
        })
    }

    if ($batch.Count -lt $PerPage) {
        break
    }
}

$payload = [ordered]@{
    fetchedAt = (Get-Date).ToUniversalTime().ToString('o')
    releases  = $all
}

$payload | ConvertTo-Json -Depth 20 | Set-Content -Path $OutputPath -Encoding utf8
Write-Output "Wrote $($all.Count) releases to $OutputPath"
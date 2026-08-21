param(
    [Parameter(Mandatory = $true)]
    [string] $PetsRoot,

    [Parameter(Mandatory = $true)]
    [string] $PackName
)

$ErrorActionPreference = "Stop"

$packDir = Join-Path $PetsRoot $PackName
$live2dDir = Join-Path $packDir "live2d"
$presetsPath = Join-Path $PetsRoot "_official_live2d_presets.json"

if (-not (Test-Path $packDir)) {
    throw "Pack folder not found: $packDir"
}

$entry = Get-ChildItem -Path $live2dDir -Filter "*.model3.json" -File -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $entry) {
    $entry = Get-ChildItem -Path $live2dDir -Filter "*.moc3" -File -ErrorAction SilentlyContinue | Select-Object -First 1
}
if (-not $entry) {
    throw "No .model3.json or .moc3 under $live2dDir"
}

$hash = (Get-FileHash -Path $entry.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
Write-Host "Entry : $($entry.FullName)"
Write-Host "SHA256: $hash"

$map = [ordered]@{}
if (Test-Path $presetsPath) {
    $raw = Get-Content -Path $presetsPath -Raw -Encoding UTF8
    $obj = $raw | ConvertFrom-Json
    foreach ($p in $obj.PSObject.Properties) {
        if ($p.Name.StartsWith("_")) { continue }
        $map[$p.Name] = [string]$p.Value
    }
}

$map[$PackName] = $hash
($map | ConvertTo-Json) | Set-Content -Path $presetsPath -Encoding UTF8
Write-Host "Updated $presetsPath"

# 用 auto-release 发布目录中的 osu.Game.dll（与安装包相同）生成 ez2lazer.Game.nupkg，供平台哈希校验。
param(
    [Parameter(Mandatory = $true)][string]$Tag,
    [Parameter(Mandatory = $true)][string]$ReleaseDir,
    [string]$OutDir = 'artifacts/nuget-pack'
)

$ErrorActionPreference = 'Stop'

function Get-Ez2VersionCore {
    param([Parameter(Mandatory = $true)][string]$Value)
    return ($Value -replace '(-ez2lazer)+$', '')
}

function Normalize-Ez2ReleaseTag {
    param([Parameter(Mandatory = $true)][string]$Value)
    $core = Get-Ez2VersionCore $Value
    if ($core -notmatch '^[0-9]+\.[0-9]+\.[0-9]+$') {
        throw "Invalid release tag '$Value' (expected x.y.z with optional -ez2lazer suffix)."
    }
    return "$core-ez2lazer"
}

$releaseTag = Normalize-Ez2ReleaseTag $Tag
$publishVersion = Get-Ez2VersionCore $releaseTag

$releaseDll = Join-Path $ReleaseDir 'osu.Game.dll'
if (-not (Test-Path $releaseDll)) {
    throw "Release 目录中未找到 osu.Game.dll: $ReleaseDir"
}

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

Write-Host "Pack ez2lazer.Game $releaseTag (AssemblyVersion $publishVersion; deps from Ez2Lazer.Dependencies.props)..."
dotnet pack osu.Game/osu.Game.csproj -c Release `
    /p:Version=$releaseTag `
    /p:OfficialEz2Build=true `
    /p:ReleaseVersion=$publishVersion `
    /p:UseEz2LazerNuGetPackages=true `
    /p:NoWarn=NU5104 `
    /p:IncludeSymbols=false `
    -o $OutDir

$nupkg = Join-Path $OutDir "ez2lazer.Game.$releaseTag.nupkg"
if (-not (Test-Path $nupkg)) {
    throw "未生成 nupkg: $nupkg"
}

Write-Host "Inject release osu.Game.dll into nupkg (byte-identical to installer)..."
$temp = Join-Path $env:RUNNER_TEMP "ez2lazer-game-nupkg-repack"
if (Test-Path $temp) { Remove-Item -Recurse -Force $temp }
New-Item -ItemType Directory -Path $temp | Out-Null

Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::ExtractToDirectory($nupkg, $temp)

$libDir = Join-Path $temp 'lib/net8.0'
if (-not (Test-Path $libDir)) {
    throw "nupkg 中缺少 lib/net8.0 目录"
}

$packedDll = Join-Path $libDir 'osu.Game.dll'
Copy-Item -Path $releaseDll -Destination $packedDll -Force

$sha = (Get-FileHash -Path $packedDll -Algorithm SHA256).Hash
Write-Host "osu.Game.dll SHA256: $sha"

Remove-Item $nupkg -Force
[System.IO.Compression.ZipFile]::CreateFromDirectory($temp, $nupkg)

Write-Host "Created $nupkg (osu.Game.dll matches release build)"

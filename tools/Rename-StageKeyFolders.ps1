# 把 Stage 下的英文键数目录改成 _4key、_8key。已是这个名字的跳过。目标已存在则不覆盖。
# Renames English key folders under Stage to _4key / _8key. Skips names already converted. Does not overwrite an existing target.
param(
    [string] $EzResources
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$words = @{
    one = 1; two = 2; three = 3; four = 4; five = 5; six = 6; seven = 7; eight = 8
    nine = 9; ten = 10; eleven = 11; twelve = 12; thirteen = 13; fourteen = 14
    fifteen = 15; sixteen = 16; seventeen = 17; eighteen = 18
}

function Get-KeyCount([string] $name) {
    if ($name -match '^(?:_)?(\d+)key$') {
        $count = [int]$Matches[1]
        if ($count -ge 1 -and $count -le 18) { return $count }
        return $null
    }

    if ($name -match '^([a-z]+)key$') {
        $word = $Matches[1]
        if ($words.ContainsKey($word)) { return $words[$word] }
    }

    return $null
}

$prompted = [string]::IsNullOrWhiteSpace($EzResources)

if ($prompted) {
    Write-Host "把 fourkey、eightkey 改成 _4key、_8key。已经是这个名字的会跳过，目标已存在则不覆盖。"
    Write-Host "Renames fourkey/eightkey to _4key/_8key. Skips folders already named that way, and will not overwrite an existing target."
    Write-Host ""
}

while ($true) {
    if ($prompted -or [string]::IsNullOrWhiteSpace($EzResources)) {
        $EzResources = Read-Host "请输入 EzResources 目录 / Enter the EzResources directory"
    }

    $EzResources = $EzResources.Trim().Trim('"')

    if ([string]::IsNullOrWhiteSpace($EzResources)) {
        Write-Host "已取消 / Cancelled"
        exit 0
    }

    if (-not (Test-Path -LiteralPath $EzResources -PathType Container)) {
        Write-Host "目录不存在，请重新输入 / Directory not found, enter it again: $EzResources"
        $EzResources = $null
        $prompted = $true
        continue
    }

    $StageRoot = Join-Path $EzResources 'Stage'

    if (-not (Test-Path -LiteralPath $StageRoot -PathType Container)) {
        Write-Host "这里没有 Stage 文件夹，请重新输入 / No Stage folder here, enter it again: $StageRoot"
        $EzResources = $null
        $prompted = $true
        continue
    }

    break
}

$renamed = 0
$skipped = 0

foreach ($theme in @(Get-ChildItem -LiteralPath $StageRoot -Directory)) {
    $stage = Join-Path $theme.FullName 'Stage'
    if (-not (Test-Path -LiteralPath $stage)) { continue }

    foreach ($dir in @(Get-ChildItem -LiteralPath $stage -Directory)) {
        $count = Get-KeyCount $dir.Name
        if ($null -eq $count) { continue }

        $targetName = "_${count}key"
        if ($dir.Name -ceq $targetName) { continue }

        $target = Join-Path $stage $targetName
        if (Test-Path -LiteralPath $target) {
            Write-Warning "skip $($dir.FullName) because $targetName already exists"
            $skipped++
            continue
        }

        Rename-Item -LiteralPath $dir.FullName -NewName $targetName
        Write-Output "$($dir.FullName) -> $targetName"
        $renamed++
    }
}

Write-Output "renamed=$renamed skipped=$skipped"

if ($prompted) {
    Read-Host "完成。按 Enter 关闭 / Done. Press Enter to close" | Out-Null
}

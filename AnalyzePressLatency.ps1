<#
.SYNOPSIS
    分析 Mania 按键延迟探针输出（diagnostics/presslatency_*.csv）。

.DESCRIPTION
    分段定义（全部 wall-clock，ms）：
      PreColumnMs  按键事件入队 -> 本列 OnPressed 入口（输入线程交班 + 等帧 + 派发遍历；列不可控）
      ColumnMs     本列 OnPressed 全程（键音触发 + 列路由 + 判定 + 同步结果扇出；列可控）
      TotalMs      两者之和（= PreColumnMs + ColumnMs，仅为对照保留）
      FrameAgeMs   处理该按键时当前游戏帧的时钟已有多旧（帧陈旧度，不是延迟组成部分）

    判读要点：
      * PreColumnMs 尾部大而 ColumnMs 平稳 -> 帧等待/派发问题，列内优化已到顶
      * ColumnMs 尾部大且与 ForceMissScan/Entries 相关 -> 提前判 miss 扫描值得继续改
      * FscIter>1 的样本 PreColumnMs 明显更大 -> catch-up 是主因
      * Routed=0 的 ColumnMs 不小于 Routed=1 -> 空按不是便宜路径
      * CacheMisses 每按必涨 -> Earliest 缓存没生效

.PARAMETER Path
    要分析的 presslatency CSV。

.PARAMETER ComparePath
    对照 CSV（例如消融 run）。给出后并列输出分段分位数与 routed/unrouted 拆分。

.PARAMETER TopN
    尾部样本对照表的行数，默认 20。

.PARAMETER Skip
    读入后跳过前 N 行（丢弃开局热身样本）。

.EXAMPLE
    ./AnalyzePressLatency.ps1 diagnostics/presslatency_20260924_101500.csv

.EXAMPLE
    # 消融对照：EZ_PRESS_PROBE_SKIP_FORCE_MISS=1 的 run 与基线对比
    ./AnalyzePressLatency.ps1 diagnostics/presslatency_base.csv -ComparePath diagnostics/presslatency_noforcemiss.csv
#>

param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string] $Path,

    [string] $ComparePath,

    [int] $TopN = 20,

    [int] $Skip = 0
)

$ErrorActionPreference = 'Stop'
$inv = [System.Globalization.CultureInfo]::InvariantCulture

function ConvertTo-Double($value) {
    if ([string]::IsNullOrWhiteSpace($value)) { return [double]::NaN }
    return [double]::Parse($value, $inv)
}

function Format-F3($value) {
    if ([double]::IsNaN($value)) { return '      -' }
    return $value.ToString('F3', $inv)
}

function Read-Samples([string] $file) {
    $rows = @(Import-Csv -Path $file)

    if ($Skip -gt 0) {
        $rows = @($rows | Select-Object -Skip $Skip)
    }

    return $rows
}

# 取某列的非 NaN 数值，升序
function Get-Sorted([object[]] $rows, [string] $column) {
    $values = New-Object 'System.Collections.Generic.List[double]'

    foreach ($row in $rows) {
        $v = ConvertTo-Double $row.$column
        if (-not [double]::IsNaN($v)) { $values.Add($v) }
    }

    $array = $values.ToArray()
    [Array]::Sort($array)
    return , $array
}

function Get-Percentile([double[]] $sorted, [double] $p) {
    if ($null -eq $sorted -or $sorted.Length -eq 0) { return [double]::NaN }
    $index = [int][math]::Floor(($sorted.Length - 1) * $p)
    return $sorted[$index]
}

function Write-Stats([object[]] $rows, [string] $label, [string] $column) {
    $sorted = Get-Sorted $rows $column

    if ($sorted.Length -eq 0) {
        '{0,-26} (no data)' -f $label
        return
    }

    '{0,-26} n={1,-7} p50={2,9} p90={3,9} p99={4,9} max={5,10} mean={6,9}' -f `
        $label, $sorted.Length,
        (Format-F3 (Get-Percentile $sorted 0.50)),
        (Format-F3 (Get-Percentile $sorted 0.90)),
        (Format-F3 (Get-Percentile $sorted 0.99)),
        (Format-F3 $sorted[-1]),
        (Format-F3 (($sorted | Measure-Object -Average).Average))
}

function Group-Rows([object[]] $rows, [scriptblock] $keySelector) {
    $buckets = @{}

    foreach ($row in $rows) {
        # 必须走 ForEach-Object -InputObject 才能让键选择器里的 $_ 指向当前行；
        # 直接 & $keySelector $row 只会填 $args，$_ 仍为 $null（会导致所有行落进同一个桶）。
        $key = ForEach-Object -InputObject $row -Process $keySelector
        if (-not $buckets.ContainsKey($key)) { $buckets[$key] = New-Object 'System.Collections.Generic.List[object]' }
        $buckets[$key].Add($row)
    }

    return $buckets
}

# 单调计数器的相邻差值（用于 GC 次数、缓存命中/未命中）
function Get-Delta([object[]] $rows, [string] $column) {
    $deltas = New-Object 'System.Collections.Generic.List[double]'
    $previous = [double]::NaN

    foreach ($row in $rows) {
        $current = ConvertTo-Double $row.$column
        if (-not [double]::IsNaN($previous) -and -not [double]::IsNaN($current)) {
            $deltas.Add($current - $previous)
        }
        $previous = $current
    }

    return , $deltas.ToArray()
}

function Show-Distribution([object[]] $rows, [string] $title, [scriptblock] $keySelector, [string[]] $columns) {
    Write-Host ''
    Write-Host "== $title ==" -ForegroundColor Cyan

    $buckets = Group-Rows $rows $keySelector
    $orderedKeys = @($buckets.Keys | Sort-Object)

    foreach ($key in $orderedKeys) {
        $bucket = $buckets[$key].ToArray()
        Write-Host ("  [{0}] n={1}" -f $key, $bucket.Length)

        foreach ($column in $columns) {
            Write-Stats $bucket ("    $column") $column
        }
    }
}

# ---------------------------------------------------------------- 读入

if (-not (Test-Path -LiteralPath $Path)) {
    throw "找不到文件: $Path"
}

$samples = Read-Samples $Path

if ($samples.Length -eq 0) {
    throw "没有样本: $Path"
}

Write-Host ''
Write-Host "文件: $Path" -ForegroundColor Green
Write-Host ("样本: {0}  谱面时间跨度: {1} -> {2} ms" -f `
        $samples.Length,
        (Format-F3 (ConvertTo-Double $samples[0].GameTime)),
        (Format-F3 (ConvertTo-Double $samples[-1].GameTime)))

$routed = @($samples | Where-Object { $_.Routed -eq '1' })
$judged = @($samples | Where-Object { $_.Judged -eq '1' })

Write-Host ("routed={0} ({1:P1})  judged={2}  emptyPress={3}" -f `
        $routed.Length, ($routed.Length / $samples.Length), $judged.Length, ($samples.Length - $routed.Length))

$frames = @($samples | Group-Object FrameId)
$multiPressFrames = @($frames | Where-Object { $_.Count -gt 1 })

Write-Host ("帧数={0}  其中多键帧={1}  单帧最大按键数={2}" -f `
        $frames.Count, $multiPressFrames.Count, (($frames | Measure-Object Count -Maximum).Maximum))

# ---------------------------------------------------------------- 帧与 stall

Write-Host ''
Write-Host '== 帧耗时与 stall ==' -ForegroundColor Cyan

$frameElapsed = Get-Sorted $samples 'FrameElapsed'

if ($frameElapsed.Length -gt 0) {
    $median = Get-Percentile $frameElapsed 0.5

    Write-Stats $samples 'FrameElapsedMs' 'FrameElapsed'
    Write-Host ("  隐含帧率        p50={0:N0} fps  p90={1:N0} fps" -f (1000.0 / $median), (1000.0 / (Get-Percentile $frameElapsed 0.9)))

    $over2 = @($frameElapsed | Where-Object { $_ -gt 2 })
    $over5 = @($frameElapsed | Where-Object { $_ -gt 5 })
    Write-Host ("  帧耗时>2ms: {0} ({1:P2})   >5ms: {2} ({3:P2})" -f `
            $over2.Length, ($over2.Length / $frameElapsed.Length), $over5.Length, ($over5.Length / $frameElapsed.Length))
}

# 列内条目数分布：真实局内的车道占用（决定列内扫描到底有多长）
Write-Host ''
Write-Host '== 列内条目数（车道占用） ==' -ForegroundColor Cyan

$entryGroups = @($samples | Group-Object Entries | Sort-Object { [int]$_.Name })

foreach ($group in $entryGroups) {
    Write-Host ("  Entries={0,-4} n={1,-6} ({2:P1})" -f $group.Name, $group.Count, ($group.Count / $samples.Length))
}

# ---------------------------------------------------------------- 缓存与 GC

Write-Host ''
Write-Host '== 缓存与 GC ==' -ForegroundColor Cyan

$hits = Get-Delta $samples 'CacheHits'
$misses = Get-Delta $samples 'CacheMisses'
$hitDelta = ($hits | Measure-Object -Sum).Sum
$missDelta = ($misses | Measure-Object -Sum).Sum
$totalLookups = $hitDelta + $missDelta

if ($totalLookups -gt 0) {
    Write-Host ("Earliest 缓存: hits={0} misses={1} 命中率={2:P1}" -f $hitDelta, $missDelta, ($hitDelta / $totalLookups))
    Write-Host '  注意：计数器是进程级，IsHittableEarliest 也由非按键路径（CheckHittable）调用，'
    Write-Host '        因此该命中率不能读成「按键是否命中缓存」。'
} else {
    Write-Host 'Earliest 缓存: 无查询（未走 Earliest 路径？）'
}

$wallSeconds = ((ConvertTo-Double $samples[-1].WallMs) - (ConvertTo-Double $samples[0].WallMs)) / 1000.0

foreach ($generation in 0, 1) {
    $collected = Get-Delta $samples "Gen$generation"
    $total = ($collected | Measure-Object -Sum).Sum

    if ($wallSeconds -gt 0) {
        Write-Host ("gen{0} 回收: {1} ({2:F2}/s)" -f $generation, $total, ($total / $wallSeconds))
    }
}

$gcPause = Get-Delta $samples 'GcPauseMs'
$gcTotal = ($gcPause | Measure-Object -Sum).Sum

if ($wallSeconds -gt 0) {
    Write-Host ("GcPause 累计: {0:F0}ms，占采样跨度 {1:F1}s 的 {2:P2}（含后台 GC，是上界）" -f `
            $gcTotal, $wallSeconds, ($gcTotal / ($wallSeconds * 1000)))
}

$gen0Deltas = Get-Delta $samples 'Gen0'
$gen0Collections = @($gen0Deltas | Where-Object { $_ -gt 0 })

Write-Host ("按键之间发生 gen0 回收的按键占比: {0} / {1}" -f $gen0Collections.Length, $gen0Deltas.Length)

# ---------------------------------------------------------------- 总体

Write-Host ''
Write-Host '== 总体分段（ms） ==' -ForegroundColor Cyan

foreach ($column in 'PreColumnMs', 'ColumnMs', 'TotalMs', 'FrameElapsed') {
    Write-Stats $samples $column $column
}

# ---------------------------------------------------------------- 分组

Show-Distribution $samples 'Routed 拆分' `
    { if ($_.Routed -eq '1') { 'routed=1  ' } else { 'routed=0  ' } } `
    'PreColumnMs', 'ColumnMs'

Show-Distribution $samples 'FrameAge 分桶（帧陈旧度）' `
    {
        $age = ConvertTo-Double $_.FrameAgeMs
        if ([double]::IsNaN($age)) { 'n/a       ' }
        elseif ($age -lt 0) { 'age<0     ' }
        elseif ($age -lt 2) { 'age0-2    ' }
        elseif ($age -lt 5) { 'age2-5    ' }
        elseif ($age -lt 10) { 'age5-10   ' }
        else { 'age>10    ' }
    } `
    'PreColumnMs', 'ColumnMs'

Show-Distribution $samples '同一帧内按键数分桶（批处理效应）' `
    {
        $count = [int]$_.PressesInFrame
        if ($count -le 1) { 'inFrame=1  ' }
        elseif ($count -eq 2) { 'inFrame=2  ' }
        elseif ($count -le 4) { 'inFrame=3-4' }
        else { 'inFrame=5+ ' }
    } `
    'PreColumnMs', 'ColumnMs'

Show-Distribution $samples '列内条目数分桶' `
    {
        $count = [int]$_.Entries
        if ($count -le 4) { 'entries<=4 ' }
        elseif ($count -le 12) { 'entries5-12' }
        elseif ($count -le 24) { 'entries13-24' }
        else { 'entries25+ ' }
    } `
    'PreColumnMs', 'ColumnMs'

Show-Distribution $samples '多遍子树（FSC iterations）' `
    {
        $iterations = [int]$_.FscIter
        if ($iterations -gt 1) { 'multiPass  ' }
        else { 'steady     ' }
    } `
    'PreColumnMs', 'ColumnMs'

# ---------------------------------------------------------------- 尾部对照

function Show-Outliers([object[]] $rows, [string] $column, [int] $count) {
    Write-Host ''
    Write-Host "== $column 尾部 $count 条 ==" -ForegroundColor Cyan

    $sorted = @($rows | Sort-Object -Property @{ Expression = { ConvertTo-Double $_.$column } } -Descending)
    $top = @($sorted | Select-Object -First $count)

    '  {0,10} {1,10} {2,10} {3,8} {4,6} {5,6} {6,6} {7,7} {8,5} {9,6} {10,7} {11,7} {12,10}' -f `
        $column, 'PreCol', 'Column', 'FrameAge', 'Entr', 'Scan', 'Routed', 'inFrame', 'Iter', 'Frame', 'CacheHit', 'CacheMiss', 'GameTime'

    foreach ($row in $top) {
        '  {0,10} {1,10} {2,10} {3,8} {4,6} {5,6} {6,6} {7,7} {8,5} {9,6} {10,7} {11,7} {12,10}' -f `
            (Format-F3 (ConvertTo-Double $row.$column)),
            (Format-F3 (ConvertTo-Double $row.PreColumnMs)),
            (Format-F3 (ConvertTo-Double $row.ColumnMs)),
            (Format-F3 (ConvertTo-Double $row.FrameAgeMs)),
            $row.Entries, $row.ForceMissScan, $row.Routed, $row.PressesInFrame,
            $row.FscIter, $row.FrameId,
            $row.CacheHits, $row.CacheMisses,
            (Format-F3 (ConvertTo-Double $row.GameTime))
    }
}

Show-Outliers $samples 'ColumnMs' $TopN
Show-Outliers $samples 'PreColumnMs' $TopN

# ---------------------------------------------------------------- 消融对照

if ($ComparePath) {
    if (-not (Test-Path -LiteralPath $ComparePath)) {
        throw "找不到对照文件: $ComparePath"
    }

    $compare = Read-Samples $ComparePath

    Write-Host ''
    Write-Host '== 消融对照 ==' -ForegroundColor Cyan
    Write-Host ("  A = {0} (n={1})" -f $Path, $samples.Length)
    Write-Host ("  B = {0} (n={1})" -f $ComparePath, $compare.Length)

    foreach ($column in 'PreColumnMs', 'ColumnMs', 'TotalMs') {
        Write-Host ''
        Write-Host "  -- $column --"

        foreach ($pair in @(@('A', $samples), @('B', $compare))) {
            Write-Stats $pair[1] ("    {0}" -f $pair[0]) $column
        }

        foreach ($pair in @(@('A', $samples), @('B', $compare))) {
            $rows = @($pair[1] | Where-Object { $_.Routed -eq '1' })
            Write-Stats $rows ("    {0} routed  " -f $pair[0]) $column
        }

        foreach ($pair in @(@('A', $samples), @('B', $compare))) {
            $rows = @($pair[1] | Where-Object { $_.Routed -eq '0' })
            Write-Stats $rows ("    {0} unrouted" -f $pair[0]) $column
        }
    }
}

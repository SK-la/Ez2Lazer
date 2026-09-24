<#
.SYNOPSIS
    分析 Mania 帧级 stall 探针输出（diagnostics/framestall_*.csv）。

.DESCRIPTION
    探针挂在 FrameStabilityContainer.UpdateSubTree 末尾（UpdateThread 的帧边界），每帧记一次耗时，
    只有超过阈值（默认 1.5ms，可用 EZ_FRAME_PROBE_MS 覆盖）的帧才写明细行。因此：

      * 本 CSV 里 ElapsedMs 的分布 = **尾部**分布，不是全帧分布（全分布只在日志的 [EzFrameStall] 摘要里）。
      * 每行带该帧的 GcPauseDeltaMs（跨该帧的 GC.GetTotalPauseDuration 增量）与
        ThreadAllocDeltaBytes（update 线程在该帧的分配量）。

    两个判据：

      1. GC 因果：若 ElapsedMs 大的帧里 GcPauseDeltaMs 也接近 ElapsedMs，GC 是原因；
         若比值接近 0，GC 被排除（那 stall 另有来源，需往别处找）。
      2. 按键尾部归因：给出 -PressPath 后，把每个 PreColumnMs 偏大的按键的**等待区间**
         [WallMs - PreColumnMs, WallMs] 与各 stall 帧窗口求交，算覆盖率。
         覆盖率高 = 这次按键延迟确实是被帧 stall 吞掉的；覆盖率低 = 等待发生在帧边界之外
         （输入线程、事件队列、或低于阈值的连续多帧）。

.PARAMETER Path
    framestall CSV。

.PARAMETER PressPath
    presslatency CSV。给出后启用第 2 项归因。

.PARAMETER PressThresholdMs
    只看 PreColumnMs 超过该值的按键，默认 1.0。

.PARAMETER CoverageRatio
    覆盖率不低于该比例才算「被 stall 解释」，默认 0.6。

.PARAMETER TopN
    尾部对照表行数，默认 20。

.EXAMPLE
    ./AnalyzeFrameStall.ps1 diagnostics/framestall_20260924_181500.csv

.EXAMPLE
    ./AnalyzeFrameStall.ps1 diagnostics/framestall_20260924_181500.csv `
        -PressPath diagnostics/presslatency_20260924_181500.csv
#>

param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string] $Path,

    [string] $PressPath,

    [string] $SummaryPath,

    [double] $PressThresholdMs = 1.0,

    [double] $CoverageRatio = 0.6,

    [int] $TopN = 20
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

function Get-Mean([double[]] $values) {
    if ($null -eq $values -or $values.Length -eq 0) { return [double]::NaN }

    $sum = 0.0
    foreach ($v in $values) { $sum += $v }
    return $sum / $values.Length
}

function Get-Percentile([double[]] $sorted, [double] $p) {
    if ($null -eq $sorted -or $sorted.Length -eq 0) { return [double]::NaN }
    return $sorted[[int][math]::Floor(($sorted.Length - 1) * $p)]
}

function Write-Stats([object[]] $rows, [string] $label, [string] $column) {
    $sorted = Get-Sorted $rows $column

    if ($sorted.Length -eq 0) {
        '{0,-30} (no data)' -f $label
        return
    }

    '{0,-30} n={1,-7} p50={2,8} p90={3,8} p99={4,8} max={5,9} mean={6,8}' -f `
        $label, $sorted.Length,
        (Format-F3 (Get-Percentile $sorted 0.50)),
        (Format-F3 (Get-Percentile $sorted 0.90)),
        (Format-F3 (Get-Percentile $sorted 0.99)),
        (Format-F3 $sorted[-1]),
        (Format-F3 (($sorted | Measure-Object -Average).Average))
}

# ---------------------------------------------------------------- 读入

if (-not (Test-Path -LiteralPath $Path)) {
    throw "找不到文件: $Path"
}

$stalls = @(Import-Csv -LiteralPath $Path)

if ($stalls.Length -eq 0) {
    throw "没有 stall 明细：本次运行没有任何一帧超过阈值。这本身是好消息，可调低 EZ_FRAME_PROBE_MS 再看次尖峰。"
}

$firstWall = ConvertTo-Double $stalls[0].WallMs
$lastWall = ConvertTo-Double $stalls[-1].WallMs
$spanSeconds = ($lastWall - $firstWall) / 1000.0

Write-Host ''
Write-Host "文件: $Path" -ForegroundColor Green
Write-Host ("stall 明细: {0} 条，覆盖墙钟 {1:F1}s（约 {2:F2} 次/s）" -f `
        $stalls.Length, $spanSeconds, $(if ($spanSeconds -gt 0) { $stalls.Length / $spanSeconds } else { 0 }))
Write-Host '注意：下面的幅度统计只覆盖超过阈值的帧（即尾部）。'

# ---------------------------------------------------------------- 全帧分布（摘要）

# 全帧直方图只在探针写的摘要文件里；默认取与 CSV 同名的兄弟文件。
if (-not $SummaryPath) {
    $csvFull = (Resolve-Path -LiteralPath $Path).Path
    $candidate = [System.IO.Path]::Combine(
        [System.IO.Path]::GetDirectoryName($csvFull),
        ([System.IO.Path]::GetFileNameWithoutExtension($csvFull) + '.summary.txt'))

    if (Test-Path -LiteralPath $candidate) { $SummaryPath = $candidate }
}

Write-Host ''
Write-Host '== 全帧分布（含未超阈值的帧） ==' -ForegroundColor Cyan

if ($SummaryPath -and (Test-Path -LiteralPath $SummaryPath)) {
    Write-Host "  来源: $SummaryPath"
    Get-Content -LiteralPath $SummaryPath |
        Where-Object { $_ -match '\[EzFrameStall\]' } |
        ForEach-Object { Write-Host "  $_" }

    # 双直方图对比：这是「一次按键把帧拉长了多少」的直接答案。
    $lines = @(Get-Content -LiteralPath $SummaryPath | Where-Object { $_ -match '^(noPress|withPress)' })

    if ($lines.Count -ge 2) {
        Write-Host ''
        Write-Host '  -- 按键帧 vs 非按键帧 --' -ForegroundColor Cyan

        $parsed = @{}
        foreach ($line in $lines) {
            $name = if ($line -match '^noPress') { 'noPress' } else { 'withPress' }

            if ($line -match '\bn=(\d+)') { $parsed[$name + '_n'] = [double]$matches[1] }
            if ($line -match 'mean=([\d.]+)') { $parsed[$name + '_mean'] = [double]::Parse($matches[1], $inv) }
            if ($line -match 'p50=([\d.]+)') { $parsed[$name + '_p50'] = [double]::Parse($matches[1], $inv) }
            if ($line -match 'p90=([\d.]+)') { $parsed[$name + '_p90'] = [double]::Parse($matches[1], $inv) }
            if ($line -match 'p99=([\d.]+)') { $parsed[$name + '_p99'] = [double]::Parse($matches[1], $inv) }
            if ($line -match 'max=([\d.]+)') { $parsed[$name + '_max'] = [double]::Parse($matches[1], $inv) }
            if ($line -match 'over0\.5=(\d+)') { $parsed[$name + '_o05'] = [double]$matches[1] }
            if ($line -match 'over1=(\d+)') { $parsed[$name + '_o1'] = [double]$matches[1] }
            if ($line -match 'over2=(\d+)') { $parsed[$name + '_o2'] = [double]$matches[1] }
            if ($line -match 'over5=(\d+)') { $parsed[$name + '_o5'] = [double]$matches[1] }
        }

        if ($parsed.ContainsKey('noPress_n') -and $parsed['noPress_n'] -gt 0) {
            Write-Host ('    {0,-10} {1,9} {2,8} {3,8} {4,8} {5,9} {6,9} {7,9} {8,9}' -f `
                    '组', 'n', 'mean', 'p50', 'p90', 'P(>1ms)', 'P(>2ms)', 'P(>5ms)', 'max')

            foreach ($name in 'noPress', 'withPress') {
                $n = $parsed[$name + '_n']
                if ($n -le 0) { continue }

                Write-Host ('    {0,-10} {1,9:N0} {2,8} {3,8} {4,8} {5,9:P2} {6,9:P2} {7,9:P2} {8,9}' -f `
                        $name, $n,
                        (Format-F3 $parsed[$name + '_mean']),
                        (Format-F3 $parsed[$name + '_p50']),
                        (Format-F3 $parsed[$name + '_p90']),
                        ($parsed[$name + '_o1'] / $n),
                        ($parsed[$name + '_o2'] / $n),
                        ($parsed[$name + '_o5'] / $n),
                        (Format-F3 $parsed[$name + '_max']))
            }

            if ($parsed.ContainsKey('withPress_n') -and $parsed['withPress_n'] -gt 0) {
                $deltaP50 = $parsed['withPress_p50'] - $parsed['noPress_p50']
                $deltaP90 = $parsed['withPress_p90'] - $parsed['noPress_p90']

                # 归一化后的尾部概率之比才是「按键是否把帧推过阈值」的正确读法。
                $p1no = $parsed['noPress_o1'] / $parsed['noPress_n']
                $p1yes = $parsed['withPress_o1'] / $parsed['withPress_n']
                $ratio = if ($p1no -gt 0) { $p1yes / $p1no } else { [double]::NaN }

                Write-Host ''
                Write-Host ("    按键帧比非按键帧：p50 {0:+#.000;-#.000;0}ms   p90 {1:+#.000;-#.000;0}ms" -f $deltaP50, $deltaP90)
                Write-Host ("    P(>1ms): {0:P2} vs {1:P2}  → {2:F1}x   （按键帧占全帧 {3:P2}）" -f `
                        $p1yes, $p1no, $ratio, ($parsed['withPress_n'] / ($parsed['withPress_n'] + $parsed['noPress_n'])))
                Write-Host '    读法：p50 与 p90 同时抬高 → 按键是稳定地给帧加开销，不是只让尾部变长。'

                # 三段切分（只有新探针会写这行）：帧起→按键进入本列 / 本列工时 / 本列之后。
                $splitLine = @(Get-Content -LiteralPath $SummaryPath | Where-Object { $_ -match '^pressSplit' })
                if ($splitLine.Count -gt 0 -and $splitLine[0] -match 'elapsedMean=([\d.]+)ms firstPressAtMean=([\d.]+)ms pressColumnMean=([\d.]+)ms afterPressMean=(-?[\d.]+)ms inputQueue=(-?\d+)') {
                    $el = [double]::Parse($matches[1], $inv)
                    $fp = [double]::Parse($matches[2], $inv)
                    $pc = [double]::Parse($matches[3], $inv)
                    $ap = [double]::Parse($matches[4], $inv)
                    $iq = [int]$matches[5]

                    Write-Host ''
                    Write-Host '    按键帧的三段切分（均值）:' -ForegroundColor Cyan
                    Write-Host ("      帧起 → 按键进入本列    {0,7}ms   {1,6:P1}   ← 派发机器（输入队列遍历 + 键绑定派发）" -f (Format-F3 $fp), $(if ($el -gt 0) { $fp / $el } else { 0 }))
                    Write-Host ("      本列工时（OnPressed）  {0,7}ms   {1,6:P1}   ← 本列判定 + 同步结果扇出" -f (Format-F3 $pc), $(if ($el -gt 0) { $pc / $el } else { 0 }))
                    Write-Host ("      本列之后 → 帧尾        {0,7}ms   {1,6:P1}   ← 判定落地之后的帧内工作" -f (Format-F3 $ap), $(if ($el -gt 0) { $ap / $el } else { 0 }))
                    Write-Host ("      合计                   {0,7}ms" -f (Format-F3 $el))
                    Write-Host ("      非位置输入队列长度     {0,7}      ← 每次按下/抬起都要重建并 Reverse 的列表规模" -f $iq)
                }
            }
        }
    }
}
else {
    Write-Host '  未找到摘要文件（framestall_*.summary.txt）。全帧分布只能从游戏日志的 [EzFrameStall] 行读。'
}
# ---------------------------------------------------------------- 尾部幅度

Write-Host ''
Write-Host '== stall 幅度（仅超阈值的帧） ==' -ForegroundColor Cyan

foreach ($column in 'ElapsedMs', 'GcPauseDeltaMs') {
    Write-Stats $stalls $column $column
}

foreach ($band in @(@('2-3ms', 2, 3), @('3-5ms', 3, 5), @('5-10ms', 5, 10), @('>10ms', 10, [double]::MaxValue))) {
    $name = $band[0]
    $low = $band[1]
    $high = $band[2]
    $inBand = @($stalls | Where-Object {
            $v = ConvertTo-Double $_.ElapsedMs
            $v -ge $low -and $v -lt $high
        })

    if ($inBand.Length -eq 0) { continue }

    $bandElapsed = Get-Sorted $inBand 'ElapsedMs'
    $bandPause = Get-Sorted $inBand 'GcPauseDeltaMs'
    $meanElapsed = Get-Mean $bandElapsed
    $meanPause = Get-Mean $bandPause

    Write-Host ("  Elapsed {0,-7} n={1,-6} mean={2,8}  meanGcPause={3,8}  GC占比={4,7:P0}" -f `
            $name, $inBand.Length, (Format-F3 $meanElapsed), (Format-F3 $meanPause), $(if ($meanElapsed -gt 0) { $meanPause / $meanElapsed } else { 0 }))
}

# ---------------------------------------------------------------- 按键自身工时 vs 帧长

Write-Host ''
Write-Host '== 按键自身的工时 vs 帧长 ==' -ForegroundColor Cyan

$pressStalls = @($stalls | Where-Object { [int]$_.PressesInFrame -gt 0 -and $_.PressColumnMs })

if ($pressStalls.Count -gt 0) {
    Write-Stats $pressStalls '  PressColumnMs（本列工时）' 'PressColumnMs'
    Write-Stats $pressStalls '  ElapsedMs（该帧总长）' 'ElapsedMs'

    # 按键工时占帧长的比例：比例低说明帧长不是按键自己的工作造成的。
    $ratios = New-Object 'System.Collections.Generic.List[double]'
    foreach ($r in $pressStalls) {
        $e = ConvertTo-Double $r.ElapsedMs
        $c = ConvertTo-Double $r.PressColumnMs
        if ($e -gt 0 -and -not [double]::IsNaN($c)) { $ratios.Add($c / $e) }
    }

    if ($ratios.Count -gt 0) {
        $sorted = $ratios.ToArray(); [Array]::Sort($sorted)
        $sum = 0.0; foreach ($v in $sorted) { $sum += $v }
        Write-Host ("  按键工时 / 帧长: p50={0:P1} p90={1:P1} mean={2:P1}" -f `
                (Get-Percentile $sorted 0.5), (Get-Percentile $sorted 0.9), ($sum / $sorted.Length))
        Write-Host '  占比高 → 帧是被按键自己的工作拉长的；占比低 → 帧长另有来源，按键只是恰好落在这里。'
    }

    # FirstPressAtMs（新探针）：帧起→首个按键进入本列。有它就能把帧内时间按位置切开，
    # 而不只是知道「按键自己的工作占多少」。
    $hasSplit = @($pressStalls | Where-Object { $_.FirstPressAtMs -and -not [double]::IsNaN((ConvertTo-Double $_.FirstPressAtMs)) })

    if ($hasSplit.Count -gt 0) {
        $fp = New-Object 'System.Collections.Generic.List[double]'
        $pc = New-Object 'System.Collections.Generic.List[double]'
        $ap = New-Object 'System.Collections.Generic.List[double]'

        foreach ($r in $hasSplit) {
            $e = ConvertTo-Double $r.ElapsedMs
            $f = ConvertTo-Double $r.FirstPressAtMs
            $c = ConvertTo-Double $r.PressColumnMs
            if ([double]::IsNaN($e) -or [double]::IsNaN($f)) { continue }
            if ([double]::IsNaN($c)) { $c = 0 }
            $fp.Add($f); $pc.Add($c); $ap.Add($e - $f - $c)
        }

        if ($fp.Count -gt 0) {
            Write-Host ''
            Write-Host '  -- 按键帧的三段切分（只统计超阈值的按键帧） --' -ForegroundColor Cyan
            Write-Host ('    {0,-26} {1,8} {2,8} {3,8} {4,8}' -f '段', 'mean', 'p50', 'p90', 'max')

            foreach ($seg in @(@('帧起 → 按键进入本列', $fp), @('本列工时(OnPressed)', $pc), @('本列之后 → 帧尾', $ap))) {
                $label = $seg[0]
                $arr = $seg[1].ToArray()
                [Array]::Sort($arr)
                $sum = 0.0; foreach ($v in $arr) { $sum += $v }

                Write-Host ('    {0,-26} {1,8} {2,8} {3,8} {4,8}' -f `
                        $label,
                        (Format-F3 ($sum / $arr.Length)),
                        (Format-F3 (Get-Percentile $arr 0.5)),
                        (Format-F3 (Get-Percentile $arr 0.9)),
                        (Format-F3 $arr[$arr.Length - 1]))
            }

            Write-Host '    判读：哪一段占大头，就是那一段在制造按键延迟——前段＝输入派发机器，'
            Write-Host '          中段＝本列判定，后段＝判定落地后的帧内工作（结果扇出／容器增删／布局失效）。'
        }
    }
}
else {
    Write-Host '  （无含按键的 stall 帧，或 CSV 缺少 PressColumnMs 列）'
}

# ---------------------------------------------------------------- 判据 1：GC 因果

Write-Host ''
Write-Host '== 判据 1：GC 是否造成 stall ==' -ForegroundColor Cyan

$explained = @($stalls | Where-Object {
        $e = ConvertTo-Double $_.ElapsedMs
        $e -gt 0 -and (ConvertTo-Double $_.GcPauseDeltaMs) / $e -ge 0.5
    })

$innocent = @($stalls | Where-Object {
        $e = ConvertTo-Double $_.ElapsedMs
        $e -gt 0 -and (ConvertTo-Double $_.GcPauseDeltaMs) / $e -le 0.1
    })

$partial = @($stalls | Where-Object {
        $e = ConvertTo-Double $_.ElapsedMs
        $r = if ($e -gt 0) { (ConvertTo-Double $_.GcPauseDeltaMs) / $e } else { -1 }
        $r -gt 0.1 -and $r -lt 0.5
    })

Write-Host ("  GC 占该帧 >=50%（GC 是原因）: {0}" -f $explained.Length)
Write-Host ("  部分相关（10%~50%）          : {0}" -f $partial.Length)
Write-Host ("  GC 占该帧 <=10%（GC 被排除）: {0}" -f $innocent.Length)

if ($explained.Length -gt 0) {
    Write-Host ''
    Write-Host '  被 GC 解释的 stall（最慢 10 条）:'
    $explained | Sort-Object { ConvertTo-Double $_.ElapsedMs } -Descending | Select-Object -First 10 | ForEach-Object {
        Write-Host ("    t={0,10} Elapsed={1,8} GcPause={2,8} alloc={3,10} presses={4} iters={5} gen0+{6}" -f `
                $_.WallMs, $_.ElapsedMs, $_.GcPauseDeltaMs, $_.ThreadAllocDeltaBytes, $_.PressesInFrame, $_.FscIter, $_.Gen0Delta)
    }
}

if ($innocent.Length -gt 0) {
    Write-Host ''
    Write-Host '  GC 之外的 stall（最慢 10 条；这些要找别的来源）:'
    $innocent | Sort-Object { ConvertTo-Double $_.ElapsedMs } -Descending | Select-Object -First 10 | ForEach-Object {
        Write-Host ("    t={0,10} Elapsed={1,8} GcPause={2,8} alloc={3,10} presses={4} iters={5} gen0+{6}" -f `
                $_.WallMs, $_.ElapsedMs, $_.GcPauseDeltaMs, $_.ThreadAllocDeltaBytes, $_.PressesInFrame, $_.FscIter, $_.Gen0Delta)
    }
}

# ---------------------------------------------------------------- 分配

Write-Host ''
Write-Host '== stall 帧内的 update 线程分配 ==' -ForegroundColor Cyan

Write-Stats $stalls 'ThreadAllocDeltaBytes' 'ThreadAllocDeltaBytes'

$withPress = @($stalls | Where-Object { [int]$_.PressesInFrame -gt 0 })
Write-Host ("  含按键的 stall 帧: {0} / {1}（这些是真正会变成玩家感知延迟的帧）" -f $withPress.Length, $stalls.Length)

# ---------------------------------------------------------------- 判据 2：按键尾部归因

if ($PressPath) {
    if (-not (Test-Path -LiteralPath $PressPath)) {
        throw "找不到按键文件: $PressPath"
    }

    $presses = @(Import-Csv -LiteralPath $PressPath)

    # 预先把每个 stall 帧的窗口 [WallMs - ElapsedMs, WallMs] 展开
    $windows = @($stalls | ForEach-Object {
            $end = ConvertTo-Double $_.WallMs
            $len = ConvertTo-Double $_.ElapsedMs
            [pscustomobject]@{
                Start = $end - $len
                End   = $end
                Len   = $len
                Pause = ConvertTo-Double $_.GcPauseDeltaMs
            }
        })

    $slow = @($presses | Where-Object { (ConvertTo-Double $_.PreColumnMs) -gt $PressThresholdMs })
    $fast = @($presses | Where-Object { (ConvertTo-Double $_.PreColumnMs) -le $PressThresholdMs -and -not [double]::IsNaN((ConvertTo-Double $_.PreColumnMs)) })

    Write-Host ''
    Write-Host '== 判据 2：按键 PreColumn 尾部是否由帧 stall 造成 ==' -ForegroundColor Cyan
    Write-Host ("  对照对象: {0}" -f $PressPath)
    Write-Stats $fast '  PreColumnMs <= 阈值（对照）' 'PreColumnMs'
    Write-Stats $slow ("  PreColumnMs > {0:F2}（尾部）" -f $PressThresholdMs) 'PreColumnMs'

    $covered = 0
    $uncovered = 0
    $pauseSum = 0.0

    foreach ($press in $slow) {
        $wait = ConvertTo-Double $press.PreColumnMs
        if ([double]::IsNaN($wait)) { continue }

        $pressWall = ConvertTo-Double $press.WallMs
        $keyWall = $pressWall - $wait

        # 等待区间的总覆盖时长、以及落在其中的 GC 暂停量
        $overlap = 0.0
        $pause = 0.0

        foreach ($w in $windows) {
            $lo = [math]::Max($keyWall, $w.Start)
            $hi = [math]::Min($pressWall, $w.End)
            if ($hi -gt $lo) {
                $overlap += $hi - $lo
                $pause += $w.Pause
            }
        }

        if ($overlap / $wait -ge $CoverageRatio) {
            $covered++
            $pauseSum += $pause
        } else {
            $uncovered++
        }
    }

    Write-Host ''
    Write-Host ("  被 stall 覆盖 >= {0:P0} 的按键: {1}" -f $CoverageRatio, $covered)
    Write-Host ("  未被 stall 覆盖的按键      : {0}" -f $uncovered)

    if ($covered -gt 0) {
        Write-Host ("  这些按键等待区间内累计 GC 暂停: {0:F1}ms（平均 {1:F3}ms/次）" -f $pauseSum, ($pauseSum / $covered))
    }

    if ($uncovered -gt 0) {
        Write-Host '  未覆盖者的等待发生在帧边界之外：可能是输入线程排队、事件队列积压，'
        Write-Host '  或由若干低于阈值的连续帧叠加（调低 EZ_FRAME_PROBE_MS 可把后者纳入）。'
    }
}

# ---------------------------------------------------------------- 尾部对照

Write-Host ''
Write-Host "== 最慢 $TopN 帧 ==" -ForegroundColor Cyan
Write-Host ('  {0,10} {1,9} {2,9} {3,11} {4,5} {5,9} {6,9} {7,6} {8,8} {9,7}' -f 'WallMs', 'Elapsed', 'GcPause', 'ThreadAlloc', 'Press', 'PressCol', 'FirstPress', 'Iters', 'Gen0d', 'FrameIdx')

$stalls | Sort-Object { ConvertTo-Double $_.ElapsedMs } -Descending | Select-Object -First $TopN | ForEach-Object {
    ('  {0,10} {1,9} {2,9} {3,11} {4,5} {5,9} {6,9} {7,6} {8,8} {9,7}' -f `
            $_.WallMs, $_.ElapsedMs, $_.GcPauseDeltaMs, $_.ThreadAllocDeltaBytes,
            $_.PressesInFrame, $(if ($_.PressColumnMs) { $_.PressColumnMs } else { '-' }),
            $(if ($_.FirstPressAtMs) { $_.FirstPressAtMs } else { '-' }),
            $_.FscIter, $_.Gen0Delta, $_.FrameIndex)
}

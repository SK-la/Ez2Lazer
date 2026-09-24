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

$captured = @(Import-Csv -LiteralPath $Path)

if ($captured.Length -eq 0) {
    throw "没有帧明细：本次运行没有任何一帧超过阈值。这本身是好消息，可调低 EZ_FRAME_PROBE_MS 再看次尖峰。"
}

# 摘要文件与 CSV 同名（兄弟文件），先解析出来以取得本次运行的真实阈值。
if (-not $SummaryPath) {
    $csvFull = (Resolve-Path -LiteralPath $Path).Path
    $candidate = [System.IO.Path]::Combine(
        [System.IO.Path]::GetDirectoryName($csvFull),
        ([System.IO.Path]::GetFileNameWithoutExtension($csvFull) + '.summary.txt'))

    if (Test-Path -LiteralPath $candidate) { $SummaryPath = $candidate }
}

$thresholdMs = [double]::NaN

if ($SummaryPath -and (Test-Path -LiteralPath $SummaryPath)) {
    $header = @(Get-Content -LiteralPath $SummaryPath | Where-Object { $_ -match '\[EzFrameStall\]' })

    if ($header.Count -gt 0 -and $header[0] -match 'threshold=([\d.]+)ms') {
        $thresholdMs = [double]::Parse($matches[1], $inv)
    }
}

# CSV 是「捕获到的帧」：阈值调到 0 时就是全集，默认只含尾部（见 EZ_FRAME_PROBE_MS）。
# 尾部统计沿用 $stalls（超过阈值者），全集供帧节奏 / 平滑度分析用。
$allFrames = $captured

if (-not [double]::IsNaN($thresholdMs) -and $thresholdMs -gt 0) {
    $stalls = @($captured | Where-Object { (ConvertTo-Double $_.ElapsedMs) -ge $thresholdMs })
}
else {
    $stalls = $allFrames
}

# 「是否全集」只能由探针写下的阈值判定：尾部 CSV 的每一行都 >= 阈值，长度比较恒为相等。
# 阈值 0 是探针「抓全集」的显式标志（见 EZ_FRAME_PROBE_MS）；摘要缺失时按尾部处理。
$isFullSequence = (-not [double]::IsNaN($thresholdMs)) -and $thresholdMs -le 0

$firstWall = ConvertTo-Double $allFrames[0].WallMs
$lastWall = ConvertTo-Double $allFrames[-1].WallMs
$spanSeconds = ($lastWall - $firstWall) / 1000.0

Write-Host ''
Write-Host "文件: $Path" -ForegroundColor Green
Write-Host ("捕获帧明细: {0} 条，覆盖墙钟 {1:F1}s（约 {2:F2} 次/s）" -f `
        $allFrames.Length, $spanSeconds, $(if ($spanSeconds -gt 0) { $allFrames.Length / $spanSeconds } else { 0 }))

if ([double]::IsNaN($thresholdMs)) {
    Write-Host '  未找到摘要，无法确定阈值：按「只含尾部」处理，不做帧节奏分析。'
}
elseif ($isFullSequence) {
    Write-Host '  阈值=0 ⇒ 已抓全集，下面的统计覆盖所有帧。'
}
else {
    Write-Host ('  超阈值（{0}ms）: {1} 条 ⇒ 下面的幅度统计只覆盖尾部。' -f $thresholdMs, $stalls.Length)
    Write-Host '  要分析帧节奏 / 平滑度，用 EZ_FRAME_PROBE_MS=0 重跑一局（抓全集）。'
}

# ---------------------------------------------------------------- 全帧分布（摘要）

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
                Write-Host '    读法：p50 与 p90 同时抬高 → 按键帧确实更长。但先对照下表「按键工时/帧长」：'
                Write-Host '          若占比很低，帧长的大头不在判定代码里（draw / 取样 / HUD），'
                Write-Host '          并且要先确认本局帧率是否已远超体感阈值（见 EZ-PERFORMANCE.md §2.4）。'

                # 切分（只有新探针会写这行）。注意只有「本列工时」是按键自身的工作量。
                $splitLine = @(Get-Content -LiteralPath $SummaryPath | Where-Object { $_ -match '^pressSplit' })
                if ($splitLine.Count -gt 0 -and $splitLine[0] -match 'elapsedMean=([\d.]+)ms sincePrevFrameMean=([\d.]+)ms pressColumnMean=([\d.]+)ms afterPressMean=(-?[\d.]+)ms inputQueue=(-?\d+)') {
                    $el = [double]::Parse($matches[1], $inv)
                    $fp = [double]::Parse($matches[2], $inv)
                    $pc = [double]::Parse($matches[3], $inv)
                    $ap = [double]::Parse($matches[4], $inv)
                    $iq = [int]$matches[5]

                    Write-Host ''
                    Write-Host '    按键帧的切分（均值）——只有「本列工时」是按键自身的工作量:' -ForegroundColor Cyan
                    Write-Host ("      帧长                   {0,7}ms" -f (Format-F3 $el))
                    Write-Host ("      上一帧边界→按键        {0,7}ms   {1,6:P1}   ← 帧间等待 + 呈现，不是本帧的工作" -f (Format-F3 $fp), $(if ($el -gt 0) { $fp / $el } else { 0 }))
                    Write-Host ("      本列工时（OnPressed）  {0,7}ms   {1,6:P1}   ← 唯一样本内的按键自身工作" -f (Format-F3 $pc), $(if ($el -gt 0) { $pc / $el } else { 0 }))
                    Write-Host ("      按键→下一帧边界        {0,7}ms   {1,6:P1}   ← 本帧剩余（draw / 其他 update）" -f (Format-F3 $ap), $(if ($el -gt 0) { $ap / $el } else { 0 }))
                    Write-Host '    判读：第一段的零点在上一帧边界，随帧率变化而不随代码量变化；它大 = 帧节奏问题，'
                    Write-Host '          不是优化判定代码能解决的。'
                    Write-Host ("      非位置输入队列长度     {0,7}      ← 每次按下/抬起要重建并 Reverse 的列表规模" -f $iq)
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

    # SincePrevFrameMs（新探针）：本帧内首个按键距上一帧边界多远。
    # 它含帧间等待与呈现，**不是**「按键前的工作量」；只有 PressColumnMs 是按键自身工作。
    $hasSplit = @($pressStalls | Where-Object { $_.SincePrevFrameMs -and -not [double]::IsNaN((ConvertTo-Double $_.SincePrevFrameMs)) })

    if ($hasSplit.Count -gt 0) {
        $fp = New-Object 'System.Collections.Generic.List[double]'
        $pc = New-Object 'System.Collections.Generic.List[double]'
        $ap = New-Object 'System.Collections.Generic.List[double]'

        foreach ($r in $hasSplit) {
            $e = ConvertTo-Double $r.ElapsedMs
            $f = ConvertTo-Double $r.SincePrevFrameMs
            $c = ConvertTo-Double $r.PressColumnMs
            if ([double]::IsNaN($e) -or [double]::IsNaN($f)) { continue }
            if ([double]::IsNaN($c)) { $c = 0 }
            $fp.Add($f); $pc.Add($c); $ap.Add($e - $f - $c)
        }

        if ($fp.Count -gt 0) {
            Write-Host ''
            Write-Host '  -- 按键帧的切分（只统计超阈值的按键帧） --' -ForegroundColor Cyan
            Write-Host ('    {0,-26} {1,8} {2,8} {3,8} {4,8}' -f '段', 'mean', 'p50', 'p90', 'max')

            foreach ($seg in @(@('上一帧边界→按键(等待)', $fp), @('本列工时(OnPressed)', $pc), @('按键→下一帧边界', $ap))) {
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

            Write-Host '    判读：只有「本列工时」是按键自身的工作量。第一段的零点在上一帧边界，装的是'
            Write-Host '          帧间等待 + 呈现，随帧率变化而不随代码量变化——它是帧节奏，不是热路径开销。'
            Write-Host '          若第一段占大头，方向是帧节奏 / 输入递送时机，不是优化判定代码。'
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

# ---------------------------------------------------------------- 帧节奏 / 平滑度
#
# 视觉上「帧数很高但 note 下落不顺滑」= 帧耗时的**相对抖动**：note 位置按实际流逝时间推进，
# 一帧耗时是 p50 的 2 倍，这一帧就跳了 2 倍距离。所以要看的是倍率分布与卡顿的周期性，
# 而不是绝对耗时。只有抓到全集（EZ_FRAME_PROBE_MS=0）才做这节。

if ($isFullSequence -and $allFrames.Length -ge 100) {
    Write-Host ''
    Write-Host '== 帧节奏 / 平滑度（全集） ==' -ForegroundColor Cyan

    $elapsed = New-Object 'System.Collections.Generic.List[double]'
    foreach ($r in $allFrames) { $elapsed.Add((ConvertTo-Double $r.ElapsedMs)) }

    $e = $elapsed.ToArray(); [Array]::Sort($e)
    $p50 = Get-Percentile $e 0.5
    $p90 = Get-Percentile $e 0.9
    $p99 = Get-Percentile $e 0.99

    Write-Host ("  帧耗时 p50={0} p90={1} p99={2} p99.9={3} max={4}（隐含 p50 帧率 {5:N0} fps）" -f `
            (Format-F3 $p50), (Format-F3 $p90), (Format-F3 $p99), (Format-F3 (Get-Percentile $e 0.999)), (Format-F3 $e[-1]), `
            $(if ($p50 -gt 0) { 1000 / $p50 } else { 0 }))

    # 相对倍率才是可见性判据：2 倍 = note 本帧跳两倍距离。
    $disp90 = if ($p50 -gt 0) { $p90 / $p50 } else { [double]::NaN }
    $disp99 = if ($p50 -gt 0) { $p99 / $p50 } else { [double]::NaN }

    Write-Host ("  相对抖动: p90/p50 = {0:F2}x   p99/p50 = {1:F2}x" -f $disp90, $disp99)
    Write-Host '  读法：p90/p50 越接近 1 越平滑。无帧率上限时基线帧很短，同一份固定开销（GC / 分配 /'
    Write-Host '        调度唤醒）在相对倍率上被放大；限帧或开垂直同步会把同一抖动压成看不出的比例。'

    foreach ($mult in 2.0, 3.0, 4.0, 6.0) {
        $limit = $p50 * $mult
        $count = 0
        foreach ($v in $elapsed) { if ($v -ge $limit) { $count++ } }

        Write-Host ("  帧耗时 >= {0:F0}x p50 ({1}ms): {2,7} 帧 = {3,6:P2}  = {4,7:F1} 次/s" -f `
                $mult, (Format-F3 $limit), $count, ($count / $e.Length), `
                $(if ($spanSeconds -gt 0) { $count / $spanSeconds } else { 0 }))
    }

    # 相邻帧的倍率跳变：这是「不顺滑」最直接的形式（这一帧相对上一帧突然加速）。
    $jumps = New-Object 'System.Collections.Generic.List[double]'
    $prev = [double]::NaN

    foreach ($r in $allFrames) {
        $v = ConvertTo-Double $r.ElapsedMs
        if (-not [double]::IsNaN($prev) -and $prev -gt 0) { $jumps.Add($v / $prev) }
        $prev = $v
    }

    if ($jumps.Count -gt 0) {
        $j = $jumps.ToArray(); [Array]::Sort($j)
        $over2 = 0
        foreach ($v in $j) { if ($v -ge 2.0) { $over2++ } }

        Write-Host ''
        Write-Host ("  相邻帧倍率: p50={0:F2}x p90={1:F2}x p99={2:F2}x max={3:F2}x" -f `
                (Get-Percentile $j 0.5), (Get-Percentile $j 0.9), (Get-Percentile $j 0.99), $j[-1])
        Write-Host ("  相邻帧跳变 >= 2x: {0} 次 = {1:F1} 次/s  ← 每次都是肉眼可能看见的一次卡顿" -f `
                $over2, $(if ($spanSeconds -gt 0) { $over2 / $spanSeconds } else { 0 }))
    }

    # 卡顿的周期性：等间隔尖峰指向定时源（GC 代回收 / 定时器），成簇则指向突发或外部抢占。
    $hits = @($allFrames | Where-Object { (ConvertTo-Double $_.ElapsedMs) -ge ($p50 * 2.0) })

    if ($hits.Length -ge 20) {
        $gaps = New-Object 'System.Collections.Generic.List[double]'
        for ($i = 1; $i -lt $hits.Length; $i++) {
            $gaps.Add((ConvertTo-Double $hits[$i].WallMs) - (ConvertTo-Double $hits[$i - 1].WallMs))
        }

        $g = $gaps.ToArray(); [Array]::Sort($g)
        $gsum = 0.0; foreach ($v in $g) { $gsum += $v }

        Write-Host ''
        Write-Host ("  卡顿（>= 2x p50）共 {0} 次；间隔 mean={1:F1}ms median={2:F1}ms" -f `
                $hits.Length, ($gsum / $g.Length), (Get-Percentile $g 0.5))

        # 周期性看间隔分布：明显的窄峰 = 定时源。
        $buckets = @{}
        foreach ($v in $g) {
            if ($v -gt 200) { continue }
            $b = [math]::Floor($v / 10) * 10
            if ($buckets.ContainsKey($b)) { $buckets[$b]++ } else { $buckets[$b] = 1 }
        }

        $top = $buckets.GetEnumerator() | Sort-Object -Property Value -Descending | Select-Object -First 5

        Write-Host '  间隔分布（10ms 桶）前 5 名 —— 窄峰指向定时源:'
        foreach ($kv in $top) {
            Write-Host ("    {0,4}-{1,4}ms : {2,5} 次" -f $kv.Key, ($kv.Key + 10), $kv.Value)
        }

        Write-Host '  对照：gen0 间隔可由摘要的 gen0 回收速率算出（回收/s ⇒ 1000/该值 ms）。'
        Write-Host '        若尖峰与该间隔吻合 ⇒ 抖动由代回收驱动，方向是减少 update 线程分配。'
    }
}
elseif (-not $isFullSequence) {
    Write-Host ''
    Write-Host '== 帧节奏 / 平滑度 ==' -ForegroundColor Cyan
    Write-Host '  跳过：本次 CSV 只含超阈值的尾部，无法给出倍率分布与周期性。'
    Write-Host '  用 EZ_FRAME_PROBE_MS=0 重跑一局抓全集（≈19MB / 100s @2000fps）。'
}

# ---------------------------------------------------------------- 尾部对照

Write-Host ''
Write-Host "== 最慢 $TopN 帧 ==" -ForegroundColor Cyan
Write-Host ('  {0,10} {1,9} {2,9} {3,11} {4,5} {5,9} {6,9} {7,6} {8,8} {9,7}' -f 'WallMs', 'Elapsed', 'GcPause', 'ThreadAlloc', 'Press', 'PressCol', 'SincePrev', 'Iters', 'Gen0d', 'FrameIdx')

$stalls | Sort-Object { ConvertTo-Double $_.ElapsedMs } -Descending | Select-Object -First $TopN | ForEach-Object {
    ('  {0,10} {1,9} {2,9} {3,11} {4,5} {5,9} {6,9} {7,6} {8,8} {9,7}' -f `
            $_.WallMs, $_.ElapsedMs, $_.GcPauseDeltaMs, $_.ThreadAllocDeltaBytes,
            $_.PressesInFrame, $(if ($_.PressColumnMs) { $_.PressColumnMs } else { '-' }),
            $(if ($_.SincePrevFrameMs) { $_.SincePrevFrameMs } else { '-' }),
            $_.FscIter, $_.Gen0Delta, $_.FrameIndex)
}

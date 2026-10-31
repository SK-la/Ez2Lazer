# Ez2Lazer 启动预热 A/B 测量。
#
# 同一份代码分别以 ReadyToRun 关 / 开 发布到两个独立目录，各自冷启 N 次，从日志里抠出
# [EzStartupTrace] 的分段耗时；可选再打开 runtime 自带的 JIT 时间报告，直接给出 JIT 编译量。
#
# 用法（在 osu 仓库根目录，或直接用 .vscode/tasks.json 里的 "Measure startup (R2R A/B)"）：
#   pwsh ./scripts/Measure-EzStartup.ps1
#   pwsh ./scripts/Measure-EzStartup.ps1 -Runs 7 -WithJit
#   pwsh ./scripts/Measure-EzStartup.ps1 -SkipPublish -R2ROutDir <dir> -BaselineOutDir <dir>
#   pwsh ./scripts/Measure-EzStartup.ps1 -Runs 3 -WithJit -NoTiered   # 额外跑一组 DOTNET_TieredCompilation=0 对照
#   pwsh ./scripts/Measure-EzStartup.ps1 -FrameworkOnly              # 额外对比「只 R2R osu.Framework」
#
# -GameplayEnter（可选，未跑通）尝试从进程外发 Enter 驱动「选歌开始 -> 进局」：
# 主菜单那次 Enter 能生效，但选歌界面那次进局键会被界面自身的焦点（搜索框/轮播）吞掉，
# 自动化不可靠，因此默认不启用。进局耗时可手动按回车，然后从 ez_runtime 日志读
# "EnterPath GameplayStart" / "PlayerLoader.Entered" / "Player.Entered" 三条分段的 +Nms 差值。
param(
    [int]$Runs = 5,
    [string]$R2ROutDir = "$PSScriptRoot/../artifacts/startup-r2r",
    [string]$BaselineOutDir = "$PSScriptRoot/../artifacts/startup-baseline",
    [string]$ResultDir = "$PSScriptRoot/../artifacts/startup-results",
    [int]$LaunchTimeoutSeconds = 180,
    [switch]$SkipPublish,
    [switch]$WithJit,
    [switch]$NoTiered,
    [switch]$FrameworkOnly,
    [string]$FrameworkOnlyOutDir = "$PSScriptRoot/../artifacts/startup-fwonly",
    [switch]$GameplayEnter,
    [int]$GameplayRuns = 3,
    [int]$HangProtectSeconds = 30,
    [switch]$Memory,
    [int]$MemorySettleSeconds = 6
)

$ErrorActionPreference = 'Stop'

# 选歌就绪后进局键的最大补发次数；选中项提交前按下会被 SelectAndRun 的 IsDefault 提前返回吞掉。
$max_enter_attempts = 5

# 进局子循环上限：卡住时不必等完整个 LaunchTimeout。
$GameplayEnterTimeoutSeconds = 60

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$project = Join-Path $repoRoot 'osu.Desktop/osu.Desktop.csproj'

function Get-EzDataDirectory {
    $roaming = [Environment]::GetFolderPath('ApplicationData')
    $ini = Join-Path $roaming 'osu-Ez2Lazer/storage.ini'

    if (Test-Path $ini) {
        foreach ($line in Get-Content $ini) {
            $parts = $line.Split('=', 2)
            if ($parts.Count -eq 2 -and $parts[0].Trim() -eq 'FullPath') {
                return $parts[1].Trim()
            }
        }
    }

    return (Join-Path $roaming 'osu-Ez2Lazer')
}

# release 构建里 EzStartupTrace.Log 默认被 EzStartupTuning.EnableStartupTrace 挡住。
# 用 EZ2LAZER_STARTUP_TRACE=1 打开，不改源码——脚本改源码会在运行中途触发重编译，
# 而且一旦写坏就会把仓库文件清空。
function Enable-StartupTrace {
    $env:EZ2LAZER_STARTUP_TRACE = '1'
    Write-Host 'EZ2LAZER_STARTUP_TRACE=1（子进程继承，不影响仓库文件）。'
}

function Invoke-Publish {
    param([string]$OutDir, [bool]$ReadyToRun, [string]$OnlyAssembly = $null)

    # 必须清干净：publish 只覆盖它这次产出的文件，上一轮的 R2R dll 会原样留下，
    # 造成「只 R2R framework」的目录里混进上一轮的 osu.Game.R2R。
    if (Test-Path $OutDir) { Remove-Item -Recurse -Force $OutDir }
    New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

    $r2r = if ($ReadyToRun) { 'true' } else { 'false' }
    Write-Host "publish ReadyToRun=$r2r only=$OnlyAssembly -> $OutDir"

    $args = @('publish', $project, '-c', 'Release', '-r', 'win-x64', '--self-contained', 'false', '-o', $OutDir, "-p:PublishReadyToRun=$r2r")

    # 总是显式传 EzReadyToRunOnly：仓库默认值现在是 osu.Framework，
    # 不传的话「R2R 全部程序集」那一组会静默退化成 fwonly，三组配置就不再互斥。
    $args += "-p:EzReadyToRunOnly=$OnlyAssembly"

    & dotnet @args | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (ReadyToRun=$r2r only=$OnlyAssembly)" }
}

function Resolve-GameExe {
    param([string]$OutDir)

    foreach ($candidate in @('Ez2osu!.exe', 'Ez2osu!', 'osu.Desktop.exe')) {
        $path = Join-Path $OutDir $candidate
        if (Test-Path $path) { return $path }
    }

    throw "在 $OutDir 找不到可执行文件"
}

function Get-LaunchTime {
    param([string]$ExePath, [string]$DataDir, [string]$Label, [switch]$DisableTiered)

    $logDir = Join-Path $DataDir 'logs'
    New-Item -ItemType Directory -Force -Path $logDir | Out-Null

    $before = @(Get-ChildItem -Path $logDir -Filter '*.log' -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Name)
    $jitFile = Join-Path $ResultDir "jit-$Label.txt"
    if (Test-Path $jitFile) { Remove-Item $jitFile -Force }

    if ($WithJit) { $env:DOTNET_JitTimeLogFile = $jitFile }
    if ($DisableTiered) { $env:DOTNET_TieredCompilation = '0' } else { Remove-Item Env:\DOTNET_TieredCompilation -ErrorAction SilentlyContinue }

    $start = Get-Date
    $proc = Start-Process -FilePath $ExePath -WorkingDirectory (Split-Path -Parent $ExePath) -PassThru
    $startupMs = $null
    $deadline = (Get-Date).AddSeconds($LaunchTimeoutSeconds)

    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 150

        $new = @(Get-ChildItem -Path $logDir -Filter '*.log' -ErrorAction SilentlyContinue | Where-Object { $before -notcontains $_.Name })
        if ($new.Count -eq 0) { continue }

        $hit = Select-String -Path $new.FullName -SimpleMatch 'MainMenu.OnEntering frameCost' -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($hit -and $hit.Line -match '\+(\d+)ms') {
            $startupMs = [int]$Matches[1]
            break
        }
    }

    if (-not $proc.HasExited) { $proc.Kill() }
    $proc.WaitForExit()

    Remove-Item Env:\DOTNET_JitTimeLogFile -ErrorAction SilentlyContinue
    Remove-Item Env:\DOTNET_TieredCompilation -ErrorAction SilentlyContinue

    return [pscustomobject]@{
        Label     = $Label
        StartupMs = $startupMs
        WallMs    = [math]::Round(((Get-Date) - $start).TotalMilliseconds)
        JitFile   = if (Test-Path $jitFile) { $jitFile } else { $null }
    }
}

function Get-JitSummary {
    param([string]$JitFile)

    if (-not $JitFile) { return $null }

    $methods = $null
    $totalMs = $null

    foreach ($line in (Get-Content $JitFile -TotalCount 8)) {
        if ($line -match 'Compiled (\d+) methods') { $methods = [int]$Matches[1] }
        if ($line -match 'Time:\s*total:\s*[\d.]+ Mcycles/\s*([\d.]+) ms') { $totalMs = [double]$Matches[1] }
    }

    return [pscustomobject]@{ Methods = $methods; TotalMs = $totalMs }
}

# 进局耗时只有在日志里出现 EnterPath GameplayStart 之后才成立；
# 逐行解析这几条里程碑的 +Nms，避免把别的启动行误当成分段端点。
# 返回 [hashtable] 而不是 [pscustomobject]：后者没有 ContainsKey，用 `.Contains()` 判断会直接失败。
function Get-EnterPathSegments {
    param([string[]]$LogFiles)

    $result = @{}
    $result['MainMenuMs'] = $null

    foreach ($file in $LogFiles) {
        foreach ($line in (Get-Content $file -ErrorAction SilentlyContinue)) {
            if ($line -notmatch '\[EzStartupTrace\] \+(\d+)ms (.*)$') { continue }

            $ms = [int]$Matches[1]
            $rest = $Matches[2]

            # 取每个标记的**首次**出现：Player 中途退回 PlayerLoader 会让
            # PlayerLoader.Entered 再次触发，取最后一次会让「转圈时长」算成负数。
            if (-not $result.ContainsKey('StartMs') -and $rest -match '^EnterPath GameplayStart$') { $result['StartMs'] = $ms }
            elseif (-not $result.ContainsKey('LoaderMs') -and $rest -match '^EnterPath PlayerLoader\.Entered$') { $result['LoaderMs'] = $ms }
            elseif (-not $result.ContainsKey('GateMs') -and $rest -match '^EnterPath GameplayStart\.gateReleased') { $result['GateMs'] = $ms }
            elseif (-not $result.ContainsKey('PlayerMs') -and $rest -match '^EnterPath Player\.Entered$') { $result['PlayerMs'] = $ms }
            elseif (-not $result.ContainsKey('CarouselMs') -and $rest -match '^EnterPath CarouselItemsPresented') { $result['CarouselMs'] = $ms }
            elseif (-not $result.ContainsKey('MainMenuMs') -and $rest -match '^MainMenu\.OnEntering') { $result['MainMenuMs'] = $ms }
        }
    }

    return $result
}

# 把按键片段写进临时 .ps1，交给独立 powershell 进程执行。
# 必须独立进程：PowerShell 的 SendKeys 在本进程内调用不生效；
# 而 AppActivate 按标题找窗口会随标题变化（"Ez2osu! (development)" 等）失手，
# 所以先按 PID 拿到主窗口再 SetForegroundWindow。
function New-SenderProcess {
    param([string]$Snippet, [string]$Name)

    $path = Join-Path $env:TEMP "ez-send-$Name.ps1"
    Set-Content -Path $path -Value $Snippet -Encoding UTF8

    return Start-Process -FilePath 'powershell' -WindowStyle Hidden -PassThru -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $path)
}

function New-SingleKeystrokeSender {
    param([int]$TargetPid, [string]$Key)

    $resultPath = Join-Path $env:TEMP "ez-send-result-$TargetPid.txt"
    Remove-Item $resultPath -Force -ErrorAction SilentlyContinue

    # 把 AppActivate 的结果写回文件：没有它就只能猜「键到底有没有送到游戏窗口」。
    $snippet = @"
`$resultPath = '$resultPath'
`$proc = Get-Process -Id $TargetPid -ErrorAction SilentlyContinue
if (-not `$proc) { Set-Content -Path `$resultPath -Value 'no-process'; exit }
`$shell = New-Object -ComObject WScript.Shell
`$activated = `$false
for (`$i = 0; `$i -lt 10; `$i++) {
    if (`$shell.AppActivate($TargetPid)) { `$activated = `$true; break }
    Start-Sleep -Milliseconds 200
}
Start-Sleep -Milliseconds 400
`$shell.SendKeys('$Key')
Set-Content -Path `$resultPath -Value "activated=`$activated hwnd=`$(`$proc.MainWindowHandle) title=`$(`$proc.MainWindowTitle)"
"@

    return New-SenderProcess -Snippet $snippet -Name "single-$TargetPid"
}

# 主菜单稳定后按 Enter 进选歌：主菜单的 GlobalAction.Select 会走 loadSongSelect。
function Send-Keystroke {
    param([int]$TargetPid, [string]$Key)

    $s = New-SingleKeystrokeSender -TargetPid $TargetPid -Key $Key
    Start-Sleep -Milliseconds 1500
    Stop-Process -Id $s.Id -Force -ErrorAction SilentlyContinue

    $resultPath = Join-Path $env:TEMP "ez-send-result-$TargetPid.txt"
    if (Test-Path $resultPath) { Write-Host ("      send: " + (Get-Content $resultPath -Raw).Trim()) }
    else { Write-Host '      send: (no result file)' }
}

# 进局子循环的上限：卡住时不必干等到整个 LaunchTimeout。
function Wait-ForPlayerEntry {
    param([int]$TargetPid, [string]$LogDir, [string[]]$Before, [int]$MaxSeconds)

    $deadline = (Get-Date).AddSeconds($MaxSeconds)
    $segments = @{}
    $carouselSeen = $false
    $enterAttempts = 0
    $lastAttemptAt = Get-Date

    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 200

        $new = @(Get-ChildItem -Path $LogDir -Filter '*.log' -ErrorAction SilentlyContinue | Where-Object { $Before -notcontains $_.Name })
        if ($new.Count -eq 0) { continue }

        $segments = Get-EnterPathSegments -LogFiles $new.FullName

        if (-not $carouselSeen -and $segments.CarouselMs) {
            $carouselSeen = $true
            Write-Host '    carousel ready -> Enter (start gameplay)'
            Send-Keystroke -TargetPid $TargetPid -Key '{ENTER}'
            $lastAttemptAt = Get-Date
            continue
        }

        if ($segments.PlayerMs) { break }

        if ($carouselSeen -and -not $segments.StartMs -and $enterAttempts -lt $max_enter_attempts -and ((Get-Date) - $lastAttemptAt).TotalSeconds -ge 1.5) {
            $enterAttempts++
            $lastAttemptAt = Get-Date
            Write-Host "    retry gameplay Enter #$enterAttempts"
            Send-Keystroke -TargetPid $TargetPid -Key '{ENTER}'
        }
    }

    return $segments
}

function Get-GameplayEntryTime {
    param([string]$ExePath, [string]$DataDir, [string]$Label)

    $logDir = Join-Path $DataDir 'logs'
    $before = @(Get-ChildItem -Path $logDir -Filter '*.log' -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Name)

    $proc = Start-Process -FilePath $ExePath -WorkingDirectory (Split-Path -Parent $ExePath) -PassThru

    $deadline = (Get-Date).AddSeconds($LaunchTimeoutSeconds)
    $mainMenuSeen = $false
    $segments = @{}

    # 第一次 Enter 也由日志事件驱动：主菜单就绪前按下会被忽略。
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 200

        $new = @(Get-ChildItem -Path $logDir -Filter '*.log' -ErrorAction SilentlyContinue | Where-Object { $before -notcontains $_.Name })
        if ($new.Count -eq 0) { continue }

        $segments = Get-EnterPathSegments -LogFiles $new.FullName

        if (-not $mainMenuSeen -and $segments.MainMenuMs) {
            $mainMenuSeen = $true
            Write-Host '    main menu ready -> Enter (song select)'
            Send-Keystroke -TargetPid $proc.Id -Key '{ENTER}'
            break
        }
    }

    if ($mainMenuSeen) {
        $segments = Wait-ForPlayerEntry -TargetPid $proc.Id -LogDir $logDir -Before $before -MaxSeconds $GameplayEnterTimeoutSeconds
    }

    if (-not $proc.HasExited) { $proc.Kill() }
    $proc.WaitForExit()

    if ($segments.StartMs -and $segments.PlayerMs) {
        $segments['TotalMs'] = $segments.PlayerMs - $segments.StartMs
    }

    return @{ Label = $Label; Segments = $segments }
}

# 采样要跨时刻反复读，太密没意义；这是两次读数之间的间隔。
$memorySampleMilliseconds = 1500

function New-MemorySampler {
    param([string]$ProcessName, [string]$OutPath)

    # 另起进程采样：这台机器上私有字节按需触达，主进程轮询会和游戏抢 CPU，把被测对象改掉。
    # 按**进程名**跟踪而不是 PID：EzGcModeBootstrap 可能带环境变量重入新进程并让原进程退出，
    # 死盯 PID 会在重入后跟丢，读到的只是那个已退出的壳。
    $snippet = @"
`$samples = New-Object System.Collections.Generic.List[object]
`$deadline = (Get-Date).AddMinutes(10)
while ((Get-Date) -lt `$deadline) {
    # 取最近启动的那个同名进程（重入后新进程的 StartTime 更晚）。
    `$proc = Get-Process -Name '$ProcessName' -ErrorAction SilentlyContinue |
        Sort-Object StartTime -Descending | Select-Object -First 1
    if (-not `$proc) {
        if (`$samples.Count -gt 5) { break }
        Start-Sleep -Milliseconds $memorySampleMilliseconds
        continue
    }
    try {
        `$samples.Add([pscustomobject]@{
            t         = [DateTime]::Now.ToString('HH:mm:ss.fff')
            PrivateMB = [math]::Round(`$proc.PrivateMemorySize64 / 1MB, 1)
            WorkingMB = [math]::Round(`$proc.WorkingSet64 / 1MB, 1)
        }) | Out-Null
    } catch { }
    Start-Sleep -Milliseconds $memorySampleMilliseconds
}
`$samples | ConvertTo-Json -Compress | Set-Content -Path '$OutPath'
"@

    return New-SenderProcess -Snippet $snippet -Name 'mem'
}

function Get-MemorySnapshot {
    param([string]$ExePath, [string]$DataDir, [string]$Label)

    $logDir = Join-Path $DataDir 'logs'
    $before = @(Get-ChildItem -Path $logDir -Filter '*.log' -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Name)

    $launchedAt = Get-Date
    $proc = Start-Process -FilePath $ExePath -WorkingDirectory (Split-Path -Parent $ExePath) -PassThru
    $outPath = Join-Path $ResultDir "mem-$Label.json"
    Remove-Item $outPath -Force -ErrorAction SilentlyContinue

    $sampler = New-MemorySampler -ProcessName 'Ez2osu!' -OutPath $outPath

    # 等到主菜单出现，再多等几秒让 BDSP/预加载的瞬时分配落下去，取稳态读数。
    $deadline = (Get-Date).AddSeconds($LaunchTimeoutSeconds)
    $menuSeen = $false

    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 300
        $new = @(Get-ChildItem -Path $logDir -Filter '*.log' -ErrorAction SilentlyContinue | Where-Object { $before -notcontains $_.Name })
        if ($new.Count -eq 0) { continue }

        $segments = Get-EnterPathSegments -LogFiles $new.FullName
        if ($segments.MainMenuMs) { $menuSeen = $true; break }
    }

    if ($menuSeen) { Start-Sleep -Seconds $MemorySettleSeconds }

    # 稳态读数直接按名字重新解析进程：主进程手上那个 pid 句柄在 GC 重入后已失效，
    # 而让采样子进程「跟名字」会和新旧进程重叠打架（上一轮就因此读到了 17MB 的启动壳）。
    # 只认 StartTime 晚于本次启动的那个进程。
    $steadyPrivate = $null
    $steadyWorking = $null

    try {
        $live = Get-Process -Name 'Ez2osu!' -ErrorAction SilentlyContinue |
            Where-Object { $_.StartTime -ge $launchedAt } |
            Sort-Object StartTime -Descending | Select-Object -First 1

        if ($live) {
            $live.Refresh()
            $steadyPrivate = [math]::Round($live.PrivateMemorySize64 / 1MB, 1)
            $steadyWorking = [math]::Round($live.WorkingSet64 / 1MB, 1)
        }
    }
    catch { }

    Stop-GameProcess
    Start-Sleep -Milliseconds 1200
    Stop-Process -Id $sampler.Id -Force -ErrorAction SilentlyContinue

    $peakMb = $null
    if (Test-Path $outPath) {
        try {
            $samples = @(Get-Content $outPath -Raw | ConvertFrom-Json)
            if ($samples.Count -gt 0) { $peakMb = ($samples | Measure-Object -Property PrivateMB -Maximum).Maximum }
        }
        catch { }
    }

    return [pscustomobject]@{
        Label     = $Label
        PrivateMB = $steadyPrivate
        WorkingMB = $steadyWorking
        PeakMB    = $peakMb
    }
}

function Show-MemoryStats {
    param([object[]]$Samples, [string]$Name)

    $priv = @($Samples | Where-Object { $null -ne $_.PrivateMB } | Select-Object -ExpandProperty PrivateMB)
    $work = @($Samples | Where-Object { $null -ne $_.WorkingMB } | Select-Object -ExpandProperty WorkingMB)
    $peak = @($Samples | Where-Object { $null -ne $_.PeakMB } | Select-Object -ExpandProperty PeakMB)

    if ($priv.Count -eq 0) { Write-Host "$Name : 无内存样本"; return $null }

    Write-Host ("{0,-24} n={1} 私有中位={2}MB [{3}..{4}]  工作集中位={5}MB  峰值私有中位={6}MB" -f `
            $Name, $priv.Count, (Get-MedianValue $priv), ($priv | Measure-Object -Minimum).Minimum, ($priv | Measure-Object -Maximum).Maximum,
        (Get-MedianValue $work), (Get-MedianValue $peak))
    return (Get-MedianValue $priv)
}

function Get-MedianValue($values)
{
    $sorted = @($values | Sort-Object)
    if ($sorted.Count -eq 0) { return $null }
    if ($sorted.Count % 2 -eq 1) { return $sorted[[int]($sorted.Count / 2)] }
    return [Math]::Round(($sorted[$sorted.Count / 2 - 1] + $sorted[$sorted.Count / 2]) / 2, 1)
}

function Show-Stats {
    param([object[]]$Samples, [string]$Name)

    $values = @($Samples | Where-Object { $null -ne $_.StartupMs } | Select-Object -ExpandProperty StartupMs)

    if ($values.Count -eq 0) {
        Write-Host "$Name : 样本为空（确认 EZ2LAZER_STARTUP_TRACE=1 已生效、且日志目录可写）"
        return $null
    }

    $sorted = $values | Sort-Object
    $median = $sorted[[int][math]::Floor($sorted.Count / 2)]
    $mean = [math]::Round(($values | Measure-Object -Average).Average, 1)

    Write-Host ("{0} : n={1} min={2} median={3} mean={4} max={5} ms" -f $Name, $values.Count, $sorted[0], $median, $mean, $sorted[-1])
    return $median
}

function Stop-GameProcess {
    Get-Process -Name 'Ez2osu!' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
}

if (-not (Test-Path $project)) { throw "找不到项目：$project" }

$dataDir = Get-EzDataDirectory
Write-Host "数据目录：$dataDir"

New-Item -ItemType Directory -Force -Path $ResultDir | Out-Null

$baselineSamples = @()
$r2rSamples = @()

# 全程打开启动追踪；子进程自动继承本进程环境变量。
Enable-StartupTrace

if (-not $SkipPublish) {
        Invoke-Publish -OutDir $R2ROutDir -ReadyToRun $true
        Invoke-Publish -OutDir $BaselineOutDir -ReadyToRun $false

        if ($FrameworkOnly) {
            Invoke-Publish -OutDir $FrameworkOnlyOutDir -ReadyToRun $true -OnlyAssembly 'osu.Framework'
        }
    }

    $baseExe = Resolve-GameExe $BaselineOutDir
    $r2rExe = Resolve-GameExe $R2ROutDir

    # 体积断言：R2R 生效时对应程序集应该明显变大；只 R2R framework 时其余应当与基线一致。
    foreach ($dll in @('osu.Framework.dll', 'osu.Game.dll', 'osu.Game.Rulesets.Mania.dll')) {
        $r = Get-Item (Join-Path $R2ROutDir $dll) -ErrorAction SilentlyContinue
        $b = Get-Item (Join-Path $BaselineOutDir $dll) -ErrorAction SilentlyContinue

        if ($r -and $b) {
            $delta = [math]::Round(100.0 * ($r.Length - $b.Length) / $b.Length, 1)
            Write-Host ("{0}: baseline={1} r2r={2} (+{3}%)" -f $dll, $b.Length, $r.Length, $delta)
        }
    }

    # 冷启：每次先杀掉残留进程，否则单实例 IPC 会让下一次启动直接退出。
    $noTieredSamples = @()
    $fwOnlySamples = @()
    $fwExe = if ($FrameworkOnly) { Resolve-GameExe $FrameworkOnlyOutDir } else { $null }

    for ($i = 1; $i -le $Runs; $i++) {
        Stop-GameProcess
        Start-Sleep -Seconds 3
        $baselineSamples += Get-LaunchTime -ExePath $baseExe -DataDir $dataDir -Label "baseline-$i"

        if ($fwExe) {
            Stop-GameProcess
            Start-Sleep -Seconds 3
            $fwOnlySamples += Get-LaunchTime -ExePath $fwExe -DataDir $dataDir -Label "fwonly-$i"
        }

        Stop-GameProcess
        Start-Sleep -Seconds 3
        $r2rSamples += Get-LaunchTime -ExePath $r2rExe -DataDir $dataDir -Label "r2r-$i"

        if ($NoTiered) {
            Stop-GameProcess
            Start-Sleep -Seconds 3
            # 关 tiered JIT 的对照项：R2R 命中后省掉热方法重编译，但会牺牲稳态峰值，只做对比不落默认。
            $noTieredSamples += Get-LaunchTime -ExePath $r2rExe -DataDir $dataDir -Label "r2r-notier-$i" -DisableTiered
        }
    }

    $baseMedian = Show-Stats -Samples $baselineSamples -Name 'JIT (ReadyToRun=off)     '
    if ($fwExe) { $null = Show-Stats -Samples $fwOnlySamples -Name 'R2R framework only       ' }
    $r2rMedian = Show-Stats -Samples $r2rSamples -Name 'R2R all assemblies       '

    if ($NoTiered) { $null = Show-Stats -Samples $noTieredSamples -Name 'R2R + no-tiered          ' }

    if ($baseMedian -and $r2rMedian) {
        $improved = [math]::Round(100.0 * ($baseMedian - $r2rMedian) / $baseMedian, 1)
        Write-Host "启动到主菜单中位数改善：${improved}%（基线 ${baseMedian}ms -> R2R ${r2rMedian}ms）"
    }

    $csv = Join-Path $ResultDir ("startup-{0:yyyyMMdd-HHmmss}.csv" -f (Get-Date))
    ($baselineSamples + $fwOnlySamples + $r2rSamples + $noTieredSamples) | Select-Object Label, StartupMs, WallMs | Export-Csv -Path $csv -NoTypeInformation -Encoding UTF8
    Write-Host "明细已写入 $csv"

    # 进局耗时：启动到主菜单之后的第二段成本，R2R 若只覆盖 framework 这一段收益会明显打折。
    if ($GameplayEnter) {
        Write-Host ''
        Write-Host "== 进局耗时（选歌界面按开始 -> Player 进入，各 $GameplayRuns 次）"

        $baselineEnter = @()
        $r2rEnter = @()

        for ($i = 1; $i -le $GameplayRuns; $i++) {
            Stop-GameProcess
            Start-Sleep -Seconds 3
            $baselineEnter += Get-GameplayEntryTime -ExePath $baseExe -DataDir $dataDir -Label "baseline-enter-$i"

            Stop-GameProcess
            Start-Sleep -Seconds 3
            $r2rEnter += Get-GameplayEntryTime -ExePath $r2rExe -DataDir $dataDir -Label "r2r-enter-$i"
        }

        $baseEnterMs = @($baselineEnter | Where-Object { $_.Segments.TotalMs } | ForEach-Object { [pscustomobject]@{ StartupMs = $_.Segments.TotalMs } })
        $r2rEnterMs = @($r2rEnter | Where-Object { $_.Segments.TotalMs } | ForEach-Object { [pscustomobject]@{ StartupMs = $_.Segments.TotalMs } })

        $null = Show-Stats -Samples $baseEnterMs -Name 'JIT 进局 (ReadyToRun=off)'
        $null = Show-Stats -Samples $r2rEnterMs -Name 'R2R 进局 (ReadyToRun=on) '

        foreach ($s in ($baselineEnter + $r2rEnter)) {
            $seg = $s.Segments
            if ($seg.TotalMs) {
                Write-Host ("  {0,-20} start={1} loader={2} gate={3} player={4} total={5}ms" -f $s.Label, $seg.StartMs, $seg.LoaderMs, $seg.GateMs, $seg.PlayerMs, $seg.TotalMs)
            }
            else {
                Write-Host ("  {0,-20} 未抓到进局分段（窗口未前台 / Enter 未送达）" -f $s.Label)
            }
        }

        $enterCsv = Join-Path $ResultDir ("gameplay-enter-{0:yyyyMMdd-HHmmss}.csv" -f (Get-Date))
        ($baselineEnter + $r2rEnter) | ForEach-Object {
            $seg = $_.Segments
            [pscustomobject]@{
                Label    = $_.Label
                StartMs  = $seg.StartMs
                LoaderMs = $seg.LoaderMs
                GateMs   = $seg.GateMs
                PlayerMs = $seg.PlayerMs
                TotalMs  = $seg.TotalMs
            }
        } | Export-Csv -Path $enterCsv -NoTypeInformation -Encoding UTF8
        Write-Host "进局明细已写入 $enterCsv"
    }

    # 内存对比：R2R 是「JIT code heap(私有) 换文件映射」，净效果方向不定，必须实测。
    # 采样时点定在主菜单稳定后（与启动收益同一时点，便于并排比较）。
    if ($Memory) {
        Write-Host ''
        Write-Host "== 内存占用（主菜单稳定后 +$MemorySettleSeconds s，各 $Runs 次）"

        $memBase = @()
        $memFw = @()
        $memR2r = @()

        for ($i = 1; $i -le $Runs; $i++) {
            Stop-GameProcess
            Start-Sleep -Seconds 3
            $memBase += Get-MemorySnapshot -ExePath $baseExe -DataDir $dataDir -Label "baseline-$i"

            if ($fwExe) {
                Stop-GameProcess
                Start-Sleep -Seconds 3
                $memFw += Get-MemorySnapshot -ExePath $fwExe -DataDir $dataDir -Label "fwonly-$i"
            }

            Stop-GameProcess
            Start-Sleep -Seconds 3
            $memR2r += Get-MemorySnapshot -ExePath $r2rExe -DataDir $dataDir -Label "r2r-$i"
        }

        $null = Show-MemoryStats -Samples $memBase -Name 'JIT (baseline)'
        if ($fwExe) { $null = Show-MemoryStats -Samples $memFw -Name 'R2R framework only' }
        $null = Show-MemoryStats -Samples $memR2r -Name 'R2R all assemblies'

        $memCsv = Join-Path $ResultDir ("memory-{0:yyyyMMdd-HHmmss}.csv" -f (Get-Date))
        ($memBase + $memFw + $memR2r) | Export-Csv -Path $memCsv -NoTypeInformation -Encoding UTF8
        Write-Host "内存明细已写入 $memCsv"
    }

    if ($WithJit) {
        foreach ($samples in @($baselineSamples, $r2rSamples)) {
            $sample = $samples | Where-Object { $_.JitFile } | Select-Object -First 1
            if (-not $sample) { continue }

            $jit = Get-JitSummary -JitFile $sample.JitFile
            if ($jit) { Write-Host ("{0}: JIT methods={1} totalJitMs={2}" -f $sample.Label, $jit.Methods, $jit.TotalMs) }
        }

        Write-Host '提示：JIT 时间报告由 runtime 在进程退出时写出，读数含整个进程生命周期，只用于对比两侧量级。'
    }

Stop-GameProcess
Remove-Item Env:\EZ2LAZER_STARTUP_TRACE -ErrorAction SilentlyContinue

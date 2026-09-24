# Ez2Lazer 性能 / FPS 排查（汇总活文档）

> **用途**：集中记录 **帧率下降、卡顿、性能测试口径** 三类内容，供以后再遇到掉帧时直接查历史结论，避免重复排查。
> **范围**：只收「性能与 FPS」相关描述。判定语义、数据来源、存储结构等仍留在各自文档，见 §8。
> **跨仓库**：`osu`（游戏侧）与 `osu-framework`（渲染 / 音频线程）都在本文件登记，框架侧改动标注 commit。
> 注意：文档中记录的内容并不代表仓库当前状态，可能已被后续改动覆盖。请以实际代码为准。

---

## 1. 观测口径（先统一术语再讨论数字）

| 指标 | 打开方式 | 含义 | 常见误读 |
|------|---------|------|---------|
| FPS 计数器 | 游戏内右下角 | Draw / Update 帧率 | 峰值意义有限，看**稳定态**与**相对差值** |
| `VerticesUpl` / `UniformUpl` | `Ctrl+F11` Frame Statistics | 每帧上传 GPU 的顶点 / uniform **次数** | **不是耗时**；高 Upl 不等于慢 |
| `FBORedraw` | `Ctrl+F11` | 离屏 buffer（`BufferedDrawNode`）重绘**次数** | 密集峰值常来自毛玻璃 / EdgeEffect，不必然是瓶颈 |
| `SwapBuffer` | `Ctrl+F11` | 交换缓冲耗时 | 高值多为**呈现 / 驱动 / 等待**，不是自身绘制变重 |
| `Work` | `Ctrl+F11` | 该线程本帧实际工作耗时 | **后台 Task 的开销不会计入**主线程 Work |
| 活动 Track 数 | `Ctrl+F9` Audio Mixer Visualiser | 混音器上真实活动通道 | 用于判断 Track 是否叠播 / 未停 |

**基线纪律**：比较必须同一启动方式（IDE / 直接启动）、同一输出模式、同一皮肤，只改一个变量。

---

## 2. 已定案根因

| 现象 | 根因 | 落地修复 | 归属 |
|------|------|---------|------|
| **传统（共享）输出播放时掉 500–700 FPS，暂停立刻回升** | 音频线程随 `GameThread.DEFAULT_ACTIVE_HZ` 提到 **8000Hz**，`TrackBass.UpdateState` 每帧都跑 `BassAmplitudeProcessor`（电平 + FFT512），播放态每秒约 8000 次昂贵 BASS 查询 | 振幅分析独立限频为 `max(显示器刷新率, 120Hz)`，音频控制仍 8000Hz | `osu-framework` `e22805587` |
| 选歌界面停留 3–5 秒后掉帧 | `BackgroundDataStoreProcessor` 回填 + `RealmDetachedBeatmapStore` 的 Replace 风暴 | `StartupBackfillDelay` = 5s（测试可覆写 0）；`DetachedBeatmapStoreFrameBudget` 每帧 Drain ≤ 24 | `osu` |
| 打得越久越卡 | `Column.pressTimes` 整局无限增长，被动 Miss 时复制整表查最近邻 | `pressTimes` 有界裁剪 + `ManiaDrawableMissTiming` 零分配 | `osu` |
| 列数 / LN 越多越卡 | 每个存活 drawable 每帧进 automiss 询问 | automiss 迁到 Column late-deadline 队列，每列每帧一次 poll；删除每 drawable 虚分派 | `osu` |
| LN 按住时掉帧（Triangles/Default） | `DefaultBodyPiece` 两层减法 FBO：按住缩体每帧 `DrawSize` 失效 → `ForceRedraw` | **LN-HOLD-FBO**：按住隐藏减法层并跳过重绘；未按住仍保留内孔 | `osu` |
| LN 同屏时每条 hold 的 head/tail 空实现占输入槽 | `OnPressed`/`OnReleased` 为空，父 Hold 已处理按键 | **LN-INPUT-SLOT**：默认不进非位置输入队列 | `osu` |
| 高 KPS 下 alloc 抬高 | `GetHitModeValidHitResults` 每次 `new[]`，经 `ResultFor` / `SelectFold` 放大 | 改静态表，`ResultFor` 零分配（Combo dense 约 1.8 KB → 45 B/press） | `osu` |
| 启动期大量谱面时 GC 爆炸、持续卡顿 | 全量 detached beatmap cache + 启动同时做核心分析与标签补算 | 见 [`EZ_ANALYSIS_STORAGE_REDESIGN.md`](./EZ_ANALYSIS_STORAGE_REDESIGN.md)：分析 / 标签 / 写库边界分离 | `osu` |
| 皮肤交互导致帧率与 GC 抖动 | 热路径创建绑定副本、频繁 `BindTo`/`Unbind`、绑定链上 `SkinInfo.TriggerChange()` | 见 [`EzSkinSystemNotes.md`](./EzSkinSystemNotes.md) 的禁止项 | `osu` |
| 高 KPS / 多键齐按时输入与取样开销随按键数线性放大 | `RulesetKeyBindingContainer.KeyBindingInputQueue` 每次枚举都重建整棵 mania 子树的输入队列（原实现读 2–3 次），且同帧内每个 binding 各建一次；按键预览取样每次按键走 LINQ（`Where`/`MinBy`/`OrderBy`/`SkipWhile`）与递归迭代器 | 队列按帧物化一次；`GetMostValidObject` 改单遍最小扫描 + 显式栈先序 | `osu` |

---

## 2.1 2026-09-20 输入 / 取样热路径

### 已落地

| 项 | 位置 | 说明 |
|----|------|------|
| **INPUT-QUEUE-FRAME** | `ManiaInputManager.ManiaKeyBindingContainer` | 一次枚举分成「列 / 非列」两个复用列表（列优先顺序不变），并在 `Update()` 失效以**按帧缓存**。原先每次读 `base.KeyBindingInputQueue` 都清空重建，一帧内 N 键齐按 = N 次重建；现在一帧一次。`keyBindingQueues[binding]` 的既有语义（仅在空时重建、Release 时清空）未改动 |
| **SAMPLE-NO-LINQ** | `GameplaySampleTriggerSource` / `DrawableManiaHitObject` | `GetMostValidObject` 去掉 `Where`/`MinBy`/`OrderBy`/`SkipWhile` 与递归 `getAllNested`，改单遍最小扫描 + 显式栈先序（并列取舍规则与上游一致）；`Play()` / `PlaySamples()` 手动填充数组代替 `Cast().ToArray()`。数组仍每次新建，因为 `GameplayState.ApplySamples` 靠引用不等触发绑定变更（`StoryboardTriggerController` 消费 `LastPlayedSamples`） |
| **LN-INPUT-SLOT** | `DrawableHoldNoteHead` / `DrawableHoldNoteTail` | 空实现移出非位置输入队列。父 Hold 仍处理 press/release |
| **LN-HOLD-FBO** | `DefaultBodyPiece` | 按住隐藏减法层并跳过 `ForceRedraw`；松手恢复。仅 Default/Triangles |
| **FW-BUTTON-QUEUE-REUSE** | `osu-framework` `ButtonEventManager<TButton>`（`osu-framework` `56e9beb2c`） | 每个按钮各持一份按下队列缓冲：按下时整体重填（替代 `InputQueue.ToList()`），抬起时就地压缩掉已脱离输入树的项（替代 `Where(...).ToList()`）。队列长度 = 整棵输入子树的非位置输入项，原实现每按一次分配一条等长列表 + 一个 LINQ 迭代器。快照语义不变（仍取本次按下时的队列，抬起按同一快照派发） |

### 已评估但**不做**（附原因，避免重复讨论）

| 候选 | 结论 |
|------|------|
| `Column.OnPressed` 返回 `true` 让 `PropagatePressed` early-out | **不可行**。`PropagatePressed` 成功后 `inputQueue.RemoveRange(...)` 会截断该 binding 的**同一条缓存列表**，而 `handleNewReleased` 用同一条列表派发 Release。列在队列头部 ⇒ `ReplayRecorder` / `KeyCounterActionTrigger` 收不到 Release（回放帧丢 KeyUp、按键显示粘键）。要做得先把观察者排到列之前并按类型识别，收益不抵风险 |
| **FW-INPUT-QUEUE-DISPATCH**：一次输入派发窗口内共享一次队列构建（框架侧） | **不可行（已被测试证伪，改动已撤回）**。前提「窗口内可进入队列的 drawable 不变」不成立：`TestSceneInputQueueChange.CombinedClicks` 在共享构建下必失败——drawable 在 `OnMouseDown` 里 `MoveToX`（duration 0，同步生效）后，同帧第二次按键必须看到**按新位置重建**的队列，否则再次命中已移开的 box（`HitCount == 2`）。同类情形还有事件处理中隐藏 / 移除 drawable、焦点改变非位置队列末位优先级。按调用点重建是刻意的新鲜度语义，不可跨事件共享；框架侧只保留去分配（见 **FW-BUTTON-QUEUE-REUSE**） |
| 去掉通道级 `BindAdjustments`（每击 4 路 `AddSource`） | **不可行（Ez 侧）**。`PlaybackConcurrency` / 音量 / 平衡 / 频率都必须跟随 `drawableRuleset.Audio`，其中频率来自 `ModRateAdjust` 系列（`AddAdjustment(Frequency, SpeedChange)`）；`AdjustableAudioComponent.Adjustments` 是 `protected internal`，Ez 无法只绑部分属性。真正做法是框架级通道复用（BASS 通道无法重播，见 `SampleChannelBass.playInternal` 的 `Played` 守卫） |
| `Column.OnPressed` 每键都 `sampleTriggerSource.Play()` | 已有代号 **SOUND-DECOUPLE**（`HIGH_KPS_JUDGE_BACKLOG.md`），**附条件**：仅在 profile 证明其阻塞时做。`KeySoundPreviewMode.Off` 目前也会触发取样（只有 `AutoPlayPlus` 排除），会让按键与 note 音互相 choke |
| 爆炸池 `DrawablePool<PoolableHitExplosion>(5)` 扩容 | **无依据**。爆炸按列产生，每列一次按键最多 1 个，初始容量 5 已覆盖单次按键；仅在单列 >25 hit/s 的极端 jack 下才会增长。首击掉帧不是它 |

---

## 2.2 2026-09-23 局内判定 / HUD 热路径

### 已落地

| 项 | 位置 | 说明 |
|----|------|------|
| **HITPOS-CACHE** | `DrawableManiaRuleset` | `updateTimeRange()` 原先**每帧**调 `skinChanged()`，即每帧构造 `ManiaSkinConfigurationLookup` 走一次皮肤配置链。改为 `updateHitPosition()` 只在皮肤 / `HitPosition` / `HitPositionGlobalEnable` 变更时重算并缓存字段，`updateTimeRange()` 只做算术。判定线绑定前置到 `ScrollStyle` 之前，保证首次 `updateTimeRange()` 读到已算好的值 |
| **JUDGE-NO-CLOSURE** | `Column.OnNewResult` / `Stage.OnNewResult` | `hitExplosionPool.Get(e => e.Apply(result))` 与 `judgementPooler.Get(type, j => j.Apply(result, judgedObject))` 每判定各造一个捕获 `result` 的闭包。两者 `Apply` 都只做字段赋值（真正动画在下一帧 `PrepareForUse`），改为 `Get()` 后再赋值。注意 `JudgementContainer.Add` 会**同步**读 `JudgedHitObject`，赋值必须仍在 `Add` 之前 |
| **POLICY-SCRATCH** | `OrderedHitPolicyHelper` | 候选 / 后判对象改复用缓冲（去掉每次判定的 `ToList()` 拷贝）；`OrderBy(...).ToList()` 改复用缓冲上的稳定插入排序；诊断串整段门控在 `EzJudgmentDiagEnabled` 之后（关闭时不再执行 `describe` / `string.Join`）；命中模式与诊断开关缓存 bindable，取代一次判定里 3 次 `ezConfig.Get` |
| **FORCEMISS-SNAPSHOT** | `Column.handleHit` / `ManiaLaneController.CollectForceMissBefore` | 命中时「提前判 miss」的 `yield` 迭代器改为写调用方缓冲（每次命中省一个迭代器对象），处理时按实时 `IsPressJudged` 复核以保持惰性枚举语义；`EnumerateForceMissBefore` 无调用方，已由新方法取代 |
| **MARKER-EASE-FIX** | `EzHUDHitTimingColumns.moveMarker` | `marker.Y = targetY;` 紧接着 `MoveToY(targetY, 800, OutQuint)`：设值后新变换的 `StartValue` 由 `ReadIntoStartValue` 在首次 `Apply` 时读到（`TransformCustom.cs:186`），即 `start == end`，缓动恒为空变换 ⇒ 标记瞬跳。这行是从上游 `BarHitErrorMeter` 那条**一次性池化判定线**（对象是新的，直接赋值才对）抄过来的，常驻 marker 不该有。删掉后恢复成上游 `arrow`（同一 EMA、同一 `800ms OutQuint`）的「只发缓动」语义。附带两处交互：`MoveHeight` 改高时先 `ClearTransforms(false, "Y")` 再按比例赋值（在途变换每帧回写自己的插值，不清就会覆盖赋值、拖滑块时标记卡在旧范围）；`StopMovement` 开启时 `FinishTransforms(false, "Y")` 收尾到目标，而不是停在中间插值上 |

### 已评估但**不做**（附原因，避免重复讨论）

| 候选 | 结论 |
|------|------|
| 缓存 `DrawableManiaHitObject.PlaySamples` 的 samples 数组 | **不可行**。`Bindable<T>.Value` setter 在 `EqualityComparer<T>.Default.Equals` 相等时直接 return（数组即引用比较，`osu-framework/…/Bindable.cs:99`），缓存同一实例会让 `GameplayState.LastPlayedSamples` 对重复同音不再触发变更，而 `StoryboardTriggerController` 正消费它。数组必须每次新建（同 **SAMPLE-NO-LINQ**） |
| HUD / 皮肤件「复用单个 `TransformSequence`」合并按键、判定动画 | **不可行（框架约束）**。`TransformSequence<T>` 在构造时固化 `startTime`/`currentTime`（`TransformSequence.cs:53`），复用实例再 `Append` 会把 transform 排到过去时刻 ⇒ 变成瞬跳而非动画；且 `PopulateTransform` 对同一 `Transform` 实例二次调用直接抛异常（`TransformableExtensions.cs:154`），即 transform 天然一次性、框架无池。单次 `MoveToY` 约 3 个小对象（`TransformSequence` + 其 1 槽 list + `TransformCustom`；`TransformID` 是 `ulong`，不产生字符串），`Ez2KeyAreaPlus` 里对 `Container<Circle>` 的 `foreach` 走结构体枚举器、本就不分配 |

---

## 2.3 lane controller 索引维护是否需要改结构（2026-09-23 实测结论：不改）

口径：`ManiaLaneHotPathMicroBenchTest`（跑真实 `ManiaLaneController`，10 列 × PeakKps 100 × alive 40 × 2000 帧，含 Select + automiss deadline 队列 + `pressTimes`）。

| 指标 | 实测 | 判读 |
|------|------|------|
| 总耗时 | 21–33 ms / 2000 帧（Earliest / Combo / Duration 分别 33 / 28 / 27 ms） | ≈ 10–16 µs/帧，约为 16.6 ms 帧预算的 0.1% |
| 分配 | 15–128 B/press，`gen0=0` | 不构成 GC 压力 |
| automiss 队列 | 每帧每列 1 次 poll，`dueVisits` ≈ 22/poll（alive 40） | 已含在上面耗时里，不是瓶颈 |

结论：`insertEntryAt` / `Unregister` / `autoMissEntries` 的 O(n) 维护即使在 100 KPS / 40 存活每列的极端设定下也不进热榜，**不为它改数据结构**。BMS poor-select（`AllowBmsFallbackToEarliest` + `PoorEnabled`）同样 30 ms 量级、128 B/press，无需单独优化。

---

## 3. 2026-08-08 音频后端排查记录

**起点现象**：启动后前 3–5 秒 Upl 极高、约 60 FPS，随后回到数百 FPS；稳定态选歌与局内仍有密集 FBO 峰值；每次启动稳定帧不一致（600 / 900 / 1000+）。

### 3.1 测试矩阵

| 启动方式 | 输出模式 | 首页稳定 | 进选歌后立即返回 | 选歌停留 >5s 后返回 | 播放 / 暂停差值 |
|---------|---------|---------|----------------|-------------------|---------------|
| IDE | 默认共享 | 1500–1700 | 有下降 | **900–1000（持续）** | 约 200 FPS |
| 直接启动 | 默认共享 | 1800+ | 1300–1400 | 无法复现持续低帧 | 约 200 FPS |
| 直接启动 | ASIO | 1700 → 1900 | — | — | 首次 +200，其后播放/暂停均约 1900 |
| 直接启动 | 传统（非 ASIO / 非独占） | 1200 → 回升 1400 | — | — | **暂停 1800–1900，播放 1200–1400** |

补充观测：

- ASIO 的 **PCM 开关**（内部 / 外部 PCM）无明显差异 → 排除外部 PCM 分支。
- ASIO 暂停后恢复播放**声音正常**，1900 FPS 不是「没出声」造成的假象。
- 首页等待 5 秒以上还能再涨几十 FPS（暖机 / 后台收尾）。
- 无论是否在播放，进入选歌都会重新触发播放，因此「进选歌是否重启 Track」不是有效变量。

### 3.2 判读

传统共享输出在 **播放态** 才产生大额外开销、暂停立即恢复，指向「按音频线程频率反复执行的播放态查询」，而非解码或设备初始化。ASIO 走拉取式解码混音，同样 8000Hz 下代价低得多，因此只表现为首次暖机差值。结论落到 `BassAmplitudeProcessor`：电平与 FFT512 只服务可视化（`OsuLogo`、`LogoVisualisation`、`MasterGameplayClockContainer` 等消费 `CurrentAmplitudes`），却被绑在 8000Hz 的音频帧上。

### 3.3 已排除项（同一复现流程下验证无效）

- 关闭毛玻璃 UI（含保持关闭后冷启动）
- 切换到无 HUD 皮肤
- 菜单背景换成静态图片
- ASIO PCM 开关
- 「代码皮系统性更慢」假设：Race 关闭后未能单独成立，`Masking` / `BufferedContainer` / `EdgeEffect` 消融开关见 `ManiaCodeSkinDrawAblation`

### 3.4 待复测

- 传统共享输出在振幅限频后，播放 / 暂停差值是否收敛到几十 FPS 量级。
- **IDE 启动特有的持续低帧**（900–1000）：直接启动无法复现，仍疑为 IDE 宿主开销（`Debug.Print`、附加调试器、进程优先级），尚未定案。
- 选歌返回后相对首页仍有 1800 → 1300–1400 的下降，与音频限频是否相关待分轨确认。
- 稳定态密集 FBO 峰值是否影响实际帧时，尚无结论。

---

## 4. 8000Hz 线程频率的取舍

fork 将 `GameThread.DEFAULT_ACTIVE_HZ` 从上游 1000 提到 **8000**（`524d84976` / `9e2b63366`），目的是降低局内音乐播放与输入调度延迟，**不因性能问题回调**。

由此产生的纪律：**凡是只服务显示的音频 / 统计分析，都必须独立限频，不得跟随音频线程频率**。当前已限频项：`BassAmplitudeProcessor`（电平 + FFT512）。

持续音乐的实际输出延迟主要由 ASIO / WASAPI 缓冲区决定；8000Hz 主要收益在播放、暂停、参数变更等**指令调度等待**。

---

## 5. 复现流程（掉帧类问题标准步骤）

1. **绕过 IDE 直接启动**客户端，先记录首页稳定 FPS。
2. 进入选歌后**立即**返回首页，记录 FPS。
3. 再进入选歌**停留 5 秒以上**后返回首页，记录 FPS 与是否持续。
4. 在首页用音乐控制器**暂停 / 恢复**播放，记录差值。
5. 切换输出模式（ASIO / 独占 WASAPI / 传统共享）重复 4。
6. 需要看音频通道时用 `Ctrl+F9` 比较各阶段**活动 Track 数**；确认「按键音效预览模式」处于关闭。

每次只改一个变量；IDE 启动的数据**不可**与直接启动的数据横向比较。

---

## 6. 性能测试与 bench 索引

| 名称 | 覆盖 |
|------|------|
| `BenchmarkManiaReplaySession.BenchmarkRunHitEventsAsync` | 补 HitEvents 吞吐（p50/p95，目标参考 <10ms） |
| `ManiaRunHitEventsLatencyTest` | 暖机延迟烟测 |
| `BenchmarkManiaLaneHotPath` / `ManiaLaneHotPathMicroBenchTest` / `ManiaLaneHotPathWorkload` | 10 列 × PeakKps 20/50/100 × alive 8/24/40；Select + 真实 automiss deadline 队列 + `pressTimes`；**不含** SwapBuffer |
| `ManiaAutoMissDeadlineTest` | future-deadline `dueVisits == 0` |
| `TestSceneManiaHoldDrawCost` / `ManiaHoldAblationTest` | **Debug only**。LN 消融：减法描边 / tick 扫描 / tick 生成 / head-tail 入队；滚动 + 模拟按住 |
| `HitModeValidResultsAllocTest` | `ResultFor` 零分配 |
| `DetachedBeatmapStoreFrameBudget` 单测 | 每帧 Drain ≤ 24 |
| `BackgroundDataStoreProcessor` 测试覆写 | `StartupBackfillDelay` 可置 0 |

性能改动的黄金标准不变：不得破坏 `TestSceneReplaySessionParity` / `ManiaCrossSourceInvariantTest` / `ManiaJudgePrecedenceParityTest`。

---

## 7. 架构基线

- 月度架构基线锁定 `702be7` / `2026.614.0`；`ae471f` 仅为当日批次点，不作基线。
- 「关 Race 冷启动约 500 FPS（历史约 1300）」的二分基线为 `f161089f75^`。
- 显卡驱动崩溃后整体约 500 FPS 属独立故障，不纳入本文件分析。

---

## 8. 相关文档

- 高 KPS 判定优化 backlog：[`HIGH_KPS_JUDGE_BACKLOG.md`](./HIGH_KPS_JUDGE_BACKLOG.md)
- 判定总拓扑与批次：[`MANIA-JUDGEMENT-TOPOLOGY.md`](./MANIA-JUDGEMENT-TOPOLOGY.md)
- 局内判定叙事：[`MANIA-JUDGEMENT-RUNTIME.md`](./MANIA-JUDGEMENT-RUNTIME.md)
- 分析存储与启动期 GC：[`EZ_ANALYSIS_STORAGE_REDESIGN.md`](./EZ_ANALYSIS_STORAGE_REDESIGN.md)
- 皮肤系统热路径纪律：[`EzSkinSystemNotes.md`](./EzSkinSystemNotes.md)

---

## 9. 局内掉帧待排查（2026-09-20 登记）

三条现象来自实机观察，**均未定案**；LN 相关项按用户要求先量后改。

| 现象 | 已知线索 | 下一步（先量） |
|------|---------|---------------|
| 进局后**首次命中** update 与 draw 同时明显掉帧，随后回升，画面无卡顿 | 单次性 ⇒ 首次执行成本：JIT、判定字形图集首次上传、首个 BASS 通道创建、判定动画首次 `GetAnimation` | 用 `ManiaJudgeHotPathTrace` 标出首次 `CheckForResult` → `ApplyResult` → 爆炸/判定的时间线；对照把 `KeySoundPreviewMode` 关掉再复测（音频侧变量隔离） |
| **LN 多的场景掉帧尤为明显** | **已定案（Triangles/Default）**：按住缩体时 `DefaultBodyPiece` 两层减法 FBO 每帧 `ForceRedraw`。静止 / 只滚不按：`ΔBodyFbo→0`。tick 扫描抬 `ΔTickScan`，12 条 LN 下 `updMs` 仍约 1–2ms，不是画侧主项。head/tail 入队已排除 | 生产：`LN-HOLD-FBO` + `LN-INPUT-SLOT`。消融场景仅 Debug。**Argon / Ez2 / Legacy 未改** |
| 其他显示器播放视频（即使暂停）时帧率掉 200+ | 游戏内代码路径无对应开销 ⇒ 指向桌面合成 / GPU 抢占 / DWM 或驱动侧 | 与 §5 流程同规：另一显示器换静态图、换浏览器硬件加速开关、换输出模式，确认是否与游戏进程无关；结论记入本文件而非改游戏代码 |

**输入侧已排除**：`DrawableHoldNoteBody` / `DrawableHoldNoteTick` 不是 `IKeyBindingHandler`，本就不进输入队列，LN 的 tick 数量不放大按键扫描成本（当前算法扫描列 + note + hold + head/tail）。

---

## 修订记录

| 日期 | 说明 |
|------|------|
| 2026-08-08 | 初版：汇总各文档 FPS / 性能测试描述；记录音频后端排查与振幅限频（框架 `e22805587`） |
| 2026-09-20 | §2.1：输入队列按帧物化、取样去 LINQ、框架侧按键队列去分配（**FW-BUTTON-QUEUE-REUSE**）落地；登记 5 项「评估后不做」的候选与原因（含被 `TestSceneInputQueueChange.CombinedClicks` 证伪的 **FW-INPUT-QUEUE-DISPATCH**）；§9 登记首次命中 / LN / 多显示器三条待排查现象与测量口径 |
| 2026-09-21 | **LN-HOLD-FBO** / **LN-INPUT-SLOT** 生产落地。消融证实按住才 ForceRedraw；观测代码 `#if DEBUG` 剥离 |
| 2026-09-23 | §2.2/§2.3：局内判定与 HUD 热路径去分配（**HITPOS-CACHE** / **JUDGE-NO-CLOSURE** / **POLICY-SCRATCH** / **FORCEMISS-SNAPSHOT**），并记录 3 项「评估后不做」（samples 数组缓存、transform 序列复用、`moveMarker` 手写插值）与 lane controller 索引维护「实测不改」结论 |
| 2026-09-24 | §2.2：**MARKER-EASE-FIX**——`EzHUDHitTimingColumns` 的判定标记恢复为真正的缓动（去掉把缓动变成空变换的直接赋值），并补上 `MoveHeight` / `StopMovement` 与在途变换的两处交互；原「`moveMarker` 手写插值」候选按「保留框架缓动」结案，不再列为待办 |

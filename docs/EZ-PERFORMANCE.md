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

## 2.4 2026-09-24 按键延迟实测（局内 probe：结论是「不是热路径问题」）

工具：`EzPressLatencyDiagnostics`（按键分段）+ `EzFrameStallDiagnostics`（帧级 stall）+ 配套分析脚本
`AnalyzePressLatency.ps1` / `AnalyzeFrameStall.ps1`。开关：`EZ_JUDGMENT_PROBE`（总开关，落 `diagnostics/`）、
`EZ_FRAME_PROBE_MS`（stall 阈值）、`EZ_FRAME_PROBE_LIGHT`（关掉每帧 GC/分配读数）、
`EZ_PRESS_PROBE_SKIP_FORCE_MISS`（消融强制 miss 扫描）。**这些只在诊断开启时生效。**

#### 2.4.1 本局条件（先看这个，否则数字会被误读）

| 项 | 实测 |
|----|------|
| 更新帧数 / 跨度 | 194,002 帧 / 98.6 s ≈ **1968 更新帧/s**（`FrameElapsedMs` p50 = 0.474 ms，隐含 p50 **2110 fps**） |
| 按键 | 1068 次 / 89.2 s ≈ 12 次/s；空按 146（13.7%）；多键帧 102 |
| 帧耗时 >2 ms / >5 ms | 1 / 0 帧 |
| GC 暂停累计 | 1565 ms（占 89.2 s 的 1.75%，含后台 GC，是上界）；gen2 = 0 |

**这不是高压场景。** 更新线程相对 16.6 ms 帧预算有约 35× 余量，因此本局任何亚毫秒数字都不能用来解释体感 4 ms。

#### 2.4.2 已定案

| 结论 | 证据 |
|------|------|
| **`PreColumnMs` 不是代码开销，是「等下一帧来处理这条输入」** | 空按与真判定的等待**相同**：`routed=0` p50 **0.625 ms** / `routed=1` p50 **0.558 ms**；而两者实际工作量差 20 倍（`ColumnMs` 0.005 vs 0.103 ms）。等待与 `FrameAgeMs` 强相关：age 0–2 ms → `PreColumnMs` p50 **0.557 ms**，age 2–5 ms → **1.830 ms**。结论：随帧节奏变化，不随代码量变化 |
| **按键自身的 inline 工时极小且不随物量增长** | `ColumnMs`（`OnPressed` 全程，含判定 + 同步结果扇出）p50 **0.101 ms** / p90 0.134 / max 1.127；空按 p50 **0.005 ms**。列内条目 0–5 都有样本，未随 `Entries` 增长 |
| **按键帧确实更长，但大头不在判定代码里** | 按键帧 p50 1.150 ms vs 非按键帧 0.450 ms（+0.70 ms，P(>1 ms) 27.4×）；而 `PressColumnMs / ElapsedMs` 仅 p50 **6.2%**。差额落在本帧其他位置（判定落地后的 sprite/HUD、`Schedule` 出去的取样播放、draw），不是 `OnPressed` |
| **大停顿与按键无关，且本局几乎没有** | >8 ms 的帧只有 3 个，全部 `presses=0`，`FrameIdx` = 2、194002、194003 ⇒ **开局首帧与退出帧**。1.5–3 ms 段 GC 占 41–48% |
| **按键帧的 `max` = 3.851 ms、P(>5 ms) = 0** | 按键造成的帧长有上界，不制造真正的大卡顿 |

#### 2.4.3 埋点自身的两个坑（登记以免重犯）

1. **只在 >阈值的帧上做「按键帧 vs 非按键帧」对比会得出错误结论。** 条件在 `ElapsedMs > 1.5 ms` 上会让两组都被拉平（实测两组 p50 只差 0.02 ms），从而误判「按键与慢帧无关」。正确做法是双直方图 —— 记录**全部**帧并按键存在与否分组（已实现在 `EzFrameStallDiagnostics.FormatSummary`）。
2. **`SincePrevFrameMs` 的零点在上一帧边界，不是「本帧按键前的工作量」。** 它含上一帧剩余时间、draw/present 与帧间等待。只有 `PressColumnMs` 是按键自身工作。该字段原名为 `FirstPressAtMs` 并曾被误读为派发开销，已改名。
3. **`currentFrameStartTimestamp` 曾滞后一整帧。** `RecordFrame()` 在帧末写入它，而 `NotifyPress` 在同一个帧内、`RecordFrame` **之前**执行，因此当时读到的是**上上帧**的边界。修正前 `SincePrevFrameMs` 被虚增约一个帧长（实测 1.330 ms vs 帧长 1.344 ms，并让三段切分出现 **−0.171 ms** 的负值）。修正是把 `= prev` 改成 `= now`：`now` 才是下一次 `NotifyPress` 之前可读到的最新边界。**修好前不要用那一行做结论**（`PreColumnMs`/`FrameAgeMs` 来自另一个探针，不受影响）。

#### 2.4.4 明确不再重开的两个方向

- **输入队列整树重建**：§2.1 的 `INPUT-QUEUE-FRAME` / `FW-BUTTON-QUEUE-REUSE` 已落地；框架侧共享构建被 `TestSceneInputQueueChange.CombinedClicks` 证伪（`FW-INPUT-QUEUE-DISPATCH`），不再提。
- **lane controller 结构**：§2.3 已实测不改。

#### 2.4.5 下一步（要复现体感，先满足条件）

本局帧率远超体感阈值，所以结论只能是「热路径不是原因」。要解释体感 4 ms，必须先在**能复现的条件**下测，并把条件一并记录（当前 probe 不记录这些）：

- 帧率上限 / 垂直同步 / `FrameSync` 设置、分辨率与皮肤
- 谱面 KPS 与列数、音频输出模式（共享 / ASIO / 独占）
- 是否**当场感觉**延迟（否则无法判断指标是否代表体感）

在该条件下 `PreColumnMs` 才是体感延迟的主要成分（它 = 输入线程递送 + 等下一帧），此时该优化的是**帧节奏与输入递送时机**，不是判定代码。

#### 2.4.6 帧数上限与「下落不顺滑」同源（2026-09-24 追加，推翻 2.4.5 的取舍前提）

需求变更为 **不降帧、继续提帧，同时解决下落不顺**。据此重新读同一份数据，得到三条与 §2.4 不冲突但更重要的结论。

**一、没有任何限速器在起作用，1968 fps 就是纯工作量上限。**

`Unlimited` 实际被夹到 8000 Hz（`FrameSyncExtensions.applyLimit` 末行 `Math.Min(8000, limiter)`），而 `ThrottledFrameClock.throttle()` 只在工作量 < 1/8000 s 时才 sleep。实测 0.450 ms/帧 ⇒ `excessFrameTime` 为负 ⇒ `sleepAndUpdateCurrent` 直接返回 0，**一次都没睡过**。

| 项 | 值 | 推论 |
|----|----|------|
| 目标 | 8000 Hz（0.125 ms/帧） | 上限高于实得，不构成约束 |
| 实得 | 1968 帧/s（0.450 ms/帧 p50） | 每轮 update+draw 的实际耗时 |
| mania 判定 | ~10–16 µs/帧（§2.3 bench） | **占每帧不到 3%** |

⇒ 提帧只能靠砍每帧工作量，改任何设置都无效。同时也说明 §2.4 关心的判定热路径在这 450 µs 里根本不是大头。

**二、每帧持续分配 ~2 KB，与 20–60 ms 的卡顿鼓包吻合。**

`alloc(updateThread)` 388 MB / 98.6 s = **3.9 MB/s**，÷1968 帧 = **~2 KB/帧**（持续量，非突发）；gen0 **24.4 次/s** = 每 41 ms 一次，单次暂停 ~0.77 ms ⇒ **1.9% 的时间在 gen0 停顿里**。这同时解释两件事：帧数被压低，以及卡顿间隔分布的主鼓包落在 20–60 ms。

**三、note 位置按 update 帧量化，所以帧间隔抖动 = 下落抖动。**

`ScrollingHitObjectContainer.UpdateAfterChildrenLife` 每帧执行一次 `updatePosition(obj, Time.Current)`；`Time` 是 FSC 暴露的 `framedClock`，其 `manualClock.CurrentTime` 每轮 update 采样一次音频时钟（`applyFrameStability` 仅在偏差 > 20 ms 时夹取，正常帧不生效）。

⇒ 呈现出来的下落位置 = 音频时钟在**若干个离散 update 时刻**的采样值。帧间隔抖动直接变成下落位置的跳变，幅度 = 抖动 × 下落速度。**提帧数之所以能改善顺滑，正是因为它在缩短这个量化步长**——两个目标是同一个目标。

框架里已有 `InterpolatingFramedClock`，但它 (a) 只用于背景/故事板/Intro，未用于 gameplay；(b) `CurrentTime` 同样只在 `ProcessFrame()` 内更新，因此换成它也解决不了「呈现时刻采样」。故这条路不通，不必再试。

**新增探针**：`frameSplit` 行把一轮 update 切成三段并给出子树内分配 ——

| 段 | 含义 |
|----|------|
| `subtreeMsMean` | `base.UpdateSubTree()` 全程 = FSC 之下的 drawable 层级（播放区 / 物件 / 判定线） |
| `clockMsMean` | `updateClock()` = 音频时钟采样 + `ReplayInput` + 子帧校正 |
| `restMsMean` | 帧长减去上两者 = HUD / 框架调度 / 掩码 |
| `subtreeAllocMean` / `loopAllocMean` | 子树内、FSC 全程的每帧分配字节 |

这一行决定下一步往哪优化：子树占比高就改 mania 的 drawable 层级，占比低就去查 HUD 与框架。分析脚本 `AnalyzeFrameStall.ps1` 已有对应章节。

#### 2.4.7 首次 frameSplit 实测：**73% 的帧时间与 88% 的分配在 FSC 之外**（2026-09-24）

一局 mania，174,587 帧 / 91.0 s ≈ **1918 帧/s**，`mode=deep`，turbo 未确认。

| 项 | 实测 |
|----|------|
| 帧长 | 0.522 ms |
| **FSC 子树**（mania 播放区 / 物件 / 判定线） | **0.142 ms = 27.2%** |
| FSC 时钟推进（音频时钟采样 + ReplayInput + 子帧校正） | < 0.0005 ms（不可分辨，非瓶颈） |
| **其余**（HUD / 框架调度 / 掩码 / 其他 screen / overlay） | **0.379 ms = 72.6%** |
| 子树内分配 | **245 B/帧** |
| 全进程 update 线程分配 | 363.9 MB / 91 s = 4.0 MB/s ÷ 1918 ≈ **2.1 KB/帧** |

**推论：这 0.379 ms 与其中约 1.8 KB/帧的分配都在 FSC 之外。** 也就是说 mania 的播放区（成千上万个 note / lane / 判定线 drawable）只花 0.142 ms，而 HUD 加框架那部分花的是它的 2.7 倍、分配是它的 7.5 倍。**优化点不在 mania 的 drawable 层级。**

已验证的 FSC 之外每帧开销（框架侧，非主因但确凿）：`TooltipContainer.Update` → `CursorEffectContainer.FindTargets()` 每帧对 `InputManager.PositionalInputQueue` 做一次 `IndexOf` + 一次反向遍历（`findTargetChildren`），并在有候选时 `new List<TTarget>(...)` + `Reverse()`：

```125:142:osu-framework/osu.Framework/Graphics/Cursor/CursorEffectContainer.cs
        protected SlimReadOnlyListWrapper<TTarget> FindTargets()
        {
            findTargetChildren();
// ...
            if (targetChildren.Count == 0)
                return empty_list;
```
量级是 O(位置输入队列长度)，无候选时不分配；待 profile 确认它占多少。

**其余症状**（同一局）：

- 每帧 2.1 KB 的持续分配 ⇒ gen0 **≈23 次/s**（约每 43 ms 一次），单次暂停 ~0.8 ms（stall 帧上 `GcPauseDeltaMs` p50 = 0.801 ms）⇒ **1.82% 的时间在 GC 停顿里**，与卡顿间隔鼓包（20–60 ms）吻合。
- stall 帧（770 个）上 update 线程分配 p50 **32 KB**、p90 163 KB、max 716 KB —— 是平常帧的 16～340 倍，属突发；即「按键落地 → 结果扇出 → `Schedule` 出去的取样 / HUD」这一段。
- 770 个 stall 帧中 **266 个 GC 占比 ≥50%**、426 个 10–50%、只有 78 个 ≤10%。**GC 是多数 stall 的直接原因。**
- **GC 之外的较大 stall**：`Elapsed` 7–9 ms 而 `GcPause=0`、`alloc` 从 1.6 KB 到 166 KB 不等（t=88405 那条只有 1.6 KB 分配却卡 8 ms）。这类与分配无关，指向线程外因素（驱动 / 其他线程 / 阻塞）；91 s 内约 6 次，量级 0.07 次/s。
- 最大的一个 86.257 ms 在 `FrameIdx=2`（开局首帧，`iters=10` catch-up），不是稳态问题。

**下一步**：这 0.379 ms 与 1.8 KB/帧需要按调用栈定位，而不是继续按组件猜。两条路：外部 CPU / 分配 profiler（最快、最确定），或在 Ez 侧加按组件的计时包装。

**已做**（见 §2.4.8）：dotTrace Timeline 精确定位到 `CDXGISwapChain::Present` 是 Draw 线程最大单项，且该节的「72.6% 在 FSC 之外」在限速生效后绝大部分是**节流 sleep**，不是真实工作量。

#### 2.4.8 帧率上限对比实测：base 500/Limit4x（≈2000 fps）vs base 250/Limit4x（≈1000 fps）（2026-09-24）

§2.4.6 的「限制器不起作用、帧数就是纯工作量」前提在本轮已改变：配置改成 `FrameLimiterBase = 500` + 两线程均 `Limit4x` ⇒ 目标 2000 Hz，实测 1979 帧/s，**限制器已接管**（dotTrace 中 `WindowsNativeSleep.Sleep` 占 19%）。

**配置口径**（实测自 `F:\MUG OSU\EZ2OSU-lazer`）：`UpdateFrameLimiter` → `MaximumUpdateHz`，`FrameSync` → `MaximumDrawHz`，两个上限**各自独立**但共用基准倍率，`LimitNx` 一律取 `base × N`，末行 `Math.Min(8000, …)` 夹顶（`FrameSyncExtensions.applyLimit`）。故改帧数只需推 `FrameLimiterBase` 一条滑条、两个下拉都留 `Limit4x` —— 只降 draw 会让 update 空转产帧，反而抢核。

| 组 | base | 目标 | 实测 |
|----|------|------|------|
| A | 500 | 2000 Hz | 66,903 帧 / 33.8 s = **1979 fps** |
| B | 250 | 1000 Hz | 33,329 帧 / 33.4 s = **998 fps** |

##### 一、两局都不是 CPU 瓶颈（先立这个，否则下面的数字会被误读）

`frameSplit` 的 `elapsedMean` 几乎就是帧间隔本身（0.505 ms = 1/1979，1.001 ms = 1/998）⇒ 帧长由限制器给的 sleep 决定，不由工作量决定。

更关键：`restMsMean` 从 0.399 → 0.884 ms，**增量 0.485 ms，而帧间隔增量是 0.496 ms**（1.001 − 0.505）。`rest` = 帧长 − 子树 − 时钟，它跟着帧间隔等量涨，说明**它就是被节流的 sleep**。

⇒ 修正 §2.4.7 的判读：那里「72.6% 在 FSC 之外」的 0.379 ms 绝大部分是 sleep。真正逐帧恒定的只有 `subtreeMsMean`：0.105 ms（A）vs 0.116 ms（B），**跨倍帧率不变** —— 这才是 mania 播放区 / 物件 / 判定线的真实每帧成本。

##### 二、帧间隔规整度：1000 fps 明显更稳

| 指标 | A（2000 fps） | B（1000 fps） | |
|------|------|------|---|
| 帧间隔 p50 | 0.450 ms | 1.050 ms | |
| **p90 / p50** | **1.89** | **1.19** | ↓ 37% |
| p99 / p50 | 2.78 | 1.67 | ↓ 40% |
| p99.9 / p50 | 5.00 | 3.10 | ↓ 38% |
| **超 2× 目标的帧** | 2044 / 66602 = **3.07%** | 214 / 33031 = **0.65%** | ↓ 4.7× |

注意：1 ms / 2 ms 这类**绝对**阈值不能跨帧率比 —— B 组目标就是 1 ms，几乎每帧都「超 1 ms」。分析脚本里 `P(>1ms): 95.30% vs 66.61% → 1.4x` 那行因此无意义；跨帧率只能比**倍率**。

##### 三、按键→判定：中位数略升，尾部降 5 倍

| | A（2000 fps） | B（1000 fps） | |
|------|------|------|---|
| `PreColumnMs` p50 | 0.784 | 1.069 | +0.29 ms |
| `PreColumnMs` p99 | **35.445** | **6.764** | ↓ 5.2× |
| `PreColumnMs` max | 45.106 | 7.853 | ↓ 5.7× |
| `ColumnMs` p99 | 1.928 | 1.510 | |
| `ColumnMs` max | **33.492** | **2.120** | ↓ 15.8× |
| `FrameAgeMs` p99 | 36.876 | 7.612 | ↓ 4.8× |

多花 0.29 ms 中位延迟，换来尾部 5 倍的收窄。`ColumnMs`（按键自身 inline 工时）max 从 33.5 ms 掉到 2.1 ms，说明 **A 组的大延迟不是判定代码算得慢，而是被 stall 帧吞掉的** —— 与 §2.4 既有结论一致。

##### 四、稳态 ≥5 ms 卡顿：两组次数相同，这是帧率无关的地板

按 `FrameIdx` 剔除开局（<1000 帧）后，两组 **>5 ms 的帧都恰好 11 个**（≈0.33 次/s），与帧率无关 —— 符合「GC 由分配速率而非帧数驱动」。

差别在幅度：A 组稳态最大为 43.6 / 41.5 / 34.4 / 24.1 / 21.4 ms；B 组最大 18.9 / 10.3 ms，其余都在 5–10 ms。**同样次数，B 组鼓包更浅。**

A 组那个 131.5 ms 极值是 `FrameIdx=2`（开局首帧），B 组同位置是 32.0 ms —— 属开局，**不能拿它当 1000 fps 更顺的证据**。

`alloc(updateThread)` 速率 A 5.05 MB/s → B 3.16 MB/s（↓37%），但 `gcPause` 占比几乎不变（2.68% → 2.54%），说明 GC 停顿主要由稳态分配结构决定，不随帧率线性缩放。

##### 五、探针语义修正：`SincePrevFrameMs` 装了一个整帧

B 组 `pressSplit` 的 `sincePrevFrameMean = 1.542 ms`，而帧间隔只有 **1.001 ms** —— 该值大于一个帧间隔，证明它的零点是**处理按键那一帧的上一帧**边界，而非本帧。

⇒ 真实帧内相位 = `SincePrevFrameMs − 帧间隔`：

| | A | B |
|---|---|---|
| `SincePrevFrameMs` | 1.088 | 1.542 |
| 帧间隔 | 0.505 | 1.001 |
| **相位（差值）** | **0.583 ms** | **0.541 ms** |

帧率差一倍而两值几乎相同（0.54 / 0.58 ms），但按随机落点相位应正比于帧间隔（A 期望 0.25 ms、B 期望 0.50 ms）—— **A 组超出均匀落点可解释范围约 0.3 ms**。

⇒ 假设：输入路径存在一个 **≈0.55 ms、不随帧率缩放的固定投递延迟**（SDL 事件 → input 线程 → 队列 → 本帧 `InputManager.Update`）。这解释了 A 组 `PreColumnMs` 中位数为何无法继续变好。n≈300、单点对照，属**待证实**，与 `EzSubFrameCorrection` 的存在动机同源。

##### 六、结论与操作点

- **采用 base 250 + 两线程 `Limit4x`（1000 fps）**：帧间隔规整度 ↑、按键尾部 ↓5×，代价是中位延迟 +0.29 ms。对「下落顺滑」与「高 KPS 不莫名 miss」都是净改善。
- 继续降到 500 fps 收益会收敛：≥5 ms 卡顿次数是帧率无关地板，而中位延迟还要再 +1 ms，**不推荐**。
- 剩余真实杠杆只有两个、且都与帧率无关：**GC**（见 §2.4.9：它 62–69% 的暂停时间集中在慢帧里，故不能只看 2.5% 墙钟）与那个 **≈0.55 ms 输入地板**。

---

#### 2.4.9 GC 归因：分辨「GC 造成卡顿」与「GC 恰好同帧」（2026-09-24，修正 §2.4.8 的表述）

§2.4.8 把 GC 记为「约占 2.5% 墙钟」——数字没错，但**没说清它的形态**：那 2.5% 高度集中在慢帧里。deep CSV 逐帧记了 `GcPauseDeltaMs`（跨该帧的 `GC.GetTotalPauseDuration` 增量），可直接与 `ElapsedMs` 相关，**不需要 profiler**。

判据用探针文档自己给的那条：`GcPauseDeltaMs` 与帧长**同量级**才算 GC 造成；远小于帧长则**可排除** GC。下表只取稳态（`FrameIndex ≥ 1000`，与 §2.4.8 同口径），`ratio = GcPauseDeltaMs / ElapsedMs`。

| | A：base 500（1979 fps） | B：base 250（998 fps） |
|---|---|---|
| 稳态 stall 帧 | 529 | 962 |
| 该局 GC 总量（摘要读数） | 904 ms（2.68% 墙钟） | 848 ms（2.54% 墙钟） |
| **其中落在 stall 帧内** | **563 ms = 62.3%** | **581 ms = 68.6%** |
| >1.5 ms：ratio ≥50% / <10%（可排除） | 65.8% / 7.9% | 24.7% / 52.5% |
| >2 ms：ratio ≥50% / <10%（可排除） | 50.9% / 7.2% | 36.6% / 7.2% |
| >5 ms：ratio ≥50% / <10%（可排除） | **0% / 72.7%** | **81.8% / 9.1%** |

**三条结论：**

1. **GC 时间高度集中在慢帧。** stall 帧不到总帧数的 3%，却装下了 62–69% 的 GC 暂停时间。所以「2.5% 墙钟」这个平均口径会**低估** GC 与卡顿的关系。
2. **中等卡顿（1.5–2 ms）GC 是主因**，尤其 A 组：65.8% 的帧其 GC 与帧长同量级。
3. **最重的卡顿（>5 ms）两组结论相反，不能合并叙述**：
   - **B 组**：11 个 >5 ms 帧里 9 个 ratio 0.73–0.89 —— 就是 GC。但它们的 `FrameIdx` 全落在 1040–1977，即**歌曲开头约 2 秒**（开局分配 / 纹理 / 物件创建），之后整局再无 >5 ms 卡顿。
   - **A 组**：11 个 >5 ms 帧里 **0 个**达到 ratio 50%，72.7% 连 10% 都不到。明细 7.06 / 43.59 / 20.31 / 34.42 / 41.51 / 5.78 / 6.58 / 7.57 / 9.68 / 6.50 / 21.37 ms，对应 GC 只有 0–2.33 ms ⇒ **GC 被明确排除**。

⇒ §2.4.8「稳态 ≥5 ms 卡顿两组都是 11 个」这个吻合**不能当成同一个地板**：一边是开局 GC，另一边是成因未明、5–44 ms、与 GC 和帧率都无关的停顿。**这一组无 GC 的重卡顿，比 GC 更值得优先查。**

**分配归属**：`frameSplit` 显示分配 92–93% 在 FSC 子树之外（`subtreeAllocMean` 仅 168 B / 270 B per frame，而 update 线程每帧 2552 B / 3162 B）⇒ ruleset / 播放区不是分配源。

**已实测排除**：`EzBoxElement`（`AcrylicBackdropDrawable`）与 mania `Stage.stageBackdropBlur`（`BackdropBlurDrawable`，按 `ColumnBlur = 0.3` → `sigma = 15` 判定为**开启**）的毛玻璃。开关 Acrylic 对局内 draw 帧数无可见差别。**注意量具敏感度**：`FrameSync = Limit4x` 下 FPS 被锁在 1000，该数字本就不动，故此结论只说明「acrylic 不显眼」，**不能**推广成「每帧分配不重要」。

**卡顿的时间分布**（稳态 stall 帧按 `FrameIndex` 每 1000 帧分桶）：**2–5 ms 类整局均匀**，两组每个桶都有 0–18 个 >2 ms 帧、无开局热点，却是全部 stall 帧的 **95–97%**；**>5 ms 类高度集中在歌曲开头** —— 首 2 s 只占全程 6%，却装下了 1000 fps 的 9/11 与 2000 fps 的 7/11 个 >5 ms 帧。⇒ 应拆成两件事：**均匀背景**（GC + 每帧分配）与**开局约 2 s 的突发**（首波物件创建 / 纹理上传 / 着色器与布局首次执行）。

**>5 ms 帧的逐帧归因**（`SubtreeMs` / `GcPauseDeltaMs` / `ThreadAllocDeltaBytes` 三列）：
- **2000 fps**：4 帧几乎全在 ruleset 子树内（`SubtreeMs`/`ElapsedMs` = 99.4% / 95.9% / 93.5% / 85.1%），GC 与分配都 ≈0；其余 6 帧带 126–382 KB 的单帧分配。
- **1000 fps**：9 帧 GC 主导（GC 4.13–5.70 ms，`ratio` 0.73–0.89），另 1 帧单帧分配 **1.54 MB**（对应 GC 6.55 ms）。
- `SubtreeMs` 是**墙钟**，线程被 OS 抢占同样计入 ⇒ 那 4 帧「子树内 5–43 ms」既可能是真算得慢，也可能是被抢占，**需要「每帧 CPU 时间 vs 墙钟」的读数才能分开**。

**下一步优先级**：① **开局约 2 s 的突发**（占 >5 ms 卡顿的 64–82%，机制已切到 GC / 单帧大分配 / 子树内 5–43 ms 三类）；② **均匀背景里的 GC**（约占 >2 ms 帧的一半，且 62–69% 的 GC 时间集中在慢帧）；③ 给探针补**每帧 CPU 时间**，区分「真算得慢」与「线程被抢占」。

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
| `AnalyzePressLatency.ps1` | 离线读 `diagnostics/presslatency_*.csv`：分 route/空按、FrameAge 分桶、同帧批处理、GC 与缓存命中 |
| `AnalyzeFrameStall.ps1` | 离线读 `diagnostics/framestall_*.csv` + `.summary.txt`：全帧双直方图（按键帧 vs 非按键帧）、GC 因果判据、慢帧归因、与按键尾部对照。见 §2.4 |

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
| 2026-09-24 | §2.4：**按键延迟实测**——新增按键分段 / 帧级 stall 探针与两个分析脚本。结论：`PreColumnMs` 是**等下一帧**（空按与真判定等待相同，且与 `FrameAgeMs` 强相关），不是代码开销；`ColumnMs`（按键自身 inline 工时）p50 0.101 ms；>8 ms 停顿只在开局/退出帧。登记埋点自身的两个误读坑（>阈值上做对比、`SincePrevFrameMs` 零点位置）与「输入队列整树重建 / lane controller 结构」两个不再重开的方向。该局跑在 ≈1968 更新帧/s，**不是高压场景**，故不能解释体感 4 ms；下一步给出复现条件清单 |
| 2026-09-24 | §2.4.6：**提帧与顺滑同源**——`Unlimited` 被夹在 8000 Hz 而实得 1968 帧/s，`throttle()` 一次都没 sleep ⇒ 帧数上限就是每帧工作量（0.450 ms，其中判定 <3%）；每帧持续分配 ~2 KB ⇒ gen0 每 41 ms 一次、单次 ~0.77 ms，与卡顿间隔鼓包（20–60 ms）吻合；`ScrollingHitObjectContainer` 每帧用 FSC 的 `framedClock` 采样一次音频时钟 ⇒ **note 位置按 update 帧量化，帧间隔抖动即下落抖动**（`InterpolatingFramedClock` 不改 `ProcessFrame` 语义，此路不通）。新增 `frameSplit` 归因（子树 / 时钟 / 其余 + 子树内分配），据此决定优化方向 |
| 2026-09-24 | §2.4.8：**帧率上限对比实测**——`base 500/Limit4x`（1979 fps）vs `base 250/Limit4x`（998 fps）。两局 `elapsedMean` ≈ 帧间隔 ⇒ 均为限制器接管，非 CPU 瓶颈；`restMsMean` 增量 0.485 ms ≈ 帧间隔增量 0.496 ms ⇒ **§2.4.7「72.6% 在 FSC 之外」绝大部分是节流 sleep**，真实逐帧恒定成本只有 `subtreeMsMean`（0.105 / 0.116 ms，跨倍帧率不变）。1000 fps 帧间隔规整度 p90/p50 1.89→1.19、超 2× 目标的帧 3.07%→0.65%、按键尾部 `PreColumnMs` p99 35.4→6.8 ms（中位 +0.29 ms）；稳态 >5 ms 卡顿两组都是 11 个（≈0.33 次/s，帧率无关地板），仅幅度更浅。另修正探针语义：`SincePrevFrameMs` 含一个整帧，真实相位 = 该值 − 帧间隔，两速率下均 ≈0.55 ms ⇒ 提出**帧率无关的输入投递地板**待证假设。操作点定为 base 250 |
| 2026-09-24 | §2.4.9：**GC 归因分辨「造成」vs「同帧」（修正 §2.4.8）**——deep CSV 逐帧 `GcPauseDeltaMs / ElapsedMs` 判据（无需 profiler），稳态（`FrameIndex≥1000`）：GC 暂停 62–69% 落在 stall 帧内（stall 帧 <3%）⇒ 平均 2.5% 墙钟会低估其与卡顿的关系；>2 ms 帧约半数 GC 与帧长同量级。**关键分歧：>5 ms 帧 A 组 0% 与帧长同量级（GC 排除，72.7% 连 10% 不到，明细 5–44 ms、GC 仅 0–2.3 ms），B 组 81.8% 是 GC 但 `FrameIdx` 全在 1040–1977（歌曲头 2 秒）** ⇒ §2.4.8「两组都 11 个」是同数不同因的巧合，**那组无 GC 的重卡顿优先级高于 GC**。另登记分配 92–93% 在 FSC 之外、**acrylic 实测排除**（`EzBoxElement` / `Stage` 毛玻璃开关对 draw 帧数无可见差别，并注明限帧下量具不敏感）。补充 stall 帧时间分布：**2–5 ms 类整局均匀（占 stall 帧 95–97%），>5 ms 类集中在歌曲首 2 s（占 >5 ms 的 64–82%）**；>5 ms 帧逐帧归因为三类（子树内 5–43 ms / GC 主导 / 126 KB–1.54 MB 单帧分配），并提出补「每帧 CPU 时间」以区分真算得慢与线程被抢占 |

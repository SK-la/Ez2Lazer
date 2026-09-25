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
| **POLICY-SCRATCH** | `OrderedHitPolicyHelper` | 候选 / 后判对象改复用缓冲（去掉每次判定的 `ToList()` 拷贝）；`OrderBy(...).ToList()` 改复用缓冲上的稳定插入排序；诊断串整段门控在 `EzJudgmentDiagEnabled` 之后（关闭时不再执行 `describe` / `string.Join`）；命中模式缓存 bindable，取代一次判定里 3 次 `ezConfig.Get`（诊断开关本身也已改为进程级静态位，见 §2.4.18） |
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
`AnalyzePressLatency.ps1` / `AnalyzeFrameStall.ps1` / `AnalyzePeriod.py`（周期性 / 跨探针相位）。
总开关是 **ini 设置 `Ez2Setting.EzJudgmentDiagEnabled`**（`EzExperimentalSettings` 里的「判定诊断」，
不是环境变量），它同时打开三个探针；其余走环境变量：`EZ_FRAME_PROBE_MS`（stall 阈值，**0 = 抓全集**）、
`EZ_FRAME_PROBE_LIGHT`（关掉每帧 GC/分配读数）、`EZ_PRESS_PROBE_SKIP_FORCE_MISS`（消融强制 miss 扫描）。
**这些只在诊断开启时生效。**

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

#### 2.4.10 周期性 / 相位分析工具（`AnalyzePeriod.py`）与「2–4 s 周期」的现状（2026-09-24）

§2.1 那两个 ACF 数字（`Drift` 3.10 s / 0.135、9.47 s / 0.164）当时是临时算的，**仓库里从来没有对应工具**，
所以既不可复现也无法核对 —— **⛔ 已作废，不要引用**（交接单 §2.1 已连同原文删除）。
新增 `AnalyzePeriod.py`（numpy，可选 matplotlib）补上这条腿：

- 三探针的 `WallMs` 同源（`EzJudgmentDiagnostics.WallClockMs`），所以**同一局**的三份 CSV 可直接跨探针对齐做互相关 ——
  这是回答「相位来源」的前提。
- 输出：逐序列的 ACF 主峰 + Welch 谱主峰 + 带内主周期（Welch 分辨率不够时用细扫定准）+ 滑窗幅度/相位，
  最后一张跨序列滞后表（只报唯一对、只报带内一个周期以内的滞后）。
- 显著性用两个可比读数：`ACF r` 与「带内主周期占带内能量 / 均匀背景」，**不用**「峰 / 中位」——
  在 1/f² 型背景上后者恒为几百倍，会把噪声报成显著。
- 选项：`--trim A,B` 去掉首尾过渡段（找周期基本都该加）、`--band`、`--dt`、`--only`、`--windows`、`--plot`。
- 序列：judgment 的 `Drift` / `TimeOffset` / `AudioLag` / `FrameElapsed`；press 的 `PreColumnMs` / `ColumnMs` /
  `FrameAgeMs`；frame 的 `ElapsedMs` / `SpikeRate` / `GcPauseDeltaMs`，以及 2026-09-24 随**每帧时钟探针**新增的
  `AudioStep`（音频源逐帧步进）/ `InterpRate`（插值时钟逐帧速率）。看 100 Hz 量化用 `--band 0.004,0.02 --dt 0.002`。
- `derived_from` 改成了**元组**：`InterpRate` 由 `InterpMs` 与 `ElapsedMs` 共同构造，单来源标记盖不住那两个定义性对。
- ⚠ **`InterpRate` 实测是条会骗人的序列（2026-09-25 结论，见 §2.4.11）**：它在 0.010 s 上确实有强 ACF 峰，
  但那对应的是**逐帧速率**的摆动；速率 std 0.28 / `|rate−1|>5%` 占 85%，而**位置**只偏离平滑线 0.41 ms。
  ⇒ 这条序列不能用来判断「顺不顺滑」，要判位置请用帧 CSV 的 `InterpMs` 与 `AudioSrcMs` 直接复算（§2.4.11 表）。
  `AudioStep` 仍会被 `RMS/稳健σ` 护栏拒掉（稀疏脉冲列的构造使然，不是数据问题），也只能离线算。

工具自带五条防误读的自检，都是实测踩出来的：

1. **事件型序列的分辨率 = 采样间隔。** judgment / press 都是 ~10 次/s，线性插值到 20 ms 网格后，
   滞后在**一个采样间隔内**的读数与「同时」在数值上不可分辨（1.5–5 s 带内成分的平坦度差异约 0.2%），
   故这类峰只报「同时」而不报毫秒数。**但绝不能把这一段直接排除掉**——真峰落在这里时，剩下的 argmax
   会停到周期旁瓣上：实测两个**完全相同**的信号曾被报到 −1.06 s，比不设限更错。
2. **带通后的相关峰很宽，贴边即不可定。** 序列先被 1.5–5 s 带通，相关长度本就是秒级，互相关系数在
   0 附近又宽又平。若峰的 argmax 落在搜索上限（±最短周期）上，说明真峰可能在被截断的另一侧，
   此时只报「贴边」+ r，不报滞后。**只有严格内部的峰才给出「谁领先」。**
3. **尾部 frame 数据必须排除。** 阈值 > 0 的 `framestall_*.csv` 只有慢帧，块均值桶大量靠插值填补，
   会伪造出极强虚相关 —— 实测它给出 `TimeOffset × ElapsedMs r = −0.874 @ 420 ms`，看着像定案，
   其实是纯伪影。现在非全集 frame 序列被标 `⚠` 并**不参与相位表**。
4. **离群帧主导方差时带内结论无效。** 用 `RMS / 稳健σ(MAD)`：实测同一局 `ElapsedMs` 全段 **15.7x**
   （方差被 t=0 那个 141 ms 首帧独占），`--trim 3,3` 后降到 **2.6x**。超 `OUTLIER_RATIO=5` 的序列
   同样不参与相位表。
5. **带通后的独立样本太少，|r| 的偶然水平极高 —— 必须标定。** 独立样本数 ≈ 跨度/周期 ≈ 13，
   实测零分布（同 span / dt / 带，6000 次独立对）：单对 `p95 = 0.503`、`p99 = 0.572`、`p99.9 = 0.664`。
   于是 66 对里「至少一对 ≥ 0.47」的概率是 **99.2%**。工具现在按 `0.05 / 对数` 反解家族性阈值
   （实测 31 对时 = **0.597**）并逐行标注是否越线。**没有这条基线，看表的人必然把偶然峰当相位来源。**
   另外把**定义性派生**的序列对剔除（`SpikeRate` := `ElapsedMs ≥ 2×p50` 的指示函数；
   `AudioLag` = `−Drift − 15.000ms` 常数偏移）——它们的高相关是构造出来的。

**在现有三局（各 ~34 s，未含全帧局）上的结果：测不出 1.5–5 s 带内的稳定周期。** 全部序列 `ACF r ≤ 0.20`、
带内占比不超过均匀背景 2.8 倍；`Drift` 的峰散在 2.1–2.8 s 但 r 只有 0.07–0.14，三局位置互不相同。

**全帧局（2026-09-24 晚，38.7 s / 76985 帧）的结果与一个假阳性教训**：未修剪的第一次运行给出
`ElapsedMs × GcPauseDeltaMs r = +0.998`、`× ColumnMs +0.957` —— 帧时长不可能与按键列工时同相。
回头查是同一个 141 ms 首帧（t=0）在两个序列里的余振。`--trim 3,3` 后：`ElapsedMs` 带内 RMS
从 **3.294 ms → 0.026 ms**，全部内部峰都落在家族性阈值 0.597 之下，唯一越线的
`ElapsedMs × SpikeRate −0.746` 是定义性相关。**稳态帧长在该带内只有 0.026 ms 抖动**（帧长均值 0.503 ms）
⇒ 帧节奏侧没有可被感知的 1.5–5 s 波动，§3 的「2–4 s 周期」前提被证伪，下一步应换观测维度（见交接单 §3.5）。

⇒ 与 §2.1 的「现象未被测下来」一致；另外 18xxxx 那两局 CSV 已不在 `diagnostics/`，§2.1 引用的数字
无法再用现有数据复算（不排除仍有其它算法能复现，故不据此推翻结论，只登记「不可复现」这一事实）。

#### 2.4.11 音频源时钟是精确 10 ms 阶梯 —— §3 空结果的结构性原因（2026-09-24 晚）

§2.1 里「`BassSource − GameTime` 峰峰 9.8–9.9 ms，是音频抖动还是缓冲量化，未定」这一条，本次**定案：是缓冲量化**。

**实测（4 局 `judgment_*.csv`，各 298–353 个判定样本）**

| 序列 | 落在 10.000 ms 网格 | 说明 |
|---|---|---|
| `BassSource`（音频源时钟） | **100%**，最大残差 **22 µs** | 331–353 个取值；相邻差只有 9.978 / 10.000 / 10.022 及其整数倍 |
| `GameTime`（= `InterpClock`） | ~3%（均匀） | 连续，无网格结构 |

- `GameTime == InterpClock`（逐行相等），且 `Drift == (GameTime − BassSource) − 15.000`（常数 15 ms 是音频偏移）。
- `Drift` 峰峰：3 局 **9.8 / 9.9 / 11.5 ms**，1 局 **19.2 ms** —— 即插值残差有**整整一个缓冲**那么大。

**机制（代码链）**：实机走 `NAudioWasapiOutput`（BASS 解码 mixer + NAudio 写 WASAPI 共享模式），
请求延迟 `DEFAULT_NAUDIO_LATENCY_MS = 10`；`TrackBass.CurrentTime` 取 `BassMix.ChannelGetPosition`，
而**解码 mixer 的位置只在 NAudio 拉走一个 buffer 时才前进**（`BassMixerWaveProvider.Read` 每次按整缓冲取数）。
⇒ 音频时钟一次跳 **10.000 ms**，即 **100 Hz**。

**实机日志确证（`logs/1790263779.audio.log`，与全帧局同一次会话）**

```
NAudio default output started: bassDevice=5 ("VoiceMeeter Aux Input (VB-Audio VoiceMeeter AUX VAIO)"),
wasapi="VoiceMeeter Aux Input (VB-Audio VoiceMeeter AUX VAIO)", 48000Hz/2ch float,
requestedLatency=10ms, actualLatency=10ms, lowLatency=true
```

- 48000 Hz × 10 ms = **480 帧**，与实测 10.000 ms 量子精确吻合（不是巧合）。
- 设备是 **VoiceMeeter 虚拟声卡**，不是物理 DAC。
- `actualLatency` 不是回显请求值：NAudio 文档明确它是「设备**实际授予**的引擎周期」（低延迟模式下由设备给的
  period 推导）；`lowLatency=true` 表示 IAudioClient3 低延迟共享模式**确实生效**。
  ⇒ **低延迟模式开着，这台虚拟设备也只给 10 ms**（物理 DAC 走 IAudioClient3 低延迟通常 ~2.67–3 ms）。
  所以「把 `DEFAULT_NAUDIO_LATENCY_MS` 调小」不会有用 —— 请求值不是瓶颈，**授予值**才是。
- 同目录 `ez_runtime.log` 里同时有 `Found 7 ASIO devices` / `Freeing ASIO device`，说明 ASIO 路径也在用，
  是「换设备/换模式」这条修复方向上的现成选项。

**为什么三次测量都没看到它**：三个探针的采样率都是 **~10 Hz**（判定 / 按键 93–100 ms 一次）。
100 Hz 信号按 10 Hz 采样只会产生**混叠的低频幻影** —— 它正好**伪装成**「隔几秒一次」的波动，
而 §3 一直在 0.2–0.6 Hz 带里找周期。**这是测量盲区，不是效应弱。**

**用真实帧间隔做的离线仿真**（1988 fps、中位帧长 0.418 ms；按 `InterpolatingFramedClock.ProcessFrame`
逐帧复刻，`DriftRecoveryHalfLife = 50`）：

| 源 | `Drift` 峰峰 | 每帧速率 std | 速率偏 >5% 的帧 | 相对匀速的位置偏差 |
|---|---|---|---|---|
| 10 ms 阶梯（实机） | **11.7 ms** | 4.0% | **27.9%** | std 0.11 ms / 峰峰 2.8 ms |
| 理想连续 | 0 | 0.36% | 0% | 0 |

仿真 `Drift` 峰峰 11.7 ms 与实测 9.8–19.2 ms 同量级 ⇒ **机制确认**。1988 fps 下每个台阶跨 **~24 帧**，
所以高帧率把这个 100 Hz 纹波**采样得更清楚**；60 fps 下它反而低于 Nyquist（这也解释了「提帧反而更不流畅」的直觉）。

⚠ **两个保留**：① 仿真是理想化的（台阶严格每 10 ms 落一次），实机拉流时刻由音频线程调度，
且实测 `Drift` 已超过仿真值；② 仿真给的位置偏差只有 **±1.4 ms**，够不够成「体感不流畅」**尚未证明**。

⇒ **每帧时钟探针已加（2026-09-24）**：`FrameStabilityContainer.UpdateSubTree` 的帧边界处把
`gcc.BassSourceCurrentTime` / `gcc.CurrentTime` 交给 `EzFrameStallDiagnostics.RecordFrame(...)`，
帧 CSV 新增 `AudioSrcMs,InterpMs` 两列、摘要新增 `clockQuant` 行、`AnalyzePeriod.py` 新增
`AudioStep`（音频源逐帧步进）与 `InterpRate`（插值时钟逐帧速率）两条序列。

---

**实测结果（2026-09-25，全帧局 `framestall_20260925_080438.csv`：36.5 s / 72463 帧 / 1983 fps，全部有钢琴键按下）**

**结论：插值把 10 ms 阶梯抹得很干净，音频时钟这条链洗清嫌疑 —— 这里不是「体感不流畅」的来源。**

*已证实的机制（与上表一致）*

- `AudioSrcMs` 在稳态是 **100.0 次/s、步长中位 10.0000 ms**、间隔 std 0.466 ms（p1 8.97 / p99 11.04）⇒ 精确 10 ms 阶梯，无第二种量子。
- `InterpMs` 在 72463 帧里有 **70641 个不同取值**（≈每帧都变），mod-10ms 残差均匀 ⇒ **插值时钟不是阶梯**，量化确实被抹平了。
- 时钟本身无走时差：排除歌曲结束后的停走（见下），`ΣΔinterp/ΣΔwall = 1.00023`，且**每 10 ms 窗**内两者只差 0.006 ms。

*⚠ 一个必须记住的指标陷阱：逐帧速率不能当抖动读*

`Δinterp/Δframe` 实测 p25 **0.82** / p50 0.98 / p75 **1.17**、std **0.28**、`|rate−1|>5%` 占 **85%**。
这看着像严重抖动，其实是**抹平成功的表现**：缓冲内要加速追赶（rate>1），缓冲跳变那一下相对变慢（rate<1），
位置反而始终贴着一条平滑线。**std 是平移不变的、速率不是 —— 判「顺不顺滑」只能看位置。**
（旧摘要里的 `interpRate mean/std/within1%/over5%` 因此已撤掉。）

*位置判据（真正的答案）*

把音频报告值还原成连续位置（报告值 + 距上次跳变的时长 = 缓冲内已播进度），再看 `interp` 离它多远：

| 量 | 稳态 t∈[5,30)s | 整局 |
|---|---|---|
| `interp − (audio + age)` std | **0.408 ms** | 0.667 ms |
| p99 / max | — | 2.64 / 39.0 ms |
| >0.5 / >1 / >2 ms | — | 19.0 / 2.4 / 1.3 % |
| 常数 offset | +10.25 ms | +10.31 ms |

- **std 0.41 ms，比 10 ms 缓冲量子小一个数量级** ⇒ 插值时钟是一条平滑线，仿真预测的「±1.4 ms 够不够成体感」到此为**否**。
- 残差主频 **109 Hz**（+218 Hz 谐波）—— 波纹确实在缓冲率上，但幅度只有 ~0.4 ms（≈600 px/s 下 0.25 px）。
- 那个 **+10.25 ms 常数**是**音频输出延迟**（`ChannelGetPosition` 报的是已拉进输出缓冲的位置，领先「听到的声音」约一个缓冲），
  会被玩家的音频偏移吸收，不是抖动。探针用 **500 ms EMA** 在线估掉它再判阈值。
- 残余的 `>1 ms`（整局 2.4%）集中在开局/收尾，稳态只有 0.55%。

*两个会误导汇总数字的收尾现象（已在文档登记）*

- 歌曲结束最后 **0.9 s** 时钟停走（1822 帧、917.5 ms）：这些帧 `Δinterp=0`，把「平均速率」拉到 0.975。
  探针现在把「停走」单列（`clockStopped`），不再混进速率/误差统计。
- 探针阈值必须在**去掉常数 offset 之后**判，否则 100% 的帧都「超标」——这就是上面 EMA 的由来。

*下一步*：更新线程侧的帧时间、FSC 子树、按键延迟、音频时钟**四条链现在都测不出问题**，
所以「下落不顺滑」若真实存在，需要在**更新线程之外**找 —— 最可能是 **present / 帧节拍**（本探针只看 update 侧，
不看呈现时刻的均匀性），其次才是主观锚定。该量具已落地，见 §2.4.12。

⚠ **上面这条「音频链干净」有前提（2026-09-25 08:44 测出边界）**：它只在**音频源拉取规整**时成立。
同机、同音频配置的下一局里源漏拉了 **145 次**，`interpErr` 立刻从 std 0.406 / >1 ms 0.52% 涨到
**1.084 / 14.56%**；剔掉漏拉邻域后回到 0.455 ms ⇒ **判 `interpErr` 必须同时看 `pullMiss`**，见 §2.4.13。

#### 2.4.12 present 侧量具落地：量「相邻两次 present 的间隔」与「上屏内容的年龄」（2026-09-25）

§2.4.11 把 update 侧四条链全部洗清之后，唯一没量过的就是**呈现**：note 位置在 update 帧边界上就定好了，
之后只剩渲染与 `Present`。它必须量，因为 update 线程 1983 fps 平稳**并不**代表屏幕每 1/刷新率 秒收到一份均匀的内容。

*为什么不能在 draw 线程打点*

本 fork 里 `osu.Framework.Game` 是 `Container` 而**不是** `GameHost`：

| 事实 | 后果 |
|---|---|
| `GameHost.DrawFrame()` / `UpdateFrame()` 确为 `protected virtual`（反射确认 `IsFamily=True, IsVirtual=True, IsFinal=False`） | 只要有继承关系就能 override |
| `public abstract class Game : Container`（`GameHost` 不在 `Game` 的继承链上） | `OsuGameBase` 没有可 override 的基方法 → **CS0115** |

`OsuGameBase` 只能通过 `Host` 拿到那个 `GameHost`（它自己只是场景图里的一个 drawable）。
⇒ 只能在 **update 侧读 `DrawThread.Clock`**：`ThrottledFrameClock` 由 draw 线程每帧 `ProcessFrame`，
但它的属性任何线程都读得到。`osu.Game` 里已有先例（`FPSCounter`、`LatencyCertifierScreen`）。

*量到的三个量*

| 摘要字段 | 来源 | 它回答什么 |
|---|---|---|
| `drawPeriod` | `DrawThread.Clock.ElapsedFrameTime` | **相邻两次 present 的间隔分布**。update 侧帧长恒为 0.5 ms 且不抖，但 draw 线程单独卡一下 update 探针是看不见的 |
| `presentAge` | `drawClock.CurrentTime + 原点偏移 + ElapsedFrameTime − 上一次 update 帧边界` | **上屏那一刻，屏幕上那份内容有多旧**。均值会被输入延迟吸收，**抖动才是眼睛看到的顿挫** |
| `clocks` | 两线程 `FramesPerSecond / Jitter / TimeSlept / MaximumUpdateHz / Throttling` + 窗口态 + 刷新率 | 限制器实际生效的目标值，以及「每个刷新显示几个 present」（`refresh / fps`） |

两处实现细节不写下来就会误读 —— **第一局实测把这两条都证伪了，已按下面的口径改掉**：

1. **原点差不能一次性标定。** `StopwatchClock` 基于 `Stopwatch.ElapsedTicks`，零点是自己 `Start()` 的时刻（= 线程创建），
   与 `Stopwatch.GetTimestamp()` 不同源，要标定原点差。但 `CurrentTime` 只在 draw 帧边界刷新、是**阶梯**的，
   `wall − CurrentTime` 因此带一个幅度 = 一个 draw 周期的**锯齿**；一次性标定锁在某个随机相位上，
   后果是 `presentAge` 出现物理上不可能的负值（实测 **min = −6.41 ms**）。⇒ 改为取**运行最小值**（刚刷新完那一瞬即真值）。
2. **逐 update 帧采样不等于逐 draw 帧采样。** update（2034 fps）比 draw（1210 fps）快，同一个 draw 帧会被读到多次；
   而某个值在第 i 帧结束时写入、只保持到第 i+1 帧结束，被采到的次数 ∝ **下一帧**的长度。
   两帧长度负相关时（限帧器自带追赶机制）长帧被少采、短帧被多采 ⇒ 均值与分位一起偏低：
   实测 `drawPeriod` mean 0.622 ms，而 `DrawThread.Clock.FramesPerSecond = 1210` ⇒ 0.826 ms，
   且按 p99 = 1.59 / max = 37 **摆不出 0.826 的均值**，两个数不可能同时成立。
   ⇒ 改为**按 `drawClock.CurrentTime` 变化去重**（update 比 draw 快，每个 draw 帧至少被读到一次），
   并在摘要里给出自检 `coverage = ΣdrawPeriod / 采样首末壁钟跨度`（≈1 = 每帧恰好采一次；
   <1 = 有 draw 帧整个落在两次采样之间被漏掉）。
3. **`presentAge` 的分辨率是**一个 draw 周期**，不是一个 update 帧长**。读不到「正被绘制的那个 buffer 的帧号」，
   零点只能取「上一次 update 帧边界」，而 draw 线程手上的 buffer 可能是更早一帧的 ⇒ 配对不确定度 ≈ ±1 draw 周期
   （0.83 ms 量级）。**这个量级和我们追的效应（0.4 ms）同阶**，所以 `presentAge` 只能判约 1 ms 以上的滞后，
   判不了亚毫秒配对抖动；`drawPeriod` 没有这个问题（是 draw 线程自己测的）。
   失焦 / 最小化时 draw 线程不画，那些帧按 `IsActive` 剔掉（`skipped` 计数）。

*不覆盖的部分（写下来免得下次又去这里找）*

`DrawFrame` 之后的 **DWM 组合 / 显示器扫描输出**在进程内量不到。若 `present` 行也干净，
剩下的解释就只有 tearing（Borderless 下 `Renderer.AllowTearing = true`）/ 组合器节拍 / 主观锚定。
**到 2026-09-25 10:11 为止，present 之外的链路都已洗清，这一条成了剩下的唯一进程外环节 —— 判据是 A/B（开 vsync / 钉刷新整数倍），见 §2.4.17 与交接单 §3.5 第 6 项。**

*判读口径*

- **先验四条采样自检**（不通过就先修量具，别读结论）：
  ① `dup=` 有值（去重生效）；② `coverage` ≈ 1；③ `drawPeriod` 的 `mean` ≈ `1000 / clocks 里的 draw fps`；
  ④ `presentAge.min` 不为负。
  - ② 在 10:11 局实测 **0.869**：原因是**采样者（update 线程）在歌曲结束后的退场段自己掉到 120–190 Hz**，
    draw 帧整段被漏掉。采样现在跳过时钟停走的帧（摘要 `frozenSkipped=`）⇒ **②只能在同一局内、同一工况下比**，
    详见 §2.4.17。③ 同理：`clocks` 的 fps 是整局（含退场段）统计，与只看局内的 `drawPeriod` 不是同一分母。
- 再看 `drawPeriod` 的**尾部**：`over1 / over2 / over5` 与 `max`。一次 8 ms 的 draw 帧就是一次可见跳帧，
  而 update 侧探针对它完全无感 —— 这正是加这个量具的理由。
- `presentAge` 看 `std` 与 `max`，**跟它自己的分辨率（= 一个 draw 周期）比**：小于分辨率就是「无滞后」。
- `clocks` 里 `refresh / fps` 不是整数 ⇒ 每个刷新显示的帧数在 1 / 2 之间交替，是结构性 judder，
  幅度 ≈ 半个 draw 帧的位置误差（2000 fps / 240 Hz 下约 0.2–0.25 ms）—— 小到不构成体感，但可以据此确认或排除。

*第一局（2026-09-25 08:44，40.5 s / 81006 帧）的数与它的用途*

| 读数 | 值 | 能不能用 |
|---|---|---|
| `drawPeriod` | n=80988 mean 0.622 std 0.390 min 0.324 p50 0.510 p90 0.990 p99 1.59 p99.9 3.11 max 36.99 over1=8053 over2=230 over5=28 | **不能**。逐 update 帧采样（见上第 2 条），均值与分位都被拉低 |
| `presentAge` | mean 14.92 std 0.657 **min −6.412** p99 16.33 p99.9 17.73 max 102.6 | **不能**。负的 min 说明原点差错（见上第 1 条），std 也可能被配对不确定度污染 |
| `clocks` | draw fps=1210 jitter 0.344 sleptMean 0.000 maxHz 2000 throttling=True；update fps=2034 jitter 0.220 maxHz 2000；FullscreenBorderless refresh=175Hz | 可用。`sleptMean = 0` 而 p50 恰好压在限帧目标 0.510 ms ⇒ draw 是**工作受限**，不是限帧受限 |

唯一可以留下来的一条：`drawPeriod` 的 p50 **正好等于**限帧目标（2000 Hz → 0.500 ms），而 `sleptMean = 0`
⇒ draw 线程的每帧工时已经贴在 0.5 ms 附近，限制器基本没睡。逐 draw 帧的分布要等修好的那一局。


#### 2.4.13 音频源会「漏拉」：`interpErr` 的读数有一个必须同时看的前置量（2026-09-25）

同一台机、**同一份音频配置**（`logs/1790294601.audio.log` 与 `1790297002.audio.log` 逐字段相同：同 VoiceMeeter 端点、
48000 Hz/2ch float、requested=actual=10 ms、lowLatency=true），相隔 40 分钟的两局全帧采集：

| 量 | A 局 `framestall_20260925_080438` | B 局 `framestall_20260925_084436` |
|---|---|---|
| 源停走 >15 ms 再一次性补上（`pullMiss`） | **1** 次（0.03/s） | **145** 次（3.6/s，累计停走 2.8 s） |
| 漏拉间隔 | — | 中位 0.17 s，min 0.02 / max 1.23 s，**不对齐 100/200/250/500/1000 ms 任何网格** |
| `interpErr` std（稳态 t∈[5,30)） | 0.406 ms | 1.084 ms |
| `interpErr` >1 ms | 0.52 % | 14.56 % |
| 同上，**剔除每次漏拉的 ±20 ms 邻域** | — | **0.455 ms / 5.5 %**（窗口放大到 ±200 ms 不再改善） |

*机制*

`BassMix.ChannelGetPosition` 只在 NAudio 渲染线程拉走一个缓冲时前进。漏拉一拍 ⇒ 位置**停走 ~20 ms**
（两次跳变间隔 20 ms 而不是 10 ms）再一次性补上。这与 §2.4.11 记的「精确 10 ms 阶梯」不矛盾：
阶梯仍在，只是偶尔少一级。**「补」是必然的、而且距离不丢**——B 局全段推进 39826.1 ms / 壁钟 39824.5 ms
（差 1.7 ms，0.004%），所以那 2.8 s 是这 146 次 hold 的**累计停走时间**，不是丢失的音频时长；
该现象只造成**相位阶跃**，不造成走时差。成因与「一跳吞掉几拍」的定量关系见 §2.4.14。

*为什么它必须和 `interpErr` 一起读*

插值器要在 ~40 ms 内把这 10 ms 的缺口追掉，追赶期间的位置误差全部计进 `interpErr` 的 std 与超阈值比例。
B 局与 A 局 `interpErr` 的 2.6 倍差距**全部**来自这 145 次漏拉（剔掉邻域即回到 A 局水平）
—— 不一起看就会把「源在卡」误判成「插值不干净」。

*另一个必须记住的坑：`audioStep top` 会把它藏掉*

`audioStep` 分布只打 top-5，而 20 ms 级步进只有 146 次，低于第 5 名的 228 次 ⇒ **一次都不会显示**。
所以摘要里加了独立的 `pullMiss` 计数（停走 >15 ms 且 <60 ms；再长的停走是暂停 / seek，不算）。

*与 update 帧的关系：只弱相关*

漏拉那一帧 `ElapsedMs` 均值 0.831 ms（全体 0.501），±5 帧内出现 >2 ms 帧的比例 4.1%（该窗口偶然水平 1.07%）
⇒ 4x 富集，不足以断言「update 卡导致音频漏拉」。也没有指向游戏自身逻辑的证据（不对齐任何定时器网格）。

*是否成体感：尚未判定*

量级是 std ~1 ms / p99 1.4 ms / maxdev 7 ms（600 px/s 下 0.6–4 px），且 145 次分布在整局
（每 4 s 计数 20/10/11/21/17/19/16/11/15/7）⇒ 若它在起作用，表现是**全程低频抖动**而不是「隔一会一次」。
但它符合「两次游戏一顺一不顺」这一现象，**优先级高于继续在 present 侧找**（present 量具本身刚修，见 §2.4.12）。

#### 2.4.14 「漏一拍、补一拍」的机制已定案：NAudio 读的是「当前全部空位」（2026-09-25）

> ⛔ **标题里原先还有半句「而渲染线程没有 MMCSS」—— 那个根因假设已被 §2.4.17 证伪**
> （登记 MMCSS 后漏拉分布一模一样）。**本节保留的部分是机制**（下面这些恒等式与源码事实），
> 与线程优先级无关；**不要再从 MMCSS / 线程优先级方向重开这条链**。

上一节的形状（停走 ~20 ms 再一次性补上）在**稳态**（t > 3 s）里干净得可以当恒等式用：

| 读数（稳态 t>3 s） | A 局 `080438` | B 局 `084436` |
|---|---|---|
| 位置跳变只出现的档位 | **10.0 / 20.0 / 30.0 / 40.0 ms** | **10.0 / 20.0 ms** |
| 亚毫秒跳变 | 0（只在开局 3 s 内有 733 次 / 394 ms） | 0（只在开局 3 s 内有 1184 次 / 666 ms） |
| 位置跳变次数 / 周期数 | 3492 / 3499 | 3761 / 3907 |
| 差额 | **7** = 20+20+30+40 各自多吞的周期数（1+1+2+3） | **146** = 每次多吞 1 个周期 |
| 全段推进 / 壁钟跨度 | 35453.7 / 35447.1 ms（+6.7） | 39826.1 / 39824.5 ms（+1.7） |
| 漏拉间隔 mean / CV | —（n=4） | 264 ms / **0.92**（泊松，20 ms 网格 ACF 全平） |
| 正常跳变的 hold | — | mean **10.004** std 0.568（无系统正漂） |

- **`跳变次数 = 周期数 − 超额周期数`** 两局都精确成立 ⇒ 每次事件都是「**漏掉一次唤醒，下一次一次读两拍**」，
  不是丢数据、也不是时钟走快。开局 3 s 那批亚毫秒跳变是启动 / seek 期的连续位置（读的是解码位置），
  与稳态机制无关，计任何稳态量前必须从 t=3 s 起算。
- **间隔 CV 0.92 + ACF 全平 + 正常 hold 无系统正漂** ⇒ 不是「固定相位漂移」（那会给近似恒定的间隔、CV ≪ 1），
  而是**随机抢不到调度**。

*为什么必然是「补一回」（NAudio 源码，已核 `src/NAudio.Wasapi/WasapiPlayer.cs` main）*

```csharp
if (mmcssTaskName != null)   // ← 只有设了才登记 MMCSS
    mmcssHandle = NativeMethods.AvSetMmThreadCharacteristics(mmcssTaskName, ref taskIndex);

bufferFrameCount = audioClient.BufferSize;
...
WaitHandle.WaitAny(waitHandles, 3 * latencyMilliseconds, false);   // 30 ms 兜底
int numFramesPadding  = (isUsingEventSync && shareMode == Exclusive) ? 0 : audioClient.CurrentPadding;
int numFramesAvailable = bufferFrameCount - numFramesPadding;
if (numFramesAvailable > 10) FillBuffer(numFramesAvailable);       // ← 读「当前所有空位」，不是读一拍
```

- 线程被引擎每 10 ms 一次的事件唤醒，但读的是 `BufferSize − CurrentPadding`，即**当时所有空位**。
  所以「一场没被唤醒」的后果必然是**一次读两拍**（960 帧）⇒ 位置一次 +20 ms ⇒ 观测到的 shape。
  这也解释了为什么 hold 全部落在 19.4–23.3 ms、`>30 ms` 一次都没有：`frameEvent` 是 **AutoReset**，
  信号不累积，超额读完之后缓冲重新填满，下一次又对齐 ⇒ 不会一漏一串。
- **`mmcssTaskName` 默认 `null`**（`WasapiPlayerBuilder`），只有显式 `.WithMmcssThreadPriority(...)` 才设；
  本 fork 的构造链 `osu-framework/osu.Framework/Audio/Wasapi/NAudioWasapiOutput.cs:112`
  原先（`.WithDevice().WithSharedMode().WithEventSync().WithLowLatency().WithLatency(10)`）**没有调它**。
  ⛔ **但「没登记 MMCSS 就是根因」已于 2026-09-25 被证伪**：登记（`"Pro Audio"`）之后
  `pullMiss` 仍 142（3.6/s）、`periods/pull max` 仍 2.00，与未登记时（145）相同 —— 见 §2.4.17 与 §2.4.16 的分布量具。
  所以这段只作为**机制**记录，不要再当作可修复的根因。

*可执行的下一步（都要先确认再改）*

1. ~~**一行候选**：`.WithMmcssThreadPriority("Pro Audio")`~~ → **已落地并已实测：无效，判据判负**（§2.4.17）。
   当时写死的判据是「下一局 `pullMiss` 从 3.6/s 级掉到 0.03/s 级即成立；若仍是几十/几百次量级 ⇒ 音频链就地结案」，
   实测 142 次（3.6/s）⇒ **结案**。
2. **把推断换成直读**：在 `BassMixerWaveProvider.Read` 记 **(墙壁时间, 请求字节数)**。NAudio 传进来的 `count`
   **就是** `numFramesAvailable × BlockAlign`，所以 `count ≈ 960 帧` 的存在直接证明「本次读吞了两拍」，
   还能顺带读出 `BufferSize`（几拍）与两次读的**真实墙壁间隔**（比帧 CSV 的 0.5 ms 分辨率细）。

#### 2.4.15 MMCSS 已进源码，但**没进那一次游戏**（交付陷阱）+ 单局判据被证伪（2026-09-25）

**改了什么**（`osu-framework`，commit `c86e585e4`）：`NAudioWasapiOutput.cs:112` 的构造链加了
`.WithMmcssThreadPriority(AudioOutputDefaults.DEFAULT_NAUDIO_MMCSS_TASK)`（`"Pro Audio"`，常量与理由在
`Audio/AudioOutputDefaults.cs`），启动日志同步加 `mmcss=<task> (requested)`。

**交付陷阱（这次踩了）**：`osu` 侧默认从 **NuGet 包 `ez2lazer.Framework 2026.921.0-ez2lazer`** 取框架
（`Ez2Lazer.Dependencies.props` 里 `UseEz2LazerLocalFrameworkProject` **默认注释掉**），所以**改 `osu-framework`
源码不会自动进游戏**。证据两条：

- 9:20 那次重建把主程序与全部规则集都刷新了，但 `osu.Desktop/bin/Release/net10.0/osu.Framework.dll`
  仍是 **2026-9-21 14:52**（包的时间戳），不是 9:27 的本地构建；
- 9:21 那一局的 `logs/1790299269.audio.log` 里**没有**新加的 `mmcss=` 字段。

⇒ **9:22 那一局不是 MMCSS 的测试**，它的 `pullMiss=4` 不能算「MMCSS 生效」。要让 framework 改动进游戏，要么打开
`UseEz2LazerLocalFrameworkProject`（props 注释里就是给这种情况用的，本次已在本机打开、**不提交**），要么走包发布。
**已做**：9:36 关掉游戏后用本地工程重建 `osu.Desktop`，输出 `osu.Desktop/bin/Release/net10.0/osu.Framework.dll`
时间戳变为 9:36:30，且内含 `mmcss` / `WasapiReadStats` / `wasapiRead` / `wasapiPull` 标记
⇒ 下一局才是真正的 MMCSS + 直读量具测试。

**单局判据被证伪**：同一份音频路径（三次采集 framework 都没变）的 `pullMiss` 是 **1（080438）/ 145（084436）/
4（092213）**。⇒ 「掉到 ≤5 次」这条判据在单局上不成立——**基线自己就在 1–145 之间跳**。要判就得在同局里拿到
**分布**，而不是一个稀有事件计数；为此补了 §2.4.16 的直读量具。

#### 2.4.16 `wasapiRead`：NAudio 拉取的间隔与请求拍数直读（2026-09-25）

- 新类 `osu-framework/osu.Framework/Audio/Wasapi/WasapiReadStats.cs`，在 `BassMixerWaveProvider.Read` 每次记录
  「距上次拉取的壁钟间隔」与「本次要了几拍」（`count / BlockAlign / (sampleRate/100)`），各进一个定长直方图
  （间隔 0.5 ms × 80 桶；拍数 0.25 拍 × 16 桶），另存精确 max。
- **默认关闭**（`Enabled`）：只有诊断打开的那一局由 `Player.cs` 打开并在局末关闭 ⇒ 正常游戏在这条热路径上
  只多一次 bool 判断。
- 汇总新增两段：`wasapiRead reads=…(…/s) gapsMs mean/p50/p90/p99/max over15=…` 与
  `wasapiPull periods/pull mean/median/max over1.5=… totalPeriods=…`。
- 为什么它比 `pullMiss` 强：`pullMiss` 是「位置停走 >15 ms」的**稀有事件计数**（1/145/4 这种量级跨局不可比），
  而这里给的是**分布**——`periods/pull` 的 median 是不是 1.0、`over1.5` 占多少、间隔的 p99/max 多少，
  **单局**就能判「线程有没有按时醒」，也顺便读出 `BufferSize` 相当于几拍。

#### 2.4.17 MMCSS 确认加载但**没起作用** ⇒ 音频链结案；局内管线 pristine；退场段是另一个工况（2026-09-25）

全帧局 `framestall_20260925_101112.csv`（39.4 s / 72196 帧 / deep / 全程有按键）。

**一、这次确实是 MMCSS 的测试（交付问题已闭环）**

本地工程重建后（9:36:30）输出 `osu.Framework.dll` 内含 `mmcss` / `WasapiReadStats` 标记，
同局音频日志 `logs/1790302201.audio.log` 出现
`… requestedLatency=10ms, actualLatency=10ms, lowLatency=true, mmcss=Pro Audio (requested)`。

**二、直读量具把「漏一拍」从推断变成直读，而 MMCSS 没改变它**

| | A 局 `080438` | B 局 `084436` | C 局 `092213` | **D 局 `101112`（MMCSS）** |
|---|---|---|---|---|
| framework | 包 | 包 | 包 | **本地源码（MMCSS）** |
| `pullMiss`（位置停走 >15 ms） | 1 | 145 | 4 | **142**（3.6/s，hold 2874 ms） |
| NAudio 请求 ≥1.5 拍 / 总拉取 | — | — | — | **162 / 3834（4.2%）** |
| `periods/pull` max | — | — | — | **2.00**（正好两拍） |
| 拉取间隔 p50 / p99 / max | — | — | — | 10.25 / 20.25 / 20.71 ms |

`over15=162` 且 `max=2.00 拍` ⇒ 渲染线程确实**每 0.24 s 一次、一次要两整拍**（不是「读一点零头」）。

**结论：MMCSS（`"Pro Audio"`）没有把漏拉拿掉** —— 按 §2.4.14 下一步第 1 项事先写死的判据，**音频链就此结案**，
不再为它安排采集。剩下两种机制（线程晚醒 / `Read` 内部 BASS 解码慢）当前量具分不开，
但既然它对体感的贡献量级只有「位置误差 std ~1 ms」、且应用侧无手段消除，继续投入没有产出。

**三、局内帧管线是 pristine 的（逐秒重建）**

把 39.4 s 按秒摊开（`framestall_*.csv` 逐秒聚合）：

| 秒 | 帧数 | 帧长均值 | >5 ms | 子树均值 | 时钟停走帧 |
|---|---|---|---|---|---|
| 0–33（局内） | 1794–2040/s | **0.490–0.558 ms** | **除头两秒外全 0** | ≈0.10 ms | **0** |
| 34 | 1946 | 0.514 | 2 | 0.048 | 793（歌结束） |
| 35 | 2026 | 0.494 | 0 | 0.000 | 2025 |
| **36** | **192** | **5.200** | **115** | 0.000 | 191 |
| **37** | **120** | **8.328** | **120** | 0.000 | 119 |
| 38 | 668 | 1.501 | 80 | 0.000 | 667 |
| 39 | 408 | 0.470 | 1 | 0.000 | 407 |

**全局 327 个 >5 ms 帧里 316 个落在这 4.6 s 的退场段**（子树已拆 = 0、update 掉到 120–192 Hz）；
局内 33 s 只有 **11** 个。⇒ 局内帧时间 / 帧节奏与前四局一致，**没有新问题**。

**四、这条退场段顺手解释了两件之前读到过但没归因的事**

- `present coverage=0.869`：present 是**在 update 侧采样**的（每 update 帧读 `DrawThread.Clock`）。
  退场段 update 只有 120–190 Hz 而 draw 仍千帧级 ⇒ 每 1 s 漏掉近千个 draw 帧，Σ`drawPeriod` 比壁钟跨度少 13%。
  **不是量具坏了，是采样者自己掉速。**
- 四局 `noPress over5` 的诡异方差（7 / 10 / 17 / **326**）：各局在退场屏停留时长不同（`clockStopped` 1.7 s → 4.6 s），
  与局内无关。**读 `over5` 前先减掉退场段。**

**五、present 侧改动**

`samplePresent` 现在**跳过时钟停走的帧**（新增 `frozenSkipped=` 计数）。理由见上一条：退场段采样者掉速会整段漏掉
draw 帧、把 `coverage` 拉到 0.87 以下，使这张表在判读前就自检失败。帧耗时直方图**不**动（保持与既有四局可比），
判读时人工减去退场段。

#### 2.4.18 诊断开关改为**进程级**：关闭时热路径零开销（2026-09-25）

三个判定侧探针 + 时序追踪原先都在 `Player` 每次进图时从配置重读开关，`OrderedHitPolicyHelper` 还各持一个
`Bindable<bool>` 订阅。这套写法对「永远关着」的功能是纯负担，改法只有一条原则：**关闭时热路径上一个字节都不做**。

- **开关只在启动时读一次**（`OsuGameBase.applyDiagnosticSwitches`，紧接 `Ez2ConfigManager` 构造之后）：
  真值落到 `EzJudgmentDiagnostics.Enabled` / `EzPressLatencyDiagnostics.Enabled` / `EzFrameStallDiagnostics.Enabled`
  / `EzTimingTrace.Enabled` 四个**静态 bool** 上；`Player` 与 `OrderedHitPolicyHelper` 不再读配置、不再持 bindable。
  ⇒ **改设置必须重启游戏**（UI 的 tooltip 已写明）。
- **调用点自己先判**，让参数求值也省掉：
  - `FrameStabilityContainer.UpdateSubTree` 用本轮已读好的局部 `sampling` 守卫 `RecordFrame` 调用 ⇒ 关闭时连这次调用都不发生；
  - `Column.OnPressed` 早已用局部 `probe` 守卫整段采样（含那次会触发全树重建的 `NonPositionalInputQueue.Count`）。
- **能被 JIT 兑现的部分就到「一次静态加载 + 一次分支」为止**：运行期开关无法再便宜（要真正 0 指令只能 `#if`／
  `[Conditional]`，那要求重新编译、不能做成设置项）。所以别再从「把它做得更快」这里下手，
  要验的是**关闭时是否真的一个分支**：`EzJudgmentDiagnostics.Enabled` 为 false 时，上述四处调用点都只做一次 bool 判断。
- ⚠ 反面教材（已撤）：曾把开关做成 `Ez2ConfigManager` 的静态属性 + 环境变量 + 探针侧链式只读属性，
  热路径上一分没省、还多了一层概念 —— **不要重复这条路**。



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
| `AnalyzePeriod.py` | 离线读判定的 `Drift`/`TimeOffset`/`AudioLag`、帧的 `ElapsedMs`/`SpikeRate`/`GcPauseDeltaMs`、按键的 `PreColumnMs`/`FrameAgeMs`：ACF + Welch 谱 + 带内主周期细扫 + 滑窗幅度/相位 + 跨序列滞后表（含偶然水平标定与派生对剔除）。拒绝在尾部 frame 数据、离群帧主导方差的序列上出结论。见 §2.4.10 |

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
| 其他显示器播放视频（即使暂停）时帧率掉 200+ | 游戏内代码路径无对应开销 ⇒ 指向桌面合成 / GPU 抢占 / DWM 或驱动侧 | 与 §5 流程同规：另一显示器换静态图、换浏览器硬件加速开关、换输出模式，确认是否与游戏进程无关；结论记入本文件而非改游戏代码。**注意这一条与「送屏 / 扫描输出」同源 —— 进程内探针量不到，只能 A/B（交接单 §3.5 第 6 项）** |

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
| 2026-09-24 | §2.4.10：**周期性分析工具 `AnalyzePeriod.py`**——补上 §2.1 起就缺的 ACF/谱/相位这条腿（此前那两个 ACF 数字无脚本、不可复现）。同一局三份 CSV 的 `WallMs` 同源故可跨探针做互相关。五条实测踩出的防误读自检：事件型序列的分辨率=采样间隔（该带内只报「同时」，但**不可排除**该段——排除后真峰会退到周期旁瓣，实测两个完全相同信号曾被报到 −1.06 s）、带通后相关峰很宽故**贴边即不可定**（只有内部峰给先后）、**尾部 frame 数据不参与**（实测伪造 `TimeOffset × ElapsedMs r = −0.874`）、**离群帧主导方差时带内结论无效**（`RMS/稳健σ`，实测全段 15.7x → 修剪后 2.6x）、**带通后需家族性偶然阈值**（零分布实测 p95 0.503，66 对里至少一对 ≥0.47 的概率 99.2%；工具按 0.05/对数反解，并剔除定义性派生对）。现有三局（各 ~34 s）**测不出 1.5–5 s 带内稳定周期**，与 §2.1 一致 |
| 2026-09-24 | §2.4.10（续）：**38.7 s 全帧局的结果与一次假阳性**——未修剪首跑给出 `ElapsedMs × GcPauseDeltaMs r = +0.998`、`× ColumnMs +0.957`，实为 t=0 那个 **141 ms 首帧**在两个序列里的余振；`--trim 3,3` 后 `ElapsedMs` 带内 RMS **3.294 → 0.026 ms**（129x），全部内部峰落到家族性阈值 0.597 以下，唯一越线的定义性相关（`SpikeRate` := `elapsed ≥ 2×p50`）。**稳态帧长该带内仅 0.026 ms 抖动**（帧长均值 0.503 ms）⇒ §3「2–4 s 周期」前提证伪，下一步换观测维度（交接单 §3.5：note 位置按 update 帧量化 / BASS 缓冲 9.8 ms / 主观锚点定位） |
| 2026-09-24 | §2.4.11：**音频源时钟 = 精确 10.000 ms 阶梯（定案 §2.1 悬案）**——4 局判定 CSV 里 `BassSource` 的 331–353 个取值 **100% 落在 10.000 ms 网格**（最大残差 22 µs，相邻差只有 9.978/10.000/10.022 及其整数倍），而同批 `GameTime`（== `InterpClock`）无此结构；`Drift == (GameTime − BassSource) − 15.000`。机制：实机走 `NAudioWasapiOutput`（BASS 解码 mixer + NAudio 拉 WASAPI），`DEFAULT_NAUDIO_LATENCY_MS = 10`，而**解码 mixer 的位置只在被拉走一个 buffer 时前进** ⇒ 100 Hz 阶梯。**三个探针均按 ~10 Hz 采样 ⇒ 该结构被混叠，且会伪装成「隔几秒一次」的低频波动**，这正是 §3 在 0.2–0.6 Hz 带里空手而归的结构性原因（测量盲区，非效应弱）。用 1988 fps 真实帧间隔离线复刻 `InterpolatingFramedClock`：仿真 `Drift` 峰峰 **11.7 ms** vs 实测 9.8–19.2 ms（机制确认），每帧速率 std **4%**、**27.9% 的帧偏 >5%**（理想连续源为 0.36% / 0%），但相对匀速的**位置**偏差仅 std 0.11 ms / 峰峰 **2.8 ms**。1988 fps 下每台阶跨 **~24 帧** ⇒ 高帧率把该 100 Hz 纹波采样得更清楚。**保留**：仿真理想化、位置偏差是否够到体感尚未证明 ⇒ 下一步加每帧时钟探针（`RecordFrame()` 处顺带取 `BassSourceCurrentTime` / `InterpolatedDrift`） |
| 2026-09-24 | §2.4.11（续）：**每帧时钟探针落地**——`FrameStabilityContainer.UpdateSubTree` 帧边界处把 `gcc.BassSourceCurrentTime` / `gcc.CurrentTime` 交给 `RecordFrame(...)`（新增 `AudioSrcMs,InterpMs` 两列），摘要新增 `clockQuant` 行（`audioStep` 非零占比 / 步长主桶 / `interpRate` 的 mean-std-`within1%`-`over5%` / `drift` 范围，速率只在帧长 ≤5ms 的帧上算），`AnalyzePeriod.py` 新增 `AudioStep` / `InterpRate` 两条序列且 `derived_from` 改为元组（`InterpRate` 由两条时钟共同构造，单来源标记盖不住两个定义性对）。**仿真数据端到端验过**：`--band 0.004,0.02 --dt 0.002 --trim 3,3` 下 `InterpRate` 在 0.010s 给出 `ACF r = +0.896`、带内高出均匀背景 914x（`AudioStep` 必被 `RMS/稳健σ` 拒掉 —— 稀疏脉冲列的构造使然）。待实机全帧局验证 |
| 2026-09-25 | §2.4.11（续）：**实机音频设备确证**——`logs/1790263779.audio.log`（与全帧局同一次会话）：`wasapi="VoiceMeeter Aux Input (VB-Audio VoiceMeeter AUX VAIO)", 48000Hz/2ch float, requestedLatency=10ms, actualLatency=10ms, lowLatency=true`。48000 × 10ms = **480 帧**，与实测 10.000ms 量子精确吻合。`actualLatency` 非回显：NAudio 文档明确它是「设备**实际授予**的引擎周期」，且 `lowLatency=true` 表示 IAudioClient3 低延迟共享模式确实生效 ⇒ **低延迟开着，这台 VoiceMeeter 虚拟声卡也只给 10ms**（物理 DAC 通常 ~2.67–3ms）。**修正确认：调小 `DEFAULT_NAUDIO_LATENCY_MS` 无效（请求值不是瓶颈，授予值才是），要更小周期须换设备/模式**（物理 DAC、独占，或已在用的 ASIO —— 同会话日志有 `Found 7 ASIO devices` / `Freeing ASIO device`） |
| 2026-09-25 | §2.4.11（结论）：**音频时钟链洗清嫌疑**——36.5 s / 72463 帧全帧局（1983 fps，全程有按键）：`AudioSrcMs` 稳态 100.0 次/s、步长中位 **10.0000 ms**、间隔 std 0.466 ms ⇒ 精确 10 ms 阶梯无第二种量子；`InterpMs` 有 70641 个不同取值 ⇒ **不是阶梯**，量化确实被抹平。**位置判据**（报告值 + 距上次跳变的时长还原成连续位置）稳态 **std 0.408 ms**（整局 0.667），p99/max 2.64/39.0 ms，+10.25 ms 是**音频输出延迟**（被音频偏移吸收，用 500 ms EMA 在线估掉）。**std 0.41 ms 比 10 ms 缓冲量子小一个数量级** ⇒ 不构成体感，§3.5 第 1/2 项的修复方向不再需要动。⚠ 指标陷阱登记：`Δinterp/Δframe` std 0.28、`|rate−1|>5%` 占 **85%**，是抹平阶梯的**必然机制**（位置反而平滑），该序列已从摘要撤掉、`AnalyzePeriod` 侧同样不可用于判「顺不顺滑」；歌曲末 0.9 s 时钟停走单列为 `clockStopped`。**update 侧四条链（帧时间 / FSC 子树 / 按键延迟 / 音频时钟）至此全部测不出问题** |
| 2026-09-25 | §2.4.12：**present 侧量具落地**——`Game` 在本 fork 是 `Container` 而非 `GameHost`（反射确认 `DrawFrame/UpdateFrame` 是 `protected virtual` 但不在 `Game` 的继承链上），`OsuGameBase` override 直接 CS0115 ⇒ 改为**在 update 侧读 `DrawThread.Clock`**（`osu.Game` 的 `FPSCounter` / `LatencyCertifierScreen` 已有先例；`Clock` 由 draw 线程 `ProcessFrame`，属性任意线程可读）。摘要新增 `present` 行：`drawPeriod`（= `ElapsedFrameTime`，**相邻两次 present 的间隔分布**，看 `over0.5/1/2/5` 与 `max` —— update 侧帧长恒 0.5 ms 且不抖，draw 线程单独卡一下 update 探针看不见）、`presentAge`（上屏那一刻那份内容有多旧，零点取上一次 update 帧边界，受 **±1 帧配对不确定度** 限制，只判毫秒级以上滞后）、`clocks`（两线程 fps/jitter/slept/maxHz/throttling + 窗口态 + 刷新率）。两处实现要点：`StopwatchClock` 零点是自己 `Start()`（线程创建）故需**标定一次原点偏移**；失焦帧按 `IsActive` 剔除（`skipped`）。**不覆盖 DWM 组合 / 显示器扫描输出** |
| 2026-09-25 | §2.4.13–2.4.17：**音频源「漏拉」查到底并结案**。① 摘要新增 `interpErr`（位置判据）后立刻发现它会被源停走污染 ⇒ 登记「**判 `interpErr` 必须同时看 `pullMiss`**」（把 ±20 ms 邻域剔掉，std 1.08 → 0.455 ms）。② 稳态跳变只有 10/20 ms 两档、`跳变次数 = 周期数 − 超额周期数` 两局精确成立 ⇒ **机制**是「漏一次唤醒 → NAudio 一次读两拍」（读 `BufferSize − CurrentPadding` = 全部空位，`frameEvent` 是 AutoReset，故距离守恒、只重排相位）。③ 曾假设根因是渲染线程没登记 MMCSS：**先踩到交付陷阱**（MMCSS 只进了 framework 源码，而 `osu` 默认取 NuGet 包 ⇒ 那一局跑的还是旧 DLL；同时同代码三次采集 `pullMiss` = **1/145/4** ⇒ 单局计数不可判），补上 `wasapiRead`/`wasapiPull` 直读量具（拉取间隔 + 请求拍数）后，实测 D 局 `pullMiss=142`、`periods/pull max=2.00`、每 0.24 s 一次两整拍 ⇒ **MMCSS 无效，音频链结案**（应用侧无手段消除；体感量级只有位置 std ~1 ms）。④ 顺带定案「**歌曲结束后的退场段是另一个工况**」（时钟停走、update 掉到 120–192 Hz、子树 = 0、`coverage` 被拉到 0.869）：present 采样改为**跳过时钟停走的帧**（新增 `frozenSkipped`），局内前 33 s 的帧管线判为 pristine。**不成立/已撤回**：早期那两个 `Drift` ACF 周期数字已在交接单删除 |
| 2026-09-25 | §2.4.18：**诊断开关改为进程级**——三个判定侧探针 + 时序追踪原先每次进图从配置重读、且 `OrderedHitPolicyHelper` 各持一个 `Bindable<bool>`。改为**启动时读一次**（`OsuGameBase.applyDiagnosticSwitches`）落到四个静态 bool，调用点用局部量先判（`FrameStabilityContainer` 关闭时连 `RecordFrame` 调用都不发生），**改设置必须重启游戏**。理由：这类功能长期关闭，热路径上应当只有一个静态 bool 分支。⚠ 曾试过 `Ez2ConfigManager` 静态属性 + 环境变量 + 链式只读属性的写法，**热路径一分未省、只多了概念，已撤回**，不要重复 |

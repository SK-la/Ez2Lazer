# Ez2Lazer 性能 / FPS 排查（汇总活文档）

> **用途**：集中记录 **帧率下降、卡顿、性能测试口径** 三类内容，供以后再遇到掉帧时直接查历史结论，避免重复排查。
> **范围**：只收「性能与 FPS」相关描述。判定语义、数据来源、存储结构等仍留在各自文档，见 §8。
> **跨仓库**：`osu`（游戏侧）与 `osu-framework`（渲染 / 音频线程）都在本文件登记。
> **写法**：§2 起只保留**结论与关键数字**；实验过程、被证伪的中间假设、探针实现细节已删（需要细节翻 git 历史）。
> 每轮末尾的「不再重开」是本文件最贵的资产 —— **重开任何一条之前先读它**。
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

**两条读数的硬纪律**：

1. **跨帧率只能比倍率，绝对阈值不可比。** `p90/p50`、`超目标倍数` 才可跨局；`P(>1 ms)`「1 ms」这类绝对值在目标帧长就是 1 ms 的局里几乎每帧都「超」（§2.4.7）。
2. **跨局不可混算 p99 / max。** 各局条件不同（限帧倍率、vsync、音频输出模式、音频设备），把两段的 p99 相加得不到任何东西（§2.4.20）。

其余基线纪律：比较必须同一启动方式（IDE / 直接启动）、同一输出模式、同一皮肤，只改一个变量。**IDE 启动的数据不可与直接启动的数据横向比较**（§3.1）。

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
| 脚本皮肤在启动期把同一批 `.csx` 用 Roslyn 完整编译**两次**，只为读一次 `CreateInfo()` 显示名 | 目录扫描分「轻量占位」与「编译元数据」两遍，而编译结果既不缓存也不进实例缓存；预热又编译第二遍 | **SCRIPTED-SKIN-LAZY**：列表只按目录名生成条目，元数据与实例都在首次选中时才编译；另加总开关（默认关） | `osu` `b8d7002b42` |

---

## 2.0 当前基线标定（2026-09-26）

### 一、当前生效的操作点

| 项 | 当前值 | 理由 |
|---|---|---|
| 帧率 | `FrameLimiterBase = 250` + update/draw **均** `Limit4x`（目标 1000 Hz） | §2.4.7 六：帧间隔规整度 ↑、按键尾部 ↓5×，代价中位延迟 +0.29 ms。降到 500 fps 收益收敛，不推荐 |
| 限帧口径 | 改帧数只推 `FrameLimiterBase` 一条滑条，两个下拉都留 `Limit4x` | 两个上限各自独立但共用基准倍率；只降 draw 会让 update 空转产帧反而抢核 |
| 诊断套件 | 默认关；开启后热路径只剩静态 bool 分支 | §2.4.17 / §2.4.19 / §2.4.21 |
| 脚本皮肤 | `Ez2Setting.EnableScriptedSkins` 默认**关**；关闭时不扫目录、不装监视、不编译 | §2.4.22 |
| 音频输出 | 设备授予周期 **10 ms**（与请求值无关） | §2.4.10；换更小周期须换设备 / 模式 |

### 二、dotTrace 快照基线（同一机、同一 Release 构建，逐版收敛）

| 快照 | 时长 | UiFreeze | UiFreeze/s | Jitting | Jitting/s | FileIO |
|---|---|---|---|---|---|---|
| `20-39-36`（改动前基线） | 194.4 s | 5878 ms / 10 次 | 30.2 | 19105 ms | 98.3 | 2958 ms |
| `21-27-43`（P1 后） | 190.0 s | 3094 ms / 6 次 | 16.3 | 30668 ms | 161.4 | 4857 ms |
| **当前（P3 后）** | 239.1 s | **2094 ms / 4 次** | **8.8** | **21348 ms** | **89.3** | 4449 ms |

> 运行时长不同，只可用**每秒归一化**列比较。当前版是标定基准；前两版只作为「改动生效」的 A/B 证据保留 ——
> 11.5–15.5 s 启动窗口的 running CPU：改前 4214.2 ms（其中 `SandboxedScriptRunner.LoadScriptInfoAsync`
> 占 1778.7 ms = 42%，含 `CSharpCompilation.GetDiagnostics` 1653.3 ms），改后 2524.1 ms 且整棵 Roslyn 子树消失。

### 三、当前版里开销在哪（下一个目标）

- **整段运行**（running 共 445.1 s）：`SwapBuffers → Present → amdxx64.dll` **123.8 s = 28%**（驱动自身 75.7 s）是最大单项，是呈现 / 限帧在驱动里等，**不是游戏逻辑**；`UpdateFrame` 117.2 s（26%）。子系统：Native 5146 s / System 3634 s / **User code 1287 s** / Sleep 890 s / GC Wait 87.6 s / **JIT 20.8 s** / Collections 5.8 s / Lock contention 0.055 s。
- **启动窗口 0–20 s**（running 36.0 s）现在第一大户是**字体族枚举**：`OsuGameBase.InitialiseFonts → RegisterLocalizedFallbacks → EzSystemFontCatalog.FindByFamily → GetEntries` = **1.772 s（5%）**，其中 `OutlineFont.TryGetFamilyName` 有 **608 ms 直接烧在 freetype 内**；其次 `bootstrapSceneGraph` 4.973 s、`EzRealmRulesetStore..ctor → PrepareDetachedRulesets` 0.570 s。**该项已按 §2.4.23 加族名落盘缓存（热启动预期归零，待下一次快照复核）**。
- **反常点（未定案）**：当前版 4 次 UiFreeze 全部落在最后一个桶（234.4–239.1 s），前 234 秒一次都没有；那段 CPU 大头是 `GameThread → UpdateFrame → BeatmapCarousel.Update`（3.08 s，占该段 37%）。多为退出 / 保存路径，未深究。

---

## 2.1 2026-09-20 输入 / 取样热路径

### 已落地

| 项 | 位置 | 说明 |
|----|------|------|
| **INPUT-QUEUE-FRAME** | `ManiaInputManager.ManiaKeyBindingContainer` | 一次枚举分成「列 / 非列」两个复用列表（列优先顺序不变），并在 `Update()` 失效以**按帧缓存**。原先每次读 `base.KeyBindingInputQueue` 都清空重建，一帧内 N 键齐按 = N 次重建；现在一帧一次 |
| **SAMPLE-NO-LINQ** | `GameplaySampleTriggerSource` / `DrawableManiaHitObject` | `GetMostValidObject` 去掉 LINQ 与递归 `getAllNested`，改单遍最小扫描 + 显式栈先序（并列取舍规则与上游一致）；`Play()` / `PlaySamples()` 手动填充数组代替 `Cast().ToArray()` |
| **LN-INPUT-SLOT** | `DrawableHoldNoteHead` / `DrawableHoldNoteTail` | 空实现移出非位置输入队列 |
| **LN-HOLD-FBO** | `DefaultBodyPiece` | 按住隐藏减法层并跳过 `ForceRedraw`；松手恢复。仅 Default/Triangles |
| **FW-BUTTON-QUEUE-REUSE** | `osu-framework` `ButtonEventManager<TButton>`（`56e9beb2c`） | 每个按钮各持一份按下队列缓冲：按下时整体重填（替代 `InputQueue.ToList()`），抬起时就地压缩掉已脱离输入树的项（替代 `Where(...).ToList()`）。快照语义不变 |

### 已评估但**不做**（重开前先读）

| 候选 | 结论 |
|------|------|
| `Column.OnPressed` 返回 `true` 让 `PropagatePressed` early-out | **不可行**。成功后的 `inputQueue.RemoveRange(...)` 会截断该 binding 的**同一条缓存列表**，而 `handleNewReleased` 用同一条列表派发 Release。列在队列头部 ⇒ `ReplayRecorder` / `KeyCounterActionTrigger` 收不到 Release（回放丢 KeyUp、按键显示粘键）。要做得先把观察者排到列之前并按类型识别，收益不抵风险 |
| **FW-INPUT-QUEUE-DISPATCH**：一次派发窗口内共享一次队列构建 | **已被测试证伪**（改动已撤回）。前提「窗口内可进入队列的 drawable 不变」不成立：`TestSceneInputQueueChange.CombinedClicks` 在共享构建下必失败 —— drawable 在 `OnMouseDown` 里 `MoveToX`（duration 0）后，同帧第二次按键必须看到**按新位置重建**的队列。按调用点重建是刻意的新鲜度语义，不可跨事件共享；框架侧只保留去分配 |
| 去掉通道级 `BindAdjustments`（每击 4 路 `AddSource`） | **不可行（Ez 侧）**。`PlaybackConcurrency` / 音量 / 平衡 / 频率都必须跟随 `drawableRuleset.Audio`，其中频率来自 `ModRateAdjust` 系列；`AdjustableAudioComponent.Adjustments` 是 `protected internal`，Ez 无法只绑部分属性。真正做法是框架级通道复用（BASS 通道无法重播，见 `SampleChannelBass.playInternal` 的 `Played` 守卫） |
| `Column.OnPressed` 每键都 `sampleTriggerSource.Play()` | 已有代号 **SOUND-DECOUPLE**（`HIGH_KPS_JUDGE_BACKLOG.md`），**附条件**：仅在 profile 证明其阻塞时做。`KeySoundPreviewMode.Off` 目前也会触发取样（只有 `AutoPlayPlus` 排除），会让按键与 note 音互相 choke |
| 爆炸池 `DrawablePool<PoolableHitExplosion>(5)` 扩容 | **无依据**。爆炸按列产生，每列一次按键最多 1 个，初始容量 5 已覆盖；仅在单列 >25 hit/s 的极端 jack 下才会增长 |

---

## 2.2 2026-09-23 局内判定 / HUD 热路径

### 已落地

| 项 | 位置 | 说明 |
|----|------|------|
| **HITPOS-CACHE** | `DrawableManiaRuleset` | `updateTimeRange()` 原先**每帧**调 `skinChanged()`（每帧构造 `ManiaSkinConfigurationLookup` 走一次皮肤配置链）。改为 `updateHitPosition()` 只在皮肤 / `HitPosition` / `HitPositionGlobalEnable` 变更时重算并缓存字段。判定线绑定前置到 `ScrollStyle` 之前，保证首次 `updateTimeRange()` 读到已算好的值 |
| **JUDGE-NO-CLOSURE** | `Column.OnNewResult` / `Stage.OnNewResult` | `hitExplosionPool.Get(e => e.Apply(result))` 与 `judgementPooler.Get(type, j => j.Apply(...))` 每判定各造一个捕获闭包。两者 `Apply` 都只做字段赋值，改为 `Get()` 后再赋值。注意 `JudgementContainer.Add` 会**同步**读 `JudgedHitObject`，赋值必须仍在 `Add` 之前 |
| **POLICY-SCRATCH** | `OrderedHitPolicyHelper` | 候选 / 后判对象改复用缓冲（去掉每次判定的 `ToList()`）；`OrderBy(...).ToList()` 改复用缓冲上的稳定插入排序；诊断串整段门控在诊断开关之后；命中模式缓存 bindable，取代一次判定里 3 次 `ezConfig.Get` |
| **FORCEMISS-SNAPSHOT** | `Column.handleHit` / `ManiaLaneController.CollectForceMissBefore` | 命中时「提前判 miss」的 `yield` 迭代器改为写调用方缓冲（每次命中省一个迭代器对象），处理时按实时 `IsPressJudged` 复核以保持惰性枚举语义 |
| **MARKER-EASE-FIX** | `EzHUDHitTimingColumns.moveMarker` | `marker.Y = targetY;` 紧接着 `MoveToY(targetY, 800, OutQuint)`：设值后新变换的 `StartValue` 由 `ReadIntoStartValue` 在首次 `Apply` 时读到，即 `start == end` ⇒ 缓动恒为空变换、标记瞬跳。这行是从上游 `BarHitErrorMeter` 那条**一次性池化**判定线抄来的，常驻 marker 不该有。删掉后恢复上游 `arrow` 的「只发缓动」语义。附带：`MoveHeight` 改高时先 `ClearTransforms(false, "Y")` 再按比例赋值；`StopMovement` 开启时 `FinishTransforms(false, "Y")` 收尾到目标 |

### 已评估但**不做**（重开前先读）

| 候选 | 结论 |
|------|------|
| 缓存 `DrawableManiaHitObject.PlaySamples` 的 samples 数组 | **不可行**。`Bindable<T>.Value` setter 在 `EqualityComparer<T>.Default.Equals` 相等时直接 return（数组即引用比较），缓存同一实例会让 `GameplayState.LastPlayedSamples` 对重复同音不再触发变更，而 `StoryboardTriggerController` 正消费它。数组必须每次新建（同 **SAMPLE-NO-LINQ**） |
| HUD / 皮肤件「复用单个 `TransformSequence`」合并动画 | **不可行（框架约束）**。`TransformSequence<T>` 构造时固化 `startTime`/`currentTime`，复用实例再 `Append` 会把 transform 排到过去 ⇒ 瞬跳；且 `PopulateTransform` 对同一 `Transform` 实例二次调用直接抛异常，即 transform 天然一次性、框架无池 |

---

## 2.3 lane controller 索引维护：**实测结论不改**（2026-09-23）

口径：`ManiaLaneHotPathMicroBenchTest`（跑真实 `ManiaLaneController`，10 列 × PeakKps 100 × alive 40 × 2000 帧，含 Select + automiss deadline 队列 + `pressTimes`）。

| 指标 | 实测 | 判读 |
|------|------|------|
| 总耗时 | 21–33 ms / 2000 帧（Earliest / Combo / Duration 分别 33 / 28 / 27） | ≈ 10–16 µs/帧，约为 16.6 ms 帧预算的 0.1% |
| 分配 | 15–128 B/press，`gen0 = 0` | 不构成 GC 压力 |
| automiss 队列 | 每帧每列 1 次 poll，`dueVisits` ≈ 22/poll（alive 40） | 已含在上面耗时里，不是瓶颈 |

⇒ `insertEntryAt` / `Unregister` / `autoMissEntries` 的 O(n) 维护即使在 100 KPS / 40 存活每列的极端设定下也不进热榜，**不为它改数据结构**。BMS poor-select（`AllowBmsFallbackToEarliest` + `PoorEnabled`）同样 30 ms 量级、128 B/press。

---

## 2.4 按键延迟与帧节奏实测（2026-09-24 ~ 09-26）

工具：`EzPressLatencyDiagnostics`（按键分段）+ `EzFrameStallDiagnostics`（帧级 stall）+ 分析脚本 `AnalyzePressLatency.ps1` / `AnalyzeFrameStall.ps1` / `AnalyzePeriod.py`。开关模型见 §2.4.19 / §2.4.21。

> **读这些数字前先读本小节**：早期几局的探针采样率是 ~10 Hz，而音频时钟是 100 Hz 阶梯 ⇒ **该结构被混叠、会伪装成「隔几秒一次」的低频波动**，这正是 §3 在最开始 0.2–0.6 Hz 带里空手而归的结构性原因（测量盲区，非效应弱）。

#### 2.4.1 本局条件（先看这个，否则数字会被误读）

一局 mania：194,002 帧 / 98.6 s ≈ **1968 更新帧/s**（`FrameElapsedMs` p50 = 0.474 ms）；按键 1068 次（12 次/s，空按 13.7%）；>2 ms / >5 ms 帧 = 1 / 0；GC 暂停累计 1565 ms（1.75%，含后台，是上界）、gen2 = 0。

⇒ **这不是高压场景**：更新线程相对 16.6 ms 帧预算约有 35× 余量，所以本局任何亚毫秒数字都不能用来解释体感 4 ms。

#### 2.4.2 已定案

| 结论 | 关键证据 |
|------|------|
| **`PreColumnMs` 不是代码开销，是「等下一帧来处理这条输入」** | 空按与真判定的等待**相同**：`routed=0` p50 **0.625 ms** / `routed=1` p50 **0.558 ms**，而两者实际工作量差 20 倍（`ColumnMs` 0.005 vs 0.103 ms）。等待与 `FrameAgeMs` 强相关：age 0–2 ms → 0.557 ms，age 2–5 ms → 1.830 ms |
| **按键自身的 inline 工时极小且不随物量增长** | `ColumnMs`（`OnPressed` 全程，含判定 + 同步结果扇出）p50 **0.101 ms** / p90 0.134 / max 1.127；空按 p50 **0.005 ms**。列内条目 0–5 都有样本，未随 `Entries` 增长 |
| **按键帧确实更长，但大头不在判定代码里** | 按键帧 p50 1.150 ms vs 非按键帧 0.450 ms（P(>1 ms) 27.4×）；而 `PressColumnMs / ElapsedMs` 仅 p50 **6.2%**。差额落在本帧其他位置（判定落地后的 sprite/HUD、`Schedule` 出去的取样播放、draw） |
| **大停顿与按键无关** | >8 ms 的帧只有 3 个，全部 `presses=0`，`FrameIdx` = 2、194002、194003 ⇒ **开局首帧与退出帧**。按键帧 `max` = 3.851 ms、P(>5 ms) = 0 |

#### 2.4.3 埋点自身的三个坑（登记以免重犯）

1. **只在 >阈值的帧上做「按键帧 vs 非按键帧」对比会得出错误结论。** 条件在 `ElapsedMs > 1.5 ms` 上会让两组都被拉平（实测两组 p50 只差 0.02 ms），从而误判「按键与慢帧无关」。正确做法是双直方图 —— 记录**全部**帧并按按键存在与否分组。
2. **`SincePrevFrameMs` 的零点在上一帧边界，不是「本帧按键前的工作量」。** 它含上一帧剩余时间、draw/present 与帧间等待。只有 `PressColumnMs` 是按键自身工作。（§2.4.7 五进一步证明它**装了一个整帧**：B 组读数 1.542 ms > 帧间隔 1.001 ms。）
3. **`currentFrameStartTimestamp` 曾滞后一整帧。** `RecordFrame()` 在帧末写入它，而 `NotifyPress` 在同一个帧内、`RecordFrame` **之前**执行，因此当时读到的是**上上帧**边界；修正前 `SincePrevFrameMs` 被虚增约一个帧长（实测 1.330 vs 1.344 ms，并让三段切分出现 **−0.171 ms** 负值）。修正为 `= now`。**修好前的那一行数据不可用于结论**（`PreColumnMs`/`FrameAgeMs` 来自另一个探针，不受影响）。

#### 2.4.4 明确不再重开的两个方向

- **输入队列整树重建**：§2.1 的 `INPUT-QUEUE-FRAME` / `FW-BUTTON-QUEUE-REUSE` 已落地；框架侧共享构建被 `TestSceneInputQueueChange.CombinedClicks` 证伪。
- **lane controller 结构**：§2.3 已实测不改。

#### 2.4.5 帧数上限与「下落不顺滑」同源（2026-09-24）

需求变更为**不降帧、继续提帧，同时解决下落不顺**。三条结论：

1. **没有任何限速器在起作用，1968 fps 就是纯工作量上限。** `Unlimited` 实际被夹到 8000 Hz（`FrameSyncExtensions.applyLimit` 末行 `Math.Min(8000, …)`），而 `throttle()` 只在工作量 < 1/8000 s 时才 sleep；实测 0.450 ms/帧 ⇒ 一次都没睡过。mania 判定只占每帧 ~10–16 µs（**<3%**）⇒ 提帧只能靠砍每帧工作量，改任何设置都无效。
2. **每帧持续分配 ~2 KB，与 20–60 ms 的卡顿鼓包吻合。** `alloc(updateThread)` 3.9 MB/s ÷ 1968 帧 = **~2 KB/帧**（持续量，非突发）；gen0 **24.4 次/s**（每 41 ms 一次），单次暂停 ~0.77 ms ⇒ **1.9% 的时间在 gen0 停顿里**。
3. **note 位置按 update 帧量化，所以帧间隔抖动 = 下落抖动。** `ScrollingHitObjectContainer` 每帧用 FSC 暴露的 `framedClock`（`manualClock.CurrentTime` 每轮 update 采样一次音频时钟）执行 `updatePosition`。呈现位置 = 音频时钟在**离散 update 时刻**的采样值 ⇒ 帧间隔抖动 × 下落速度 = 位置跳变。**提帧之所以能改善顺滑，正是因为它在缩短这个量化步长** —— 两个目标是同一个目标。框架的 `InterpolatingFramedClock` 解决不了（未用于 gameplay，且 `CurrentTime` 同样只在 `ProcessFrame()` 内更新），**此路不通，不必再试**。

**`frameSplit` 行**把一轮 update 切成三段：`subtreeMsMean`（`base.UpdateSubTree()` = 播放区 / 物件 / 判定线）、`clockMsMean`（`updateClock()` = 音频时钟采样 + `ReplayInput` + 子帧校正）、`restMsMean`（帧长减上两者 = HUD / 框架调度 / 掩码），加 `loopAllocMean`（FSC 全程每帧分配）。

#### 2.4.6 首次 frameSplit：**73% 的帧时间与 88% 的分配在 FSC 之外**（2026-09-24）

一局 mania，174,587 帧 / 91.0 s ≈ **1918 帧/s**。

| 项 | 实测 |
|----|------|
| 帧长 | 0.522 ms |
| **FSC 子树**（mania 播放区 / 物件 / 判定线） | **0.142 ms = 27.2%** |
| FSC 时钟推进 | < 0.0005 ms（不可分辨，非瓶颈） |
| **其余**（HUD / 框架调度 / 掩码 / 其他 screen / overlay） | **0.379 ms = 72.6%** |
| 全进程 update 线程分配 | 4.0 MB/s ÷ 1918 ≈ **2.1 KB/帧** |

**⚠ 后来已修正（§2.4.7）**：这 0.379 ms 里绝大部分是**节流 sleep**，不是真实工作量。真正逐帧恒定的只有 `subtreeMsMean`（0.105–0.116 ms，跨倍帧率不变）。

同一局的其余症状：stall 帧（770 个）update 分配 p50 **32 KB** / p90 163 KB / max 716 KB（平常帧的 16–340 倍，属突发）；770 个 stall 帧中 **266 个 GC 占比 ≥50%**、426 个 10–50%、只有 78 个 ≤10% ⇒ **GC 是多数 stall 的直接原因**。GC 之外的较大 stall：`Elapsed` 7–9 ms 而 `GcPause = 0`、alloc 从 1.6 KB 到 166 KB 不等（一条只有 1.6 KB 分配却卡 8 ms），指向线程外因素，91 s 内约 6 次（0.07 次/s）。最大的 86.257 ms 在 `FrameIdx = 2`（开局首帧 catch-up），不是稳态问题。

已验证的 FSC 之外每帧开销（框架侧，非主因但确凿）：`TooltipContainer.Update` → `CursorEffectContainer.FindTargets()` 每帧对 `InputManager.PositionalInputQueue` 做一次 `IndexOf` + 一次反向遍历，并在有候选时 `new List<TTarget>(...)` + `Reverse()`；量级 O(位置输入队列长度)，无候选时不分配，未测占比。

#### 2.4.7 帧率上限对比：base 500/Limit4x（≈2000 fps）vs base 250/Limit4x（≈1000 fps）（2026-09-24）

配置改成 `FrameLimiterBase = 500` + 两线程均 `Limit4x` ⇒ 目标 2000 Hz，实测 1979 帧/s，**限制器已接管**（dotTrace 中 `WindowsNativeSleep.Sleep` 占 19%）。

| 组 | base | 目标 | 实测 |
|----|------|------|------|
| A | 500 | 2000 Hz | 66,903 帧 / 33.8 s = **1979 fps** |
| B | 250 | 1000 Hz | 33,329 帧 / 33.4 s = **998 fps** |

**一、两局都不是 CPU 瓶颈。** `elapsedMean` 几乎就是帧间隔本身（0.505 / 1.001 ms）⇒ 帧长由限制器给的 sleep 决定。关键：`restMsMean` 从 0.399 → 0.884 ms，**增量 0.485 ms ≈ 帧间隔增量 0.496 ms** ⇒ 它就是被节流的 sleep。真正逐帧恒定的只有 `subtreeMsMean`：0.105 ms（A）vs 0.116 ms（B），**跨倍帧率不变**。

**二、帧间隔规整度：1000 fps 明显更稳。**

| 指标 | A（2000 fps） | B（1000 fps） | |
|------|------|------|---|
| 帧间隔 p50 | 0.450 ms | 1.050 ms | |
| **p90 / p50** | **1.89** | **1.19** | ↓ 37% |
| p99 / p50 | 2.78 | 1.67 | ↓ 40% |
| **超 2× 目标的帧** | **3.07%** | **0.65%** | ↓ 4.7× |

**三、按键→判定：中位略升，尾部降 5 倍。**

| | A | B | |
|---|---|---|---|
| `PreColumnMs` p50 | 0.784 | 1.069 | +0.29 ms |
| `PreColumnMs` p99 | **35.445** | **6.764** | ↓ 5.2× |
| `ColumnMs` max | **33.492** | **2.120** | ↓ 15.8× |

`ColumnMs` max 从 33.5 ms 掉到 2.1 ms ⇒ **A 组的大延迟不是判定代码算得慢，而是被 stall 帧吞掉的**。

**四、稳态 ≥5 ms 卡顿：两组次数相同（各 11 个，≈0.33 次/s），这是帧率无关的地板**（符合「GC 由分配速率而非帧数驱动」）。差别只在幅度：A 组最大 43.6 / 41.5 / 34.4 / 24.1 / 21.4 ms，B 组最大 18.9 / 10.3 ms。A 组那个 131.5 ms 极值是 `FrameIdx = 2`（开局首帧），**不能当「1000 fps 更顺」的证据**。`alloc` 速率 A 5.05 → B 3.16 MB/s（↓37%），但 `gcPause` 占比几乎不变（2.68% → 2.54%）。

**五、探针语义修正 + 一个待证假设。** `SincePrevFrameMs` 装了一个整帧，真实相位 = 该值 − 帧间隔：A 0.583 ms / B 0.541 ms。**帧率差一倍而两值几乎相同**，而按随机落点相位应正比于帧间隔（A 期望 0.25 ms、B 期望 0.50 ms）⇒ A 组超出均匀落点可解释范围约 0.3 ms。**假设**：输入路径存在 **≈0.55 ms、不随帧率缩放的固定投递延迟**（SDL 事件 → input 线程 → 队列 → 本帧 `InputManager.Update`）。n ≈ 300、单点对照，**待证实**，与 `EzSubFrameCorrection` 的存在动机同源。

**六、结论与操作点**：采用 **base 250 + 两线程 `Limit4x`（1000 fps）**；继续降到 500 fps 收益收敛（≥5 ms 卡顿是帧率无关地板，而中位延迟还要再 +1 ms），**不推荐**。剩余真实杠杆只有两个、且都与帧率无关：**GC**（见 §2.4.8）与那个 **≈0.55 ms 输入地板**。

**七、Draw 线程成本结构（dotTrace Timeline，窗口 17–60 s / 91,094 ms running）**

| 项 | 占 `DrawFrame` | 占全窗口 |
|---|---|---|
| `CDXGISwapChain::Present` | **78%** | **25%** |
| `WindowsNativeSleep.Sleep`（节流 sleep） | — | 19% |
| `GameHost.UpdateFrame` 整个更新帧 | — | 30% |
| └ 其中 **FSC `UpdateSubTree`（mania 规则集全部）** | — | **仅 5%** |

按帧摊算：**每次 `Present` ≈ 0.27 ms，其中 0.25 ms 在 AMD 驱动内部** ⇒ 这是**固定成本 × 次数**（2000 fps 时约 495 ms/s，几乎吃满一个线程；降到 1000 fps 直接减半）。**提帧有收益上限**。它与「送屏 / 扫描输出」是同一条 swap chain 的两端但量法不同：驱动内这段在进程内、可由 profiler 量清；`Present` 返回后的 DWM 组合与扫描输出在进程内量不到，只能实机 A/B（§2.4.11）。

> ⚠ 该 91 s 窗口含进出图与选歌过渡，桌宠 `EzPetCubismMeshView` 在其中一度占分配 14%；**纯局内**窗口（25–55 s）完全不出现，与 `DesktopPetShowOnGameplay = False` 一致 ⇒ 早期草稿「桌宠是局内分配热点」的判断**作废**。

#### 2.4.8 GC 归因：分辨「GC 造成卡顿」与「GC 恰好同帧」（2026-09-24，修正 §2.4.6）

deep CSV 逐帧记了 `GcPauseDeltaMs`（跨该帧的 `GC.GetTotalPauseDuration` 增量），可**不需要 profiler** 直接与 `ElapsedMs` 相关。判据：两者**同量级**才算 GC 造成；远小于帧长则**可排除**。下表只取稳态（`FrameIndex ≥ 1000`）。

| | A：base 500（1979 fps） | B：base 250（998 fps） |
|---|---|---|
| 该局 GC 总量 | 904 ms（2.68% 墙钟） | 848 ms（2.54% 墙钟） |
| **其中落在 stall 帧内** | **563 ms = 62.3%** | **581 ms = 68.6%** |
| >5 ms：ratio ≥50% / <10%（可排除） | **0% / 72.7%** | **81.8% / 9.1%** |

1. **GC 时间高度集中在慢帧。** stall 帧不到总帧数的 3%，却装下了 62–69% 的 GC 暂停时间 ⇒ 「2.5% 墙钟」这个平均口径会**低估** GC 与卡顿的关系。
2. **中等卡顿（1.5–2 ms）GC 是主因**，尤其 A 组（65.8% 的帧其 GC 与帧长同量级）。
3. **最重的卡顿（>5 ms）两组结论相反，不能合并叙述**：**B 组** 11 个 >5 ms 帧里 9 个就是 GC（ratio 0.73–0.89），但 `FrameIdx` 全落在 1040–1977 = **歌曲开头约 2 秒**（开局分配 / 纹理 / 物件创建），之后整局再无；**A 组** 11 个里 **0 个**达到 ratio 50%、72.7% 连 10% 都不到（明细 7.06 / 43.59 / 20.31 / 34.42 / 41.51 / 5.78 / 6.58 / 7.57 / 9.68 / 6.50 / 21.37 ms，对应 GC 只有 0–2.33 ms）⇒ **GC 被明确排除**。

⇒ §2.4.6「稳态 ≥5 ms 卡顿两组都是 11 个」**不能当成同一个地板**：一边是开局 GC，另一边是成因未明、5–44 ms、与 GC 和帧率都无关的停顿。**这一组无 GC 的重卡顿，比 GC 更值得优先查。**

**分配归属**：分配 92–93% 在 FSC 子树之外（子树内仅 168 B / 270 B per frame，而 update 线程每帧 2552 B / 3162 B）⇒ ruleset / 播放区不是分配源。

**已实测排除毛玻璃**：`EzBoxElement`（`AcrylicBackdropDrawable`）与 mania `Stage.stageBackdropBlur`（按 `ColumnBlur = 0.3` → `sigma = 15` 判定为**开启**）的开关对局内 draw 帧数无可见差别。⚠ **量具敏感度**：`FrameSync = Limit4x` 下 FPS 被锁在 1000，该数字本就不动，故此结论只说明「acrylic 不显眼」，**不能**推广成「每帧分配不重要」。

**卡顿的时间分布**：**2–5 ms 类整局均匀**（两组每个千帧桶都有 0–18 个 >2 ms 帧、无开局热点，却是全部 stall 帧的 **95–97%**）；**>5 ms 类高度集中在歌曲开头**（首 2 s 只占全程 6%，却装下了 1000 fps 的 9/11 与 2000 fps 的 7/11）。⇒ 应拆成两件事：**均匀背景**（GC + 每帧分配）与**开局约 2 s 的突发**。

**>5 ms 帧的逐帧归因**：2000 fps 组 4 帧几乎全在 ruleset 子树内（`SubtreeMs/ElapsedMs` = 99.4% / 95.9% / 93.5% / 85.1%）而 GC 与分配 ≈0；1000 fps 组 9 帧 GC 主导（GC 4.13–5.70 ms），另 1 帧单帧分配 **1.54 MB**。⚠ `SubtreeMs` 是**墙钟**，线程被 OS 抢占同样计入 ⇒ 那 4 帧「子树内 5–43 ms」既可能真算得慢、也可能被抢占，**需要「每帧 CPU 时间 vs 墙钟」的读数才能分开**。

#### 2.4.9 周期性分析工具 `AnalyzePeriod.py` 与「2–4 s 周期」的现状（2026-09-24）

§2.1 里那两个 ACF 数字（`Drift` 3.10 s / 0.135，9.47 s / 0.164）当时是临时算的，**仓库里从来没有对应工具**，不可复现也无法核对 —— **⛔ 已作废，不要引用**。新增 `AnalyzePeriod.py`（numpy，可选 matplotlib）补上这条腿：三探针 `WallMs` 同源故**同一局**三份 CSV 可直接跨探针对齐；输出逐序列 ACF 主峰 + Welch 谱主峰 + 带内主周期细扫 + 滑窗幅度/相位 + 跨序列滞后表；显著性用 `ACF r` 与「带内主周期占带内能量 / 均匀背景」，**不用「峰 / 中位」**（在 1/f² 型背景上后者恒为几百倍，会把噪声报成显著）。

**工具自带五条防误读自检（都是实测踩出来的）**：

1. **事件型序列的分辨率 = 采样间隔。** judgment / press 都是 ~10 次/s，插值到 20 ms 网格后，一个采样间隔内的读数与「同时」不可分辨，故只报「同时」不报毫秒数。**但绝不能把这一段直接排除** —— 真峰落在这里时剩下的 argmax 会停到周期旁瓣上：实测两个**完全相同**的信号曾被报到 −1.06 s，比不设限更错。
2. **带通后的相关峰很宽，贴边即不可定。** 峰的 argmax 落在搜索上限（±最短周期）时只报「贴边」+ r，不报滞后。**只有严格内部的峰才给出「谁领先」**。
3. **尾部 frame 数据必须排除。** 阈值 > 0 的 `framestall_*.csv` 只有慢帧，块均值桶大量靠插值填补，会伪造极强虚相关（实测给出 `TimeOffset × ElapsedMs r = −0.874 @ 420 ms`，看着像定案，其实是纯伪影）。非全集 frame 序列现被标 `⚠` 且**不参与相位表**。
4. **离群帧主导方差时带内结论无效。** 用 `RMS / 稳健σ(MAD)`：同一局 `ElapsedMs` 全段 **15.7x**（方差被 t=0 那个 141 ms 首帧独占），`--trim 3,3` 后降到 **2.6x**。超 `OUTLIER_RATIO = 5` 的序列不参与相位表。
5. **独立样本太少，|r| 的偶然水平极高 —— 必须标定。** 独立样本 ≈ 跨度/周期 ≈ 13；实测零分布（6000 次独立对）单对 `p95 = 0.503`、`p99 = 0.572`，于是 66 对里「至少一对 ≥ 0.47」的概率是 **99.2%**。工具按 `0.05 / 对数` 反解家族性阈值（31 对时 = **0.597**）并逐行标注是否越线。**没有这条基线，看表的人必然把偶然峰当相位来源。**

**结果**：现有三局（各 ~34 s）**测不出 1.5–5 s 带内的稳定周期**（全部序列 `ACF r ≤ 0.20`）。全帧局（38.7 s / 76985 帧）第一次未修剪跑出 `ElapsedMs × GcPauseDeltaMs r = +0.998`、`× ColumnMs +0.957` 的假阳性，回头查是同一个 141 ms 首帧的余振；`--trim 3,3` 后 `ElapsedMs` 带内 RMS 从 **3.294 → 0.026 ms**，全部内部峰落到阈值之下 ⇒ **稳态帧长在该带内只有 0.026 ms 抖动**（帧长均值 0.503 ms），**§3 的「2–4 s 周期」前提被证伪**。

> ⚠ `InterpRate` 是条**会骗人的序列**：它在 0.010 s 上有强 ACF 峰，但那是**逐帧速率**的摆动（速率 std 0.28、`|rate−1|>5%` 占 85%），而**位置**只偏离平滑线 0.41 ms。⇒ 判「顺不顺滑」只能看位置（帧 CSV 的 `InterpMs` / `AudioSrcMs`），不能用速率。

#### 2.4.10 音频源时钟是精确 10 ms 阶梯 —— §3 空结果的结构性原因（2026-09-24 晚）

**定案：`BassSource − GameTime` 峰峰 9.8–19.2 ms 是缓冲量化，不是音频抖动。**

| 序列 | 落在 10.000 ms 网格 | 说明 |
|---|---|---|
| `BassSource`（音频源时钟） | **100%**，最大残差 **22 µs** | 相邻差只有 9.978 / 10.000 / 10.022 及其整数倍 |
| `GameTime`（= `InterpClock`） | ~3%（均匀） | 连续，无网格结构 |

**机制**：实机走 `NAudioWasapiOutput`（BASS 解码 mixer + NAudio 写 WASAPI 共享模式），请求延迟 `DEFAULT_NAUDIO_LATENCY_MS = 10`；`TrackBass.CurrentTime` 取 `BassMix.ChannelGetPosition`，而**解码 mixer 的位置只在 NAudio 拉走一个 buffer 时才前进** ⇒ 一次跳 **10.000 ms = 100 Hz**。

**实机日志确证**（`logs/1790263779.audio.log`）：VoiceMeeter 虚拟声卡、48000 Hz × 10 ms = **480 帧**，与实测量子精确吻合；`actualLatency=10ms` **不是回显请求值**，而是设备**实际授予**的引擎周期，且 `lowLatency=true` 表示 IAudioClient3 低延迟共享模式确实生效。⇒ **低延迟模式开着，这台虚拟声卡也只给 10 ms**（物理 DAC 通常 ~2.67–3 ms）。**调小 `DEFAULT_NAUDIO_LATENCY_MS` 无效 —— 请求值不是瓶颈，授予值才是**；要更小周期须换设备 / 模式（同会话日志有 `Found 7 ASIO devices`，是现成选项）。

**为什么三次测量都没看到它**：三个探针采样率都是 **~10 Hz**，100 Hz 信号按 10 Hz 采样只产生**混叠的低频幻影**，正好伪装成「隔几秒一次」的波动 —— 而 §3 一直在 0.2–0.6 Hz 带里找周期。**这是测量盲区，不是效应弱。**（离线仿真：1988 fps 下每个台阶跨 ~24 帧，`Drift` 峰峰 11.7 ms vs 实测 9.8–19.2 ms，机制确认；60 fps 下它反而低于 Nyquist。）

**每帧时钟探针的实测结论（36.5 s / 72463 帧全帧局，1983 fps）：音频链洗清嫌疑。**

- `AudioSrcMs` 稳态 **100.0 次/s、步长中位 10.0000 ms**、间隔 std 0.466 ms ⇒ 精确阶梯、无第二种量子；`InterpMs` 有 70641 个不同取值 ⇒ **插值把阶梯抹平了**。
- 时钟本身无走时差：`ΣΔinterp/ΣΔwall = 1.00023`。
- **位置判据（真正的答案）**：把音频报告值还原成连续位置再比，稳态 **std 0.408 ms**（整局 0.667），p99 / max 2.64 / 39.0 ms，>1 ms 仅 2.4%。**std 0.41 ms 比 10 ms 缓冲量子小一个数量级** ⇒ 不构成体感，仿真预测的「±1.4 ms 够不够」到此为**否**。
- 残差主频 109 Hz（+218 Hz 谐波），幅度只有 ~0.4 ms（≈600 px/s 下 0.25 px）。
- 那个 **+10.25 ms 常数**是**音频输出延迟**（`ChannelGetPosition` 报的是已拉进输出缓冲的位置），会被玩家音频偏移吸收，**不是抖动** —— 探针用 500 ms EMA 在线估掉它再判阈值（否则 100% 的帧都「超标」）。
- 歌曲结束最后 **0.9 s** 时钟停走（1822 帧）会把「平均速率」拉到 0.975 ⇒ 探针把「停走」单列（`clockStopped`），不再混进统计。

⚠ **上面这条「音频链干净」有前提**：只在**音频源拉取规整**时成立。同机同配置的下一局源漏拉了 145 次，`interpErr` 立刻从 std 0.406 / >1 ms 0.52% 涨到 **1.084 / 14.56%** ⇒ **判 `interpErr` 必须同时看 `pullMiss`**（§2.4.12）。

#### 2.4.11 present 侧量具：量「相邻两次 present 的间隔」与「上屏内容的年龄」（2026-09-25）

update 侧四条链全部洗清之后，唯一没量过的就是**呈现** —— update 线程 1983 fps 平稳**并不**代表屏幕每 1/刷新率 秒收到一份均匀的内容。

**为什么不能在 draw 线程打点**：本 fork 里 `osu.Framework.Game` 是 `Container` 而**不是** `GameHost`（`GameHost.DrawFrame/UpdateFrame` 虽为 `protected virtual`，但不在 `Game` 的继承链上），`OsuGameBase` override 会 CS0115。⇒ 只能在 **update 侧读 `DrawThread.Clock`**（`ThrottledFrameClock` 由 draw 线程每帧 `ProcessFrame`，属性任何线程可读；`FPSCounter` 有先例）。

| 摘要字段 | 它回答什么 |
|---|---|
| `drawPeriod` | **相邻两次 present 的间隔分布**。update 侧帧长恒 0.5 ms 且不抖，draw 线程单独卡一下 update 探针看不见 |
| `presentAge` | **上屏那一刻屏幕上那份内容有多旧**。均值会被输入延迟吸收，**抖动才是眼睛看到的顿挫** |
| `clocks` | 两线程 fps / jitter / slept / maxHz / throttling + 窗口态 + 刷新率 |

**三条实现口径（第一局实测把这三条都证伪过，已改）**：

1. **原点差不能一次性标定。** `StopwatchClock.CurrentTime` 只在 draw 帧边界刷新、是**阶梯**的，`wall − CurrentTime` 带一个幅度 = 一个 draw 周期的**锯齿**；一次性标定锁在随机相位上会让 `presentAge` 出现物理上不可能的负值（实测 **min = −6.41 ms**）。⇒ 取**运行最小值**。
2. **逐 update 帧采样 ≠ 逐 draw 帧采样。** update（2034 fps）比 draw（1210 fps）快，同一 draw 帧被读多次；两帧长度负相关时限帧器追赶机制让长帧被少采 ⇒ 均值与分位一起偏低（实测 `drawPeriod` mean 0.622 ms 而 draw fps = 1210 ⇒ 0.826 ms，且按 p99 = 1.59 摆不出 0.826 的均值）。⇒ 改为**按 `drawClock.CurrentTime` 变化去重**，并给出自检 `coverage = ΣdrawPeriod / 采样首末壁钟跨度`。
3. **`presentAge` 的分辨率是一个 draw 周期，不是一个 update 帧长。** 零点只能取「上一次 update 帧边界」，而 draw 手上的 buffer 可能更早 ⇒ 配对不确定度 ≈ ±1 draw 周期（0.83 ms 量级）。**这与我们追的效应（0.4 ms）同阶** ⇒ `presentAge` 只能判约 1 ms 以上的滞后。失焦帧按 `IsActive` 剔掉（`skipped`）。

**判读口径**：先验四条自检（① `dup=` 有值；② `coverage` ≈ 1；③ `drawPeriod.mean ≈ 1000 / clocks 里的 draw fps`；④ `presentAge.min` 不为负），不通过就先修量具。②③ **只能在同一局内、同一工况下比**（退场段采样者自己掉速，见 §2.4.16）。再看 `drawPeriod` 的**尾部**（`over1 / over2 / over5` 与 `max`）—— 一次 8 ms 的 draw 帧就是一次可见跳帧，而 update 侧探针对它完全无感。`clocks` 里 `refresh / fps` 不是整数 ⇒ 每刷新显示的帧数在 1/2 间交替，是结构性 judder（2000 fps / 240 Hz 下约 0.2–0.25 ms，小到不构成体感）。

**不覆盖的部分（写下来免得下次又去这里找）**：`DrawFrame` 之后的 **DWM 组合 / 显示器扫描输出**在进程内量不到，与 §2.4.7 七里驱动内的 `Present` 成本**是同一条 swap chain 的两端** —— 那一段在进程内、可用 profiler 量清（0.25 ms/帧），这一段在进程外，**两者不要混为一谈**。若 `present` 行也干净，剩下的解释只有 tearing / 组合器节拍 / 主观锚定 ⇒ 判据是实机 A/B（开 vsync / 钉刷新整数倍）。

#### 2.4.12 音频源会「漏拉」：`interpErr` 有一个必须同时看的前置量（2026-09-25）

同机、**同一份音频配置**（两局日志逐字段相同）、相隔 40 分钟的两局全帧采集：

| 量 | A 局 `080438` | B 局 `084436` |
|---|---|---|
| 源停走 >15 ms 再一次性补上（`pullMiss`） | **1** 次 | **145** 次（3.6/s，累计停走 2.8 s） |
| 漏拉间隔 | — | 中位 0.17 s，**不对齐 100/200/250/500/1000 ms 任何网格** |
| `interpErr` std（稳态） | 0.406 ms | 1.084 ms |
| `interpErr` >1 ms | 0.52 % | 14.56 % |
| 同上，**剔除每次漏拉的 ±20 ms 邻域** | — | **0.455 ms / 5.5 %** |

**机制**：漏拉一拍 ⇒ 位置**停走 ~20 ms** 再一次性补上。阶梯仍在，只是偶尔少一级。**「补」是必然的且距离不丢** —— B 局全段推进 39826.1 ms / 壁钟 39824.5 ms（差 0.004%），所以那 2.8 s 是 146 次 hold 的**累计停走时间**，不是丢失的音频时长；该现象只造成**相位阶跃**，不造成走时差。

**为什么必须一起读**：插值器要在 ~40 ms 内把这 10 ms 缺口追掉，追赶期间的位置误差全计进 `interpErr`。B 局与 A 局的 2.6 倍差距**全部**来自这 145 次漏拉 —— 不一起看就会把「源在卡」误判成「插值不干净」。

⚠ **另一个坑：`audioStep top` 会把它藏掉。** 该分布只打 top-5，而 20 ms 级步进只有 146 次、低于第 5 名的 228 次 ⇒ **一次都不会显示**。故摘要里加了独立的 `pullMiss` 计数（停走 >15 ms 且 <60 ms；更长的停走是暂停 / seek）。

与 update 帧只弱相关（漏拉帧 `ElapsedMs` 均值 0.831 vs 全体 0.501；±5 帧内出现 >2 ms 帧的比例 4.1% vs 偶然水平 1.07%，4x 富集，不足以断言「update 卡导致音频漏拉」）。量级 std ~1 ms / p99 1.4 ms（600 px/s 下 0.6–4 px），且 145 次均匀分布在整局 ⇒ 若它起作用，表现是**全程低频抖动**而非「隔一会一次」。

#### 2.4.13 「漏一拍、补一拍」的机制定案：NAudio 读的是「当前全部空位」（2026-09-25）

> **本节内容是机制，不是可修复的根因。** 原先假设「渲染线程没登记 MMCSS 就是根因」已被 §2.4.16 **证伪**，原半句标题已删。**不要再从 MMCSS / 线程优先级方向重开这条链。**

**两条恒等式（稳态 t > 3 s）**：

- **`跳变次数 = 周期数 − 超额周期数`** 两局都精确成立（A 局 3492/3499 差 7 = 20+20+30+40 各自多吞的周期数；B 局 3761/3907 差 146 = 每次多吞 1 个周期）⇒ 每次事件都是「**漏掉一次唤醒，下一次一次读两拍**」，不是丢数据、也不是时钟走快。位置跳变只出现在 **10.0 / 20.0 / 30.0 / 40.0 ms** 档位，亚毫秒跳变只在开局 3 s 内（启动 / seek 期读的是解码位置，计任何稳态量前必须从 t = 3 s 起算）。
- **间隔 CV 0.92 + 20 ms 网格 ACF 全平 + 正常 hold 无系统正漂**（mean 10.004 / std 0.568）⇒ 不是「固定相位漂移」（那会给近似恒定间隔、CV ≪ 1），而是**随机抢不到调度**。

**为什么必然是「补一回」**（`src/NAudio.Wasapi/WasapiPlayer.cs`）：线程被引擎每 10 ms 一次事件唤醒，但读的是 `BufferSize − CurrentPadding`，即**当时所有空位** ⇒ 「一场没被唤醒」的后果必然是一次读两拍（960 帧）、位置一次 +20 ms。这也解释了为什么 hold 全落在 19.4–23.3 ms、`>30 ms` 一次都没有：`frameEvent` 是 **AutoReset**，信号不累积，超额读完后缓冲重新填满、下一次又对齐 ⇒ 不会一漏一串。

**可执行下一步（都要先确认再改）**：把推断换成直读 —— 在 `BassMixerWaveProvider.Read` 记（墙壁时间, 请求字节数）；NAudio 传进来的 `count` **就是** `numFramesAvailable × BlockAlign`，所以 `count ≈ 960 帧` 的存在直接证明「本次读吞了两拍」，还能读出 `BufferSize`（几拍）与两次读的**真实墙壁间隔**。→ **已实现为 §2.4.15 的 `wasapiRead` / `wasapiPull`。**

#### 2.4.14 MMCSS 交付陷阱（2026-09-25）——改 framework 源码不一定进游戏

**踩过的坑**：`osu` 侧默认从 **NuGet 包 `ez2lazer.Framework <版本>-ez2lazer`** 取框架（`Ez2Lazer.Dependencies.props` 里 `UseEz2LazerLocalFrameworkProject` **默认注释掉**），所以**改 `osu-framework` 源码不会自动进游戏**。证据：9:20 那次重建刷新了主程序与全部规则集，但 `osu.Desktop/bin/Release/net10.0/osu.Framework.dll` 仍是包的时间戳（9-21 14:52）；9:21 那局的 audio.log 里**没有**新加的 `mmcss=` 字段。

⇒ **要验证 framework 改动，必须**：打开 `UseEz2LazerLocalFrameworkProject`（props 注释里就是给这种情况用的；本机打开过但**不提交**），或走包发布。**每次声称「framework 改动生效」前，先核对输出目录里那个 DLL 的时间戳。**

**单局判据被证伪**：同一份音频路径（三次采集 framework 都没变）的 `pullMiss` 是 **1（080438）/ 145（084436）/ 4（092213）** ⇒ 「掉到 ≤5 次」这条判据在单局上不成立 —— **基线自己就在 1–145 之间跳**。要判就得在同局里拿到**分布**，而不是一个稀有事件计数。

#### 2.4.15 `wasapiRead` / `wasapiPull`：NAudio 拉取的间隔与请求拍数直读（2026-09-25）

- 新类 `osu-framework/osu.Framework/Audio/Wasapi/WasapiReadStats.cs`，在 `BassMixerWaveProvider.Read` 每次记录「距上次拉取的壁钟间隔」与「本次要了几拍」（`count / BlockAlign / (sampleRate/100)`），各进一个定长直方图（间隔 0.5 ms × 80 桶；拍数 0.25 拍 × 16 桶），另存精确 max。**默认关闭**，只有诊断打开的那一局由 `Player.cs` 打开、局末关闭 ⇒ 正常游戏在这条热路径上只多一次 bool 判断。
- 汇总新增 `wasapiRead reads=… gapsMs mean/p50/p90/p99/max over15=…` 与 `wasapiPull periods/pull mean/median/max over1.5=… totalPeriods=…`。
- **为什么它比 `pullMiss` 强**：`pullMiss` 是**稀有事件计数**（1/145/4 这种量级跨局不可比），这里给的是**分布** —— `periods/pull` 的 median 是不是 1.0、`over1.5` 占多少、间隔的 p99/max 多少，**单局**就能判「线程有没有按时醒」，也顺便读出 `BufferSize` 相当于几拍。

#### 2.4.16 MMCSS 确认加载但**没起作用** ⇒ 音频链结案；局内管线 pristine；退场段是另一个工况（2026-09-25）

全帧局 `framestall_20260925_101112.csv`（39.4 s / 72196 帧 / deep / 全程有按键）。

**一、这次确实是 MMCSS 的测试**：本地工程重建后输出 DLL 内含 `mmcss` / `WasapiReadStats` 标记，同局日志出现 `mmcss=Pro Audio (requested)`。

**二、直读量具把「漏一拍」从推断变成直读，而 MMCSS 没改变它**

| | A `080438` | B `084436` | C `092213` | **D `101112`（MMCSS）** |
|---|---|---|---|---|
| framework | 包 | 包 | 包 | **本地源码（MMCSS）** |
| `pullMiss` | 1 | 145 | 4 | **142**（3.6/s） |
| NAudio 请求 ≥1.5 拍 / 总拉取 | — | — | — | **162 / 3834（4.2%）** |
| `periods/pull` max | — | — | — | **2.00**（正好两拍） |
| 拉取间隔 p50 / p99 / max | — | — | — | 10.25 / 20.25 / 20.71 ms |

⇒ 渲染线程确实**每 0.24 s 一次、一次要两整拍**（不是读零头）。**MMCSS（`"Pro Audio"`）没有把漏拉拿掉** —— 按事先写死的判据，**音频链就此结案**，不再为它安排采集。剩下两种机制（线程晚醒 / `Read` 内部 BASS 解码慢）当前量具分不开，而它对体感的贡献量级只有「位置误差 std ~1 ms」、且应用侧无手段消除，继续投入没有产出。

**三、局内帧管线是 pristine 的**（39.4 s 按秒摊开）：局内 33 s 帧长均值 **0.490–0.558 ms**、`>5 ms` 帧除头两秒外**全 0**、子树 ≈0.10 ms、时钟停走帧 **0**。**全局 327 个 >5 ms 帧里 316 个落在歌结束后的 4.6 s 退场段**（子树已拆 = 0、update 掉到 120–192 Hz）；局内 33 s 只有 **11** 个 ⇒ 局内没有新问题。

**四、这条退场段顺手解释了两件之前读到过但没归因的事**：

- `present coverage = 0.869`：present 是**在 update 侧采样**的，退场段 update 只有 120–190 Hz 而 draw 仍千帧级 ⇒ 每 1 s 漏掉近千个 draw 帧。**不是量具坏了，是采样者自己掉速。**
- 四局 `noPress over5` 的诡异方差（7 / 10 / 17 / **326**）：各局在退场屏停留时长不同。**读 `over5` 前先减掉退场段。**

**五、present 侧改动**：`samplePresent` 现在**跳过时钟停走的帧**（新增 `frozenSkipped=`）。帧耗时直方图**不**动（保持与既有四局可比），判读时人工减去退场段。

#### 2.4.17 诊断开关改为**进程级**：关闭时热路径零开销（2026-09-25）

> ⚠ 本条里的「四个静态 bool」开关模型已被 §2.4.19 取代（总开关 + `EZ_DIAG_PROBES` 内容选择），下发点又被 §2.4.21 从启动时移回**进局前**。下面**原理与反面教材仍然成立**。

原先三个判定侧探针 + 时序追踪都在 `Player` 每次进图时从配置重读开关，`OrderedHitPolicyHelper` 还各持一个 `Bindable<bool>` 订阅 —— 对「永远关着」的功能是纯负担。改法只有一条原则：**关闭时热路径上一个字节都不做。**

- **开关只在进局前读一次**，真值落到探针的**静态 bool** 上；`Player` 与 `OrderedHitPolicyHelper` 不再读配置、不再持 bindable。
- **调用点自己先判**，让参数求值也省掉（`FrameStabilityContainer` 用局部 `sampling` 守卫 `RecordFrame` 调用 ⇒ 关闭时连调用都不发生）。
- **能被 JIT 兑现的部分就到「一次静态加载 + 一次分支」为止**：运行期开关无法再便宜（要真正 0 指令只能 `#if` / `[Conditional]`，那要求重新编译、不能做成设置项）。**别再从「把它做得更快」这里下手。**
- ⚠ **反面教材（已撤，不要重复）**：曾把开关做成 `Ez2ConfigManager` 的静态属性 + 环境变量 + 探针侧**链式只读属性** —— 热路径上一分没省、还多了一层概念。

#### 2.4.18 删掉 6 个「拿到也等于另一个参数」的列（2026-09-25）

判据只有一条：**同一行里能被另一个参数精确算出**（恒等，或差一个可复原的常数）。「只是还没人读」**不算**理由 —— 这类量以后可能用得上，留着。（逐列拿现有 8 局真数据复核：judgment 8×298–375 行；frame 8 局共 373,188 帧；press 8×354–478 行。）

| 列 | 等价关系 | 证据 |
|----|---------|------|
| judgment `InterpClock` | ≡ `GameTime` | 源码里是**同一属性 `Time.Current` 在同一次调用里连读两次**；逐行 `max|差| = 0.000000` |
| judgment `BassSource` | ≡ `GameTime − Drift − 15.000` | 残差全落在 `[14.999, 15.001]`；`Drift` 与 `SourceCurrentTime` 本就同源相减 |
| frame `SubtreeAllocBytes` | ≡ `LoopAllocBytes` | **逐帧全等，373,188 帧 0 例外**。成因是 `updateClock()` 从不分配，所以「整轮」与「子树内」必然相同 |
| frame `Gen2Delta` | ≡ 0 | 8 局（含 72k 帧全帧局）非 0 计数 = **0** |
| press `Gen2` | 增量 ≡ 0 | 8/8；列值本身是每局一个常数，全是开跑前累计的 gen2 ⇒ 列内无信息 |
| press `CatchingUp` | ≡ 0 | 8/8 全为 `0`；而更一般的 `FscIter > 1` 在 **6/8 局**出现（1/2/3/22/37…）⇒ 被完全覆盖 |

judgment `AudioLag` 序列（与 `Drift` 去趋势后恒 `r = −1.000`）随列一起从 `AnalyzePeriod.py` 删掉。

**顺带修掉一个静默 bug**：`AnalyzePressLatency.ps1` 一直读 `$_.FscIterations`，而 CSV 列名是 `FscIter` ⇒ PowerShell 取到 `$null`、`[int]$null = 0`，「多遍子树」分桶**从未命中过**。现已改正。

**明确不删的（看着像重复其实不是）**：press `PressesInFrame`（flush 时的值来自**完整内存环形缓冲**，比 CSV 反推更准）；frame `Gen1Delta` / press `Gen1` / press `FscIter`（**有信号**）；press `FrameElapsed` vs frame `ElapsedMs`（frame CSV 在阈值模式下只留慢帧，不能互为替代）；judgment `FrameElapsed` vs frame `ElapsedMs`（**不是同一个测量**：前者 `Clock.ElapsedFrameTime` 时钟增量，后者两次 `Stopwatch` 戳之差墙钟）；press `GcPauseMs` vs frame `GcPauseDeltaMs`（累计快照 vs 跨帧增量）；judgment `InputToJudgeMs`（终点在 `handleHit` 内部，与 press 两列终点不同）。

**副作用**：旧 CSV / 旧 summary 与新脚本**不兼容**（`AnalyzeFrameStall.ps1` 的 `frameSplit` 正则已按新格式收紧，旧 summary 那一节会静默不打印）。

#### 2.4.19 探针套件收口：总开关 + 内容选择，采集边界归位（2026-09-25）

> 本条**不涉及任何测量口径**（CSV 列、阈值、采样率全不变），动的是「谁拥有开关、谁算派生量」。旧数据仍可比。

**开关模型**：启动闸门仍是 ini 总开关 `EzJudgmentDiagEnabled`，但它**只决定整套诊断是否工作**；**跑哪些内容**由环境变量 `EZ_DIAG_PROBES` 选择（`judgment` / `press` / `frame` / `hotpath` / `all`；**空 = 全部**）。由 `EzDiagnosticSwitches.Enabled` 的赋值作为唯一入口下发（setter 内完成内容选择与调参下发）。**例外**：`EzTimingTrace` 走自己的设置项，不参与套件、也不进 `EZ_DIAG_PROBES`。

**为什么内容选择是环境变量而不是设置项**：子项回答的是「这次实验想验什么」，不是用户偏好。曾经把它做成五个持久化开关（连同 UI 复选框）**已撤掉** —— 那是把设置面板变实验台。⚠ 解析失败**直接抛异常**，因为静默降级会让一整局采集白跑，而这是本套件最贵的错误。

**顺带修掉的子项失效**：`ManiaJudgeHotPathTrace` 的清零与读数出口原先都关在 `#if DEBUG` 里，而埋点只在 `Enabled` 上分叉 ⇒ **Release 下 `hotpath` 子项打开后计数器只增不出**：跨局累计、永不输出，还白付 `Interlocked.Increment`（落在 `isHittable` / `CheckForResult` 这类最热路径上）。现改由子项闸门控制。

**采集边界归位（本条主要动机）**：纪律是 **调用点只声明「发生了什么」，派生量、单位换算、健全性上限、NaN 判定全部属于探针**。违反时症状是热的玩法代码里出现「先算好再喂进去」的中间量，改动探针要动玩法文件。

| 位置 | 旧 | 新 |
|---|---|---|
| 判定 `DrawableHitObject.UpdateResult` | 自算插值漂移 + 按键 wall 耗时 + 健全性上限再喂（~33 行） | `if (userTriggered && Enabled) EzJudgmentDiagnostics.Capture(this, timeOffset, keyTs);` |
| 按键 `Column.recordPressLatency` | 自算三段 + 单位换算 + NaN 判定（~45 行，在 `Column` 内） | `ManiaPressProbe.Capture(column, pressEnterTs, time, routed, judged, forceMissScan)`（放 Mania 侧，因需列号 / 车道计数） |
| 帧归因 `FrameStabilityContainer` | 对外暴露 5 个探针静态量 | 每轮末尾一次 `ReportLoop(...)`；5 个静态量撤出 FSC |
| 帧归因闸门 | `Sampling`（帧探针独有） | `FrameLoopAttribution = 帧探针 ‖ 按键探针`（按键探针要 `FscIter`） |
| 探针共享落盘 | 各探针各自算 wallclock / 目录 / 浮点格式 / 异步写 | `EzProbeOutput` |
| 局生命周期 | `Player` 里硬编码探针 clear/flush 名单 + 框架钩子 | `EzDiagnosticSession.Begin/End`，`Player` 不再出现探针名单 |

**唯一刻意留在调用点的是「非位置输入队列长度」**（`Column.reportInputQueueCountOnce`）：它读一次会触发一次全树重建，且该 API 是 `protected internal`（外部 helper 取不到树），既不是派生量也不能搬走 ⇒ 保持「每列首按键才读一次」。

**零开销自检（关闭时各调用点只剩什么）**：`DrawableHitObject.UpdateResult` 一次 `userTriggered && Enabled`；`Column.OnPressed` 一次局部 `probe` 判断；`FrameStabilityContainer.UpdateSubTree` 一次本地 bool；`EzSubFrameCorrection.RecordFscUpdate` 一次 `Enabled` 静态读取。**没有任何一处保留 bindable、DI 查询或运行期配置读取；也没有新增闭包或分配。**

**本条未覆盖的实验性诊断（仍各自为政，待决定）**：`InputAudioLatencyTracker`（运行期 bindable，产物是局末通知而非 CSV）、`EzStartupTrace` / `EzSongSelectEnterTrace`（注释写 Debug-only 但 5 个调用点都没有 `#if DEBUG`，Release 也做插值 + `Logger.Log`；只在启动路径，代价小）、`EzManiaAnalysisPerf` / `EzModConversionPerf`（第二套环境变量机制）、`EzSubFrameCorrection.Enabled`（是生产校正开关不是探针，但其静态量被探针读）。

#### 2.4.20 链式延迟汇总：两条链的串联分段与**累计总值**（2026-09-25）

**两条链的起终点**（必须钉死，否则数不可比）：

| 链 | 起点 | 终点 |
|---|---|---|
| A · 按键 → 判定 | `InputManager.EzSubFrameTimestamp`（输入事件被时间戳那一刻） | `Column.OnPressed` 返回（判定已 `ApplyResult` + 同步结果扇出完成） |
| B · 按键 → 驱动音频 | 同上（`RecordColumnPress` 就在 `OnPressed` 首行） | 输出路径 PCM 过阈值 |

**物理按键 → 子帧时间戳**这一段**两链都没有量具**（`EzSubFrameTimestamp` 已是校正后的戳），下面所有数都不含它。

**链路 A（仅真判定按键）**：`TotalMs` = `PreColumnMs + ColumnMs`，**CSV 里本来就有这一列**，此前一直只引用两段。

| 局（帧率） | `PreColumnMs` p50 | `ColumnMs` p50 | **`TotalMs` p50** | 累计 p90 / p99 | 独立探针 `InputToJudgeMs` p50 | 残差 |
|---|---|---|---|---|---|---|
| 233115（2381 fps） | 0.650 | 0.121 | **0.857** | 1.969 / 2.667 | 0.631 | 0.226 |
| 222233（1089 fps） | 0.981 | 0.116 | **1.247** | 2.366 / 3.291 | 0.935 | 0.312 |
| 080438（2320 fps） | 0.426 | 0.152 | **0.583** | 0.929 / 1.289 | 0.427 | 0.156 |
| 084436（2478 fps） | 0.572 | 0.107 | **0.798** | 1.598 / 3.438 | 0.565 | 0.233 |
| 092213（2410 fps） | 0.543 | 0.110 | **0.724** | 1.534 / 2.261 | 0.536 | 0.188 |
| 101112（2165 fps） | 0.525 | 0.124 | **0.736** | 1.829 / 3.349 | 0.532 | 0.204 |

1. **稳态累计 p50 = 0.58–0.80 ms**（1000 fps 配置局 1.25 ms）；两段中 `PreColumnMs` 占 **70–88%**。
2. **残差 0.16–0.31 ms 有机制解释，不是测量噪声**：`InputToJudgeMs` 终点在 `handleHit` 内、`TotalMs` 终点在 `OnPressed` 返回 ⇒ 差值 = 「判定已落地 → 同步结果扇出结束」。代入即闭合（080438：0.426+0.152−0.156 = 0.422 vs 0.427）。
3. **空按累计 p50 = 0.50–1.28 ms**，其中 `ColumnMs` p50 仅 **0.009–0.019 ms** ⇒ 等待部分与「有没有东西可判」无关，再次证 §2.4.2 的等帧结论。

**链路 B：键音 → 出声**

| 环节 | 已记录值 |
|---|---|
| B1 触发入队（`sampleTriggerSource.Play()`，真正播放在 `Schedule` 里） | 含在链路 A 的 `ColumnMs`（0.107–0.152 ms）内 |
| B2 真正 `Sample.Play`（下一帧 `Schedule` 排空 = `afterPressMean`） | 0.341–0.633 ms（含该帧其余工作，非纯键音） |
| B3 输出缓冲 / 引擎周期 | `wasapiRead.gapsMs` p50 **10.25 ms**（p99 20.25）/ 授予 **10 ms**（480 帧 @48 kHz，`lowLatency=true`） |
| **B4 端到端累计** | **本仓无记录值**：`EzLatencyStatistics.AvgInputToPlayback` 不产 CSV，需开 `InputAudioLatencyTracker` 跑一局 |

按键 → `Sample.Play` ≈ **1.0–1.6 ms**；再加输出缓冲 ≈ **11 ms 量级**，其中 10.25 ms 是**设备授予的恒定周期**，且被音频偏移在线吸收（`EzSubFrameCorrection` / 判定 `TimeOffset`），**不影响判定同步，只影响「听见自己键音」**。

**并联支路（同时发生，但不可加进上面任何一条链）**：`FrameAgeMs`（p50 0.92–1.00，探针文档已明确「不是延迟的组成部分」）；按键帧切分（`sincePrevFrameMean` / `pressColumnMean` / `afterPressMean` 和 = 按键帧长，量的是「按键落在的那一帧花在哪」）；`drawPeriod` / `presentAge`（draw/present 与 update 是两条独立链）。

**回顾性线索（不作结论）**：`20260924_211244`（34.1 s、56576 帧）是**唯一**一次「按键 → 判定」中位达 4 ms 的局 —— `TotalMs` p50 **4.936** 与 `InputToJudgeMs` p50 **3.683** 两个**独立**探针一致；但帧级探针显示原因不在列内：`withPress` 帧长 p50 **8.05 ms**（`noPress` 0.450、`over2 = 326/326`）而 `pressColumnMean` 只有 **0.881 ms** ⇒ 7 ms 停在「载有按键的那一帧被拉长」上。⚠ 该局处于埋点零点缺陷修复之前、`FscIter` 全 1 ⇒ **只作重测线索**，要复现必须重新采集。

#### 2.4.21 诊断开关下发点移回**进局前**：一口断定，局内不读配置（2026-09-26）

§2.4.17 把下发点放在启动时，§2.4.19 又把「跑哪些内容」并进来，代价是改设置必须重启。本条把唯一写入口搬到 `Player.load` 开头（**在 `DrawableRuleset` 创建之前**）：

```csharp
EzOsuGame.Diagnostics.EzDiagnosticSwitches.Enabled = ez2Config.Get<bool>(Ez2Setting.EzJudgmentDiagEnabled);
EzOsuGame.Diagnostics.EzTimingTrace.Enabled = ez2Config.Get<bool>(Ez2Setting.EzTimingTraceEnabled);
```

`EzDiagnosticSwitches.Enabled` 的 setter 内完成 `EZ_DIAG_PROBES` 内容选择与 `EZ_*` 调参下发，所以赋值这一处就是整局诊断范围的唯一决定点。

- **不变量**：一局里的「整套诊断用不用、跑哪些」在进局前**一口断定**；局内任何代码只读冻结后的静态 bool，不存在「运行到某处才发现现在能不能用」的分支，也没有第二处 `Get<bool>(Ez2Setting.EzJudgmentDiagEnabled)`。
- **热路径代价不变**：仍是静态 bool 分支，探针关闭时 JIT 仍可把整段采集消掉。
- **收益**：改设置**下一局生效**，无需重启；`EZ_DIAG_PROBES` 写错从「启动即失败」变成「进局即失败」，仍然响亮。
- **不损失采集覆盖**：`RecordFrame` / `samplePresent` / `ManiaJudgeHotPathTrace` 本来就只在局内被调用。

#### 2.4.22 脚本皮肤：启动期不再编译（2026-09-26）

**问题**：启动期把同一批 `.csx` 用 Roslyn 完整编译了**两次** —— 第一遍只为读一次 `CreateInfo()` 拿显示名，且编译结果既不写 `ScriptCompilationCache` 也不留用（读完即弃）；第二遍才真正编译并缓存实例。dotTrace 实测第一遍在 11.5–15.5 s 窗口占该窗口 running CPU 的 **42%**（`LoadScriptInfoAsync` 1778.7 ms，其中 `CSharpCompilation.GetDiagnostics` 1653.3 ms）。

**改动（**SCRIPTED-SKIN-LAZY**）**：目录扫描只按文件夹名生成条目（不编译）；脚本声明的显示名 / 作者、以及脚本实例都在**首次选中该皮肤时**才编译（走原有的 `LoadScriptAsync` + 编译缓存路径）。另加 `Ez2Setting.EnableScriptedSkins` 总开关（**默认关**）：关闭时完全不介入 —— 不扫目录、不装文件监视、不做任何编译。

**收益（A/B 同窗口对比）**：11.5–15.5 s 窗口 running CPU 4214.2 → **2524.1 ms（−40%）**，整棵 Roslyn 子树消失；三版快照的 UiFreeze/s 为 30.2 → 16.3 → **8.8**（见 §2.0）。

**代价与前提**：① 皮肤列表显示**目录名**而非脚本声明的显示名（声明名要等选中时才拿到）；② 开关属**启动期快照**（`SkinManager` 构造时读一次、刻意不做响应式绑定）⇒ 设置页改动**需重启生效**，且老用户升级后脚本皮肤默认不出现。

#### 2.4.23 字体族枚举：族名落盘缓存，启动不再逐个打开字体（2026-09-26）

**问题**：启动期 `InitialiseFonts → RegisterLocalizedFallbacks → FindByFamily → GetEntries` 是**全量**目录扫描 + 逐文件 `FT_New_Face` + 遍历 SFNT name 表。dotTrace 实测 **1.772 s**，其中 `freetype.dll` 自身 607.8 ms、`[未知]` 259.0 ms，其余是 `tryGetSfntFamilyName` 对每个字体 name 表的逐条 P/Invoke。整条链跑在 `GameHost.Run → bootstrapSceneGraph` 的主线程上、**主循环启动之前**，所以是纯串行启动时间（不是局内冻结）。而启动真正只需要「配置里那一两个族名落在哪个文件」。

两个前置事实，决定这条成本的归属：

- `EzFontSettingsOverlay` 早已用 `Task.Run` 加载目录（状态栏先显示 `…`），**不是**它的问题；
- 六个 UI 字体设置默认值全是 `string.Empty`，全空时 `FindByFamily` 根本不被调用 —— 这条成本**只落在配置过系统字体的用户**身上。

**改动（`EZ-FONT-NAME-CACHE`）**：新增 `EzSystemFontNameCache`，把「文件 → 族名」按 `(length, lastWriteTimeUtc)` 指纹落盘（`ez-font-name-cache.json`，带格式版本，写入走 `Storage.CreateFileSafely` 的临时文件 + 替换）。`GetEntries()` 对每个文件先查缓存：命中且指纹一致就**直接复用族名，不再打开字体**；`OsuGameBase` 在 `InitialiseFonts()` 之前 `EzSystemFontCatalog.AttachCache(Storage)` 打开开关，未挂载时行为与改动前完全一致。

- **不变量**：缓存**只对本次枚举到的文件**做查询。删掉的字体的条目永远查不到；被替换的字体指纹不匹配、自动重读。因此**不需要任何失效扫描**，也不存在「缓存说有、磁盘其实没有」的窗口 —— 这是选择「按文件指纹」而不是「按目录指纹」的原因。
- **预期收益**：首次启动仍是 1.772 s（同时写入缓存），之后每次启动只剩目录枚举 + 每文件一次 stat。设置面板首次打开同样受益。
- **代价 / 边界**：新增 `ez-font-name-cache.json`（数百条、几十 KB，放数据目录根部，与 `EzSkinSettings.ini` 同级）；指纹相同的替换（复制 + 保留时间戳）会复用旧族名。缓存缺失 / 损坏 / 版本不符 → 退化为全量扫描，不影响正确性。
- **验证**：`EzSystemFontNameCacheTest`（round-trip / 指纹不匹配 / 缺失与损坏 / 无改动不写盘）。端到端数字**待下一次 dotTrace 快照复核**。

#### 2.4.24 选歌 Panel 的 KPS 基线：memo 化读，去掉逐个建连接（2026-09-26）

**问题**：`PanelBeatmap.PrepareForUse()`（`osu.Game/Screens/Select/PanelBeatmap.cs:355`）每次面板被取用 —— 初次填充、每次滚动物化、来回滚动的重新绑定 —— 都调 `EzPanelKpsMetrics.TryResolveBaselineFromSqlite` 取 NoMod KPS 基线，落到 `EzAnalysisPersistentStore.TryGet`：**每次** `new SqliteConnection` + `Open` + `PRAGMA foreign_keys=ON`，再跑一条 `entry LEFT JOIN mania` 的查询，并把 `KPS_LIST_JSON` / `COLUMN_COUNTS_JSON` / `HOLD_NOTE_COUNTS_JSON` **三个 JSON 列解析一遍**。滚动即重复，因为面板池会为同一批谱面反复 rebind。

仓库里本来就有为此准备的 `EzAnalysisPersistentStore.ReadSession`（一条连接 + 逐谱面 memo，**未分析的负结果也 memo**，注释写明「大库上被反复命中的正是没分析的那些」），但它只被 `EzLocalProfileAggregator` 的批量聚合使用，面板路径没接。

**为什么不复用 `ReadSession` 而是新加一个**：`ReadSession` 自带一条长连接且**非线程安全**（注释即写「one session belongs to one worker」）。面板路径只是 update 线程，但 `EzAnalysisCache.GetAnalysisAsync` 的 stored fallback 会走到 `await … ConfigureAwait(false)` 之后，可能落在池线程上；而长连接还会挡住切分支时的 `File.Delete` + `ClearAllPools`。因此共享 memo **不持有连接**。

**改动（`EZ-PANEL-KPS-READ-MEMO`）**：

- memo 语义抽成 `tryReadMemo`（指纹校验 + 按 `writeGeneration` 整体丢弃）与 `resolveMemoEntry`（`pendingWrites` 覆盖 + 有效性闸门，**每次读都做、从不 memo**）两个方法，`ReadSession.TryGet` 与新路径共用，语义不变；
- 新增 `EzAnalysisPersistentStore.TryGetMemoised`：`ConcurrentDictionary` memo，只存 `tryGetRawData` 的**纯存储行**结果，未命中时才 `Initialise()` + 开连接；命中路径**不碰 SQLite**；
- `MemoEntry` 从 `ReadSession` 内嵌提升到类级；容量上限 `shared_read_memo_capacity = 2048`，整体丢弃而非 LRU —— 一次重读屏幕上那点内容，比维护一套没人依赖的淘汰策略便宜；
- `EzAnalysisDatabase.TryGetStoredSqliteSlice` 改走 `TryGetMemoised`，面板与 `EzAnalysisCache` 的 L1 读同时受益。

- **不变量**：命中与未命中**不可区分** —— 同样校验 hash / md5 / ruleset OnlineID，同样按落地写入换代整体失效，同样每次读都套 pending 覆盖与有效性闸门。`backfillStoredData` 仍用未 memo 的 `TryGet`（它要的就是当前存储态）。
- **代价 / 边界**：memo 持有 `EzAnalysisResult`（含 KPS 列表），上限 2048 条；切分支 / 换库仍靠 `writeGeneration` 换代清空。
- **验证**：`EzAnalysisPersistentStoreMemoTest`（负结果被后续 pending 写入覆盖 / 同一谱面新结果覆盖旧 memo）；`EzLocalProfile*` + `EzAnalysis*` 共 54 项通过。端到端滚动手感数字**待下一次 dotTrace 快照复核**。

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

补充观测：ASIO 的 **PCM 开关**（内部 / 外部 PCM）无明显差异 → 排除外部 PCM 分支；ASIO 暂停后恢复播放**声音正常**，1900 FPS 不是「没出声」造成的假象；首页等待 5 秒以上还能再涨几十 FPS（暖机 / 后台收尾）；无论是否在播放，进入选歌都会重新触发播放，因此「进选歌是否重启 Track」不是有效变量。

### 3.2 判读

传统共享输出在 **播放态** 才产生大额外开销、暂停立即恢复，指向「按音频线程频率反复执行的播放态查询」，而非解码或设备初始化。ASIO 走拉取式解码混音，同样 8000Hz 下代价低得多，因此只表现为首次暖机差值。结论落到 `BassAmplitudeProcessor`：电平与 FFT512 只服务可视化（`OsuLogo`、`LogoVisualisation`、`MasterGameplayClockContainer` 等消费 `CurrentAmplitudes`），却被绑在 8000Hz 的音频帧上。

### 3.3 已排除项（同一复现流程下验证无效）

关闭毛玻璃 UI（含保持关闭后冷启动）；切换到无 HUD 皮肤；菜单背景换成静态图片；ASIO PCM 开关；「代码皮系统性更慢」假设（Race 关闭后未能单独成立，`Masking` / `BufferedContainer` / `EdgeEffect` 消融开关见 `ManiaCodeSkinDrawAblation`）。

### 3.4 待复测

- 传统共享输出在振幅限频后，播放 / 暂停差值是否收敛到几十 FPS 量级。
- **IDE 启动特有的持续低帧**（900–1000）：直接启动无法复现，仍疑为 IDE 宿主开销（`Debug.Print`、附加调试器、进程优先级），**尚未定案**。
- 选歌返回后相对首页仍有 1800 → 1300–1400 的下降，与音频限频是否相关待分轨确认。
- 稳定态密集 FBO 峰值是否影响实际帧时，尚无结论。

> ⛔ **§3 早期那句「2–4 s 周期」前提已被 §2.4.9 证伪**（稳态帧长在 0.2–0.6 Hz 带内只有 0.026 ms 抖动），不要再按「找周期」的思路重开这一节。

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

每次只改一个变量。

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
| `AnalyzePressLatency.ps1` | 离线读 `diagnostics/presslatency_*.csv`：分 route / 空按、FrameAge 分桶、同帧批处理、GC |
| `AnalyzeFrameStall.ps1` | 离线读 `diagnostics/framestall_*.csv` + `.summary.txt`：全帧双直方图、GC 因果判据、慢帧归因、`frameSplit` 分段 |
| `AnalyzePeriod.py` | 离线读三探针 CSV：ACF + Welch 谱 + 带内主周期细扫 + 滑窗幅度/相位 + 跨序列滞后表（含偶然水平标定与派生对剔除）。**拒绝对尾部 frame 数据、离群帧主导方差的序列出结论**（§2.4.9） |

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
- 脚本皮肤安全模型：[`ScriptedSkin-Security.md`](./ScriptedSkin-Security.md)

---

## 9. 局内掉帧待排查（2026-09-20 登记）

| 现象 | 已知线索 | 下一步（先量） |
|------|---------|---------------|
| 进局后**首次命中** update 与 draw 同时明显掉帧，随后回升，画面无卡顿 | 单次性 ⇒ 首次执行成本：JIT、判定字形图集首次上传、首个 BASS 通道创建、判定动画首次 `GetAnimation` | 用 `ManiaJudgeHotPathTrace` 标出首次 `CheckForResult` → `ApplyResult` → 爆炸/判定的时间线；对照把 `KeySoundPreviewMode` 关掉再复测（音频侧变量隔离） |
| **LN 多的场景掉帧尤为明显** | **已定案（Triangles/Default）**：按住缩体时 `DefaultBodyPiece` 两层减法 FBO 每帧 `ForceRedraw`。tick 扫描抬 `ΔTickScan`，12 条 LN 下 `updMs` 仍约 1–2 ms，不是画侧主项。head/tail 入队已排除 | 生产：`LN-HOLD-FBO` + `LN-INPUT-SLOT`。消融场景仅 Debug。**Argon / Ez2 / Legacy 未改** |
| 其他显示器播放视频（即使暂停）时帧率掉 200+ | 游戏内代码路径无对应开销 ⇒ 指向桌面合成 / GPU 抢占 / DWM 或驱动侧 | 与 §5 流程同规 A/B；结论记入本文件而非改游戏代码。**与「送屏 / 扫描输出」同源 —— 进程内探针量不到** |

**输入侧已排除**：`DrawableHoldNoteBody` / `DrawableHoldNoteTick` 不是 `IKeyBindingHandler`，本就不进输入队列，LN 的 tick 数量不放大按键扫描成本。

---

## 附：框架侧 commit 索引

| commit | 内容 |
|---|---|
| `e22805587` | 振幅分析独立限频（`max(刷新率, 120Hz)`），音频控制仍 8000Hz |
| `56e9beb2c` | `ButtonEventManager<TButton>` 按下/抬起队列缓冲复用 |
| `c86e585e4` | `NAudioWasapiOutput` 登记 MMCSS `"Pro Audio"`（**已实测无效**，见 §2.4.16）+ `WasapiReadStats` |
| `524d84976` / `9e2b63366` | `GameThread.DEFAULT_ACTIVE_HZ` 1000 → 8000 |
| `b8d7002b42` | **SCRIPTED-SKIN-LAZY**：脚本皮肤启动期不编译 + 启用开关（默认关） |

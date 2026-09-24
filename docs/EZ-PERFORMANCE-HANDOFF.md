# Ez2Lazer 性能排查 · 会话交接（2026-09-24）

> 给**新会话**的交接单。细节见 `EZ-PERFORMANCE.md`（§2.4 系列是本次实测记录）。
> 目标：① 尽量提帧 ② 解决「下落不顺滑」 ③ 解释「判定全程规律的正弦波动」。

## 0. 一句话现状

帧率已到操作点（1000 fps，限帧器接管）；**mania 子树每帧恒定 0.105–0.116 ms，跨倍帧率不变** ⇒ 帧数上限不是游戏逻辑。
但**用户真正抱怨的两个现象（周期性不流畅、判定正弦波动）在现有 31 秒探针数据里都复现不出来**。
⇒ 下一步不是继续优化，而是**先把这两个现象测下来**。

## 1. 已定案（不要再重开）

| 结论 | 依据 |
|---|---|
| 帧率操作点 = `FrameLimiterBase = 250` + 两线程 `Limit4x`（1000 fps） | 2000 fps 帧间隔规整度 p90/p50 = 1.89，1000 fps = 1.19；按键尾部 `PreColumnMs` p99 35.4 → 6.8 ms |
| 帧是**限帧 / Present 受限**，不是工作受限 | 1000 fps 下 `elapsedMean ≈ 1.001 ms`，`restMsMean = 0.884 ms` 绝大部分是节流 sleep |
| mania 子树每帧恒定 0.105–0.116 ms | `frameSplit subtreeMsMean`，跨倍帧率不变 |
| 分配 92–93% 在 FSC（ruleset）之外 | `subtreeAllocMean` 168 B / 270 B vs update 线程 2552 B / 3162 B per frame |
| Draw 线程最大单项是 `Present` | dotTrace：25% 墙钟、`DrawFrame` 的 78%，含 AMD 驱动内 0.25 ms/帧 |
| **acrylic 已排除** | `EzBoxElement`（`AcrylicBackdropDrawable`）/ `Stage.stageBackdropBlur`（`BackdropBlurDrawable`）开关对 draw 帧数无可见差别。注意限帧下该量具**不敏感**，此结论不能推广成「每帧分配不重要」 |
| lane controller 索引维护不改 | 10 列 × 100 KPS × 2000 帧实测 ≈10–16 µs/帧 |
| 输入队列整树重建不再重开 | 已被 `TestSceneInputQueueChange.CombinedClicks` 证伪 |

## 2. 未决

### 2.1 用户观察 vs 探针数据不一致（本次最大的坑）

- 用户：不流畅**隔一会一次，约 2 s / 4 s**；判定**全程规律正弦波动**。
- 数据（两局各 ~31 s，`diagnostics/judgment_*.csv`、`framestall_*.csv`）：
  - `Drift` 自相关主峰 3.10 s (0.135) / 9.47 s (0.164) —— **弱，且两组不一致**
  - 卡顿爆发点平均间隔 **0.57–0.63 s**，不是 2–4 s
- ⇒ **现象未被测下来**。可能：探针时长太短、现象只在特定配置/曲目出现、或用户看的是另一个量。
- 已知的可信信号：`BassSource − GameTime` 峰峰 **9.8–9.9 ms**（`Drift` 是同一信号反号）。是音频时钟抖动还是缓冲量化，未定。

### 2.2 >5 ms 卡顿的三类成因（`framestall_*.csv` 三列可分辨）

| 类 | 特征 | 出现 |
|---|---|---|
| 子树内 | `SubtreeMs` / `ElapsedMs` = 85–99%，GC 与分配 ≈0 | 2000 fps 4 帧（5.5–43 ms） |
| GC 主导 | `GcPauseDeltaMs` / `ElapsedMs` = 0.73–0.89 | 1000 fps 9 帧，**全在歌曲首 2 s** |
| 单帧分配突发 | `ThreadAllocDeltaBytes` 126 KB – **1.54 MB** | 两组各数帧 |

`SubtreeMs` 是**墙钟**，线程被 OS 抢占同样计入 ⇒ 「子树内」类可能是抢占，而非真算得慢。

### 2.3 时间分布

- **2–5 ms 类整局均匀**，占 stall 帧 **95–97%**（GC 约占 >2 ms 帧的一半）。
- **>5 ms 类 64–82% 集中在歌曲首 2 s**（首 2 s 只占全程 6%）。

## 3. 下一步（唯一动作：先测再改）

**长时全帧捕获一次**，让周期可测：

1. 打开 `EzExperimentalSettings` 的判定诊断开关（`Ez2Setting.EzJudgmentDiagEnabled`）——它**同时**开启 frame-stall / press-latency / judgment 三个探针。
2. 环境变量 `EZ_FRAME_PROBE_MS=0`（抓全集；默认 `1.5` 只留超阈值帧）。可选 `EZ_FRAME_PROBE_LIGHT=1` 降探针自身开销。
3. 跑 **2–3 分钟**同一张图；三个 CSV 会同时产出。
4. 产出后做周期分析（自相关 / 滑窗）：判定的 `Drift`、`TimeOffset`，帧的 `ElapsedMs` 全序列，确认是否存在 2–4 s 周期及其相位来源。
5. 若确认存在「子树内」类 >5 ms 帧，再加**「每帧 CPU 时间 vs 墙钟」**读数，区分真算得慢与线程被抢占。

## 4. 代码锚点

| 用途 | 位置 |
|---|---|
| 帧级 stall 探针 | `osu.Game/EzOsuGame/Diagnostics/EzFrameStallDiagnostics.cs` |
| 按键延迟分段探针 | `osu.Game/EzOsuGame/Diagnostics/EzPressLatencyDiagnostics.cs` |
| 判定诊断 | `osu.Game/EzOsuGame/Diagnostics/EzJudgmentDiagnostics.cs` |
| 探针接线 + 环境变量 | `osu.Game/Screens/Play/Player.cs`（约 344–368 行） |
| 子树 / 时钟 / 其余 归因 | `osu.Game/Rulesets/UI/FrameStabilityContainer.cs`（`UpdateSubTree`） |
| 分析脚本 | `AnalyzeFrameStall.ps1` |
| 活文档 | `docs/EZ-PERFORMANCE.md` §2.4 |

## 5. 环境要点

- 配置：`F:\MUG OSU\EZ2OSU-lazer\framework.ini`（`ExecutionMode = MultiThreaded`、`FrameSync = Limit4x`）、`EzSkinSettings.ini`（`FrameLimiterBase`、`ColumnBlur`、`TurboMode`）。判 `Stage` 毛玻璃是否开启：`ColumnBlur × 50 > 0.01` 且 `TurboMode = False`。
- 探针热路径不做 IO、不产字符串，落盘在局末；`RecordFrame()` 跑在输入派发**之前**。
- 改 Realm schema / 迁移后**不得**擅自启动客户端做验证（见 `.cursor/rules/realm-schema-development.mdc`）。

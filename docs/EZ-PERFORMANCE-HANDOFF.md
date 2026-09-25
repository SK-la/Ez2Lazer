# Ez2Lazer 性能排查 · 会话交接（2026-09-24）

> 给**新会话**的交接单。细节见 `EZ-PERFORMANCE.md`（§2.4 系列是本次实测记录）。
> 目标：① 尽量提帧 ② 解决「下落不顺滑」 ③ 解释「判定全程规律的正弦波动」。

## 0. 一句话现状

帧率已到操作点（1000 fps，限帧器接管）；**mania 子树每帧恒定 0.105–0.116 ms，跨倍帧率不变** ⇒ 帧数上限不是游戏逻辑。

**周期性调查已做过长时全帧捕获（2026-09-24，38.7 s / 76985 帧，`EZ_FRAME_PROBE_MS=0`），结论是否定的**：
稳态下**测不出任何 1.5–5 s 周期**，跨探针也没有越过偶然阈值的相位关系。更关键的是发现了一个**假阳性陷阱**：
整段「带内信号」原本就是开局加载帧的余振 —— 去掉首尾各 3 s 后 `ElapsedMs` 的带内 RMS 从 **3.294 ms 掉到 0.026 ms**。
⇒ 下一步不是继续优化，也不是继续在同一批探针上找周期，而是**换一个能观测「体感不流畅」的量**（见 §3.5）。

**同一次捕获还顺带定案了一件更基础的事**：驱动 note 位置的音频源时钟是**精确 10.000 ms 的阶梯**
（4 局判定 CSV 全部落在该网格上，最大残差 22 µs，见 §2.1）。三个探针都在 **~10 Hz（100 ms）** 采样，
所以这个 **100 Hz** 结构**在设计上就测不到** —— §3 找不到周期是**带了找错**，不是效应本身弱。

**2026-09-25 又抓了一局全帧（36.5 s / 72463 帧）专门看这条音频时钟链，结论是否定的 —— 见 §3.4b。**
一句话：**插值把 10 ms 阶梯抹得很干净**。`interp` 相对「去量化后的连续音频位置」在稳态只偏 **std 0.41 ms**
（整局 0.67 ms）—— 比 10 ms 缓冲量子小一个数量级，不足以成体感。
⚠ **同时踩到一个指标陷阱**：`Δinterp/Δframe`（逐帧速率）std 0.28、`|rate−1|>5%` 占 85%，看着像剧烈抖动，
其实是抹平阶梯的**必然机制**（位置才是判据）。该指标已从摘要撤掉，换成 `interpErr`（位置误差）。
⇒ 更新线程侧的四条链（帧时间 / FSC 子树 / 按键延迟 / 音频时钟）**现在都测不出问题**，体感来源要往**更新线程之外**找（present / 帧节拍），或回到主观锚定（§3.5 第 3 项）。

**2026-09-25 的 present 局（40.5 s / 81006 帧，`framestall_20260925_084436`）给出两条结论 —— 见 §3.4c。**

1. **present 量具本身有两处采样缺陷（已修，见 §3.5 第 4 项）**，这一局的 `present` 行**不能**用来判 present：
   逐 update 帧采样而非逐 draw 帧采样（update 2034 fps > draw 1210 fps ⇒ 同一 draw 帧被采 ~1.7 次，
   而一个值只在**下一帧**期间可见，采样次数 ∝ 下一帧长度 ⇒ 长帧被少采，均值被拉到 0.622 ms 而 draw 自报 0.826）；
   以及原点差一次性标定锁在随机锯齿相位上（表现为 `presentAge` 出现不可能的负值 min = −6.41 ms）。
2. **§3.4b 的「音频时钟链干净」是**有条件的**。** 这一局 `BassSource` 停走一整个 10 ms 周期再补上的次数是
   **145 次（3.6/s，累计停走 2.8 s）**，上一局只有 **1 次**；而 `interpErr` 就从 **std 0.41 ms / >1 ms 0.52%**
   涨到 **std 1.08 ms / >1 ms 14.6%**。把每次漏拉的 ±20 ms 邻域剔掉，std 回到 **0.455 ms**（≈上一局水平）
   ⇒ 那 2.6 倍**全部**来自这 145 次源停走，不是插值变差。**判 `interpErr` 必须同时看 `pullMiss`。**

**2026-09-25 已把「漏拉」的机制定案（§3.4d）**：稳态里位置跳变只有 10 / 20 ms 两档，
且「跳变次数 = 周期数 − 超额周期数」两局精确成立 ⇒ 每次都是「**漏掉一次唤醒 → NAudio 一次读两拍**」，
**距离守恒**（0.004%）、只有**相位**阶跃，不是丢数据。根因：NAudio 只在 `.WithMmcssThreadPriority()`
时才登记 MMCSS，本 fork 的构造链没调 ⇒ 音频渲染线程与 2000 Hz 的 update / draw 同为 Normal，
而 Windows 默认时间片（15.6 ms 量级）比 10 ms 周期还长。

**⚠ 2026-09-25 09:22 那一局不算 MMCSS 的测试**（细节见 §3.4e）：MMCSS 只进了 framework **源码**，
而 `osu` 默认从 **NuGet 包**取框架 ⇒ 那一局跑的还是旧 DLL（证据：输出目录 `osu.Framework.dll` 是包的时间戳
2026-9-21，且音频日志没有新加的 `mmcss=` 字段）。同时同一份音频路径三次采集的 `pullMiss` 是
**1 / 145 / 4** ⇒ **单局计数不可判**，改为看 §3.4e 的分布量具。

**2026-09-25 10:11 局（`framestall_20260925_101112`，39.4 s）是真正的 MMCSS 测试 —— 结论：MMCSS 没用，音频链结案（§3.4f）。**

- 交付闭环：本地工程重建（9:36:30）后的 DLL 才带上改动，音频日志出现 `mmcss=Pro Audio (requested)`。
- 直读量具把「漏一拍」从推断变直读：`wasapiRead … p50=10.25 p99=20.25 over15=162`、
  `wasapiPull periods/pull max=2.00 over1.5=162 / 3834 (4.2%)` ⇒ 每 0.24 s 一次、一次要**两整拍**。
- **`pullMiss=142`（3.6/s）—— 与没带 MMCSS 的 B 局（145）一样。按事先写死的判据：音频链就此结案**，不再安排采集。
- **局内帧管线 pristine**：逐秒看前 33 s 是 1794–2040 帧/s、帧长 0.490–0.558 ms、>5 ms 帧共 11 个、子树 ≈0.10 ms、时钟零停走。
- 新登记一个**工况陷阱**：歌曲结束后的 4.6 s 退场段里 update 掉到 120–192 Hz、子树 = 0，
  该局 327 个 >5 ms 帧有 316 个在这里。它同时解释了过去四局 `noPress over5` 的诡异方差（7/10/17/326）
  与 `present coverage=0.869`。**present 采样现在跳过时钟停走的帧**（`frozenSkipped=`）；帧直方图不动，判读时人工减去。
⇒ 局内更新线程、音频、present 三条侧链**都没有可归因的问题**了。剩下唯一在进程内**量不到**的环节是
**送屏 / 扫描输出**（Borderless + 未开 vsync ⇒ draw 以 ~1126 fps 撕裂送屏，每次刷新跨多次 swap），见 §3.5 第 6 项。

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
| `interpErr` 必须和 `pullMiss` 一起读 | 同机两次采集 `pullMiss` 1 → 145 时 `interpErr` std 0.41 → 1.08 ms；剔掉漏拉邻域后回到 0.455（§3.4c） |
| 漏拉机制 = 漏一次唤醒 → 一次读两拍（距离守恒，只有相位阶跃） | 稳态位置跳变只有 10/20 ms 两档；「跳变次数 = 周期数 − 超额周期数」两局精确成立；NAudio 读 `BufferSize − CurrentPadding`（§3.4d） |
| **音频源漏拉不可由应用侧消除 ⇒ 音频链结案** | 登记 MMCSS（`"Pro Audio"`）后 `pullMiss` 142（3.6/s）、`periods/pull max=2.00`，与未登记时（145）相同（§3.4f） |
| **歌曲结束后的退场段是另一个工况** | update 掉到 120–192 Hz、子树 = 0；该局 316/327 个 >5 ms 帧落在此（§3.4f） |
| 输入队列整树重建不再重开 | 已被 `TestSceneInputQueueChange.CombinedClicks` 证伪 |

## 2. 未决

### 2.1 用户观察 vs 探针数据不一致（本次最大的坑）

- 用户：不流畅**隔一会一次，约 2 s / 4 s**；判定**全程规律正弦波动**。
- 数据（两局各 ~31 s，`diagnostics/judgment_*.csv`、`framestall_*.csv`）：
  - `Drift` 自相关主峰 3.10 s (0.135) / 9.47 s (0.164) —— **弱，且两组不一致**
  - 卡顿爆发点平均间隔 **0.57–0.63 s**，不是 2–4 s
- ⇒ **现象未被测下来**。可能：探针时长太短、现象只在特定配置/曲目出现、或用户看的是另一个量。
- ⚠ 上面那两个 ACF 数字是**当时临时算的，仓库里没有对应脚本**；18xxxx 那两局 CSV 也已不在 `diagnostics/`。
  2026-09-24 补齐工具（`AnalyzePeriod.py`，见 §3.4）后，在现有三局（各 ~34 s）上**复现不出**这两个周期
  （`Drift` 全部 `ACF r ≤ 0.20`）。不据此推翻结论，只登记「引用值不可复算」。
- **2026-09-24 晚的 38.7 s 全帧局仍未测出周期**（详见 §3.4 结果），且揭示了这批探针上两个会让「周期」凭空出现的陷阱：
  开局加载帧主导方差、以及带通后独立样本数太少导致的高偶然相关。**在下结论前必须先看这两个读数是否合格。**
- 已知的可信信号：`BassSource − GameTime` 峰峰 **9.8–9.9 ms**（`Drift` 是同一信号反号；实测两者恰差常数 15.000 ms）。
  **2026-09-24 晚定案：这是音频缓冲量化，不是随机抖动。**
  - 4 局判定 CSV 里 `BassSource` 的 331–353 个取值 **100% 落在 10.000 ms 网格上**，最大残差 **22 µs**；
    同一批 `GameTime` 完全无此结构（各 bin 均匀）⇒ 只有音频源被量化。
  - 机制：实机走 `NAudioWasapiOutput`（BASS 解码 mixer + NAudio WASAPI 拉流），`DEFAULT_NAUDIO_LATENCY_MS = 10`；
    `TrackBass.CurrentTime` 取 `BassMix.ChannelGetPosition`，而解码 mixer 的位置**只在 NAudio 拉走一个 buffer 时前进**
    ⇒ 每次正好 10.000 ms。
  - 于是 `BassSource` 是 100 Hz 阶梯，`GameTime`（== `InterpClock`，插值时钟）连续，两者之差就是 `Drift`。
  - **实机日志（`logs/1790263779.audio.log`，与全帧局同一次会话）**：
    `NAudio default output started: ... wasapi="VoiceMeeter Aux Input (VB-Audio VoiceMeeter AUX VAIO)",
    48000Hz/2ch float, requestedLatency=10ms, actualLatency=10ms, lowLatency=true`
    - **设备是 VoiceMeeter 虚拟声卡**，不是物理 DAC。48000 Hz × 10 ms = **480 帧**，与实测 10.000 ms 量子精确吻合。
    - `actualLatency` 不是回显：NAudio 文档明确它是「设备**实际授予**的引擎周期」（低延迟模式下由设备给的 period 推导），
      而 `lowLatency=true` 表示 IAudioClient3 低延迟共享模式**确实生效** —— 也就是说**即使低延迟模式开着，
      这台虚拟设备也只给 10 ms**（物理 DAC 走 IAudioClient3 低延迟通常给 ~2.67–3 ms）。
      ⇒ §3.5「缩小缓冲」这条路在这台设备上**大概率谈不下来**，要更小周期得换设备（物理 DAC / 独占 / ASIO；
      日志里同时有 `Found 7 ASIO devices` 与 `Freeing ASIO device`，说明 ASIO 路径也在用）。
  - **三个探针都按 ~10 Hz 采样 ⇒ 100 Hz 结构被混叠掉。** 这是 §2.1 开头「现象未被测下来」的结构性原因。
  - **2026-09-25 每帧探针实测（见 §3.4b）：阶梯在，但被插值抹平了** —— `interp` 相对连续音频位置只偏 std 0.41 ms。
    所以这一条到此**不再是待查项**。⚠ 判它不能看逐帧速率（std 0.28 是抹平的机制，不是抖动），要看位置。

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
2. 抓**全集**：环境变量 `EZ_FRAME_PROBE_MS=0`（默认 `1.5` 只留超阈值帧）。若不想动环境变量，
   临时把 `EzFrameStallDiagnostics.ThresholdMs` 的默认值改为 `0` 等价（本地改、**不提交**，见 §3 末尾）。
   可选 `EZ_FRAME_PROBE_LIGHT=1` 降探针自身开销。
    - **抓全集是硬要求，不是优化**：阈值 > 0 时 `framestall_*.csv` 只有慢帧，把这种尾部数据插值到均匀网格会伪造
      极强虚相关（实测给出过 `r = −0.874`）。`AnalyzePeriod.py` 会读同名 `.summary.txt` 的 `threshold=`
      识别并拒绝这类序列，于是这一局就白跑了。
3. 跑 **2–3 分钟**同一张图；三个 CSV 会同时产出。
4. 产出后跑 **`python AnalyzePeriod.py`**（在 `diagnostics/` 目录下执行，默认读最新的那一局；`--plot` 出图、
   `--windows` 看滑窗明细，也可直接传路径 / `--judgment|--frame|--press` 指定）。
   它按 ① 判定的 `Drift` / `TimeOffset`、② 帧的 `ElapsedMs`、
   ③ 按键的 `PreColumnMs` / `FrameAgeMs`、④ 帧的 `AudioStep` / `InterpRate`（音频源逐帧步进 / 插值时钟逐帧速率，
   2026-09-24 新增列）做 ACF + Welch 谱 + 带内主周期细扫 + 滑窗幅度/相位，
   并因同局三份 CSV 的 `WallMs` 同源（`EzJudgmentDiagnostics.WallClockMs`）而给出**跨探针滞后表** ——
   这是回答「相位来源（音频时钟 / 帧节奏 / 输入投递）」的关键。判读口径：
    - 先看 `ACF r` 与「带内主周期占带内能量 / 均匀背景」两个读数是否同时明显（单看 PSD「峰 / 中位」会被 1/f² 噪声骗成几百倍）。
    - **先确认两个前置读数合格**，否则后面的数全是假的：① 每序列的 `RMS/稳健σ` 要 < 5x（>5x = 方差由开局加载帧独占）；
      ② 相位表要打印出「偶然水平基准」，只有**越过家族性阈值**的行才算数。
    - 跨探针滞后表**只有「内部峰」那几行**能给出先后关系。「同时」= 两者锁相但先后在一个采样间隔内不可分
      （事件序列 ~93 ms），只读 r；「贴边」= 峰被 ±最短周期 截断，滞后数值不可信，也只读 r。
      这两类占多数是正常的，不要从中提取「谁领先多少毫秒」。
    - 尾部 frame 序列（`⚠`）完全不能用于周期/相位，先保证第 2 步做对。
    - 想分析「稳态」而不是「含加载/退出的整局」时加 `--trim 3,3`（去掉首尾各 3 s）。**找周期基本都应该加。**
    - **要看音频时钟量化（§2.1 / §3.4b）就换带**：`--band 0.004,0.02 --dt 0.002` 看 100 Hz 附近。
      ⚠ **`InterpRate` 已证明不能用来判「顺不顺滑」**（§3.4b）：它在 0.010 s 上确实有强 `ACF r`，但那只是逐帧速率的摆动，
      位置其实只偏 0.41 ms。要判位置请直接拿帧 CSV 的 `InterpMs` / `AudioSrcMs` 离线复算（口径见 §3.4b 表）。
      `AudioStep` 会**必然**被 `RMS/稳健σ` 护栏拒掉（稀疏脉冲列，本来就是「多数 0 + 少数 10 ms」），那是构造使然；
      它有用的形态在 `clockQuant` 摘要行里（`audioStep top`）。
      帧 CSV 的 `clockQuant` 摘要行现在给的是：`audioStep` 分布 + `interpErr`（位置误差 std/p99/maxdev/offset，**这个才是判据**，
      ⚠ 必须与同行的 `pullMiss` 一起读，见 §3.4c —— 源漏拉会把 std 从 0.41 抬到 1.08）+ `clockStopped`。
      另注意 `audioStep top` 只打 top-5，20 ms 级步进会被截断藏掉，只有 `pullMiss` 计数看得见它。
5. 若确认存在「子树内」类 >5 ms 帧，再加**「每帧 CPU 时间 vs 墙钟」**读数，区分真算得慢与线程被抢占。

> 抓全集用的 `ThresholdMs = 0` 属于**临时本地改动，不要提交**（环境变量是正式口子）。

### 3.4 结果（2026-09-24 晚，38.7 s / 76985 帧全帧局）

**结论：稳态下测不出 1.5–5 s 周期；跨探针也没有越过偶然阈值的相位关系。**

| 读数 | 全段 | `--trim 3,3` | 说明 |
|---|---|---|---|
| `ElapsedMs` 去趋势 RMS | 3.294 ms | **0.026 ms** | 差 129 倍 ⇒ 全段的「带内能量」就是开局加载帧 |
| `ElapsedMs` RMS/稳健σ | 15.7x | 2.6x | >5x 即方差被离群帧独占（首个 141 ms 帧在 t=0） |
| 各序列带内主周期 `ACF r` | — | ≤ 0.172 | 阈值 0.2 |
| 各序列带内占比 / 均匀背景 | — | ≤ 1.7x | 阈值 5x |
| 跨探针家族性 5% 阈值 | 0.597 | 0.597 | 31 对 × 独立带通噪声标定 |

- 修剪后**没有任何一对内部峰越过 0.597**；唯一越线过的是 `ElapsedMs × SpikeRate −0.746`，而
  `SpikeRate` 是 `elapsed ≥ 2×p50` 的指示函数 ⇒ **定义性相关，非发现**（已从表中剔除）。
- 唯一站得住的物理关系仍是 §2.4 的老结论：`PreColumnMs × FrameAgeMs` 同时（分辨率内）`r = +0.880`
  ⇒ 按键等待就是等下一帧，不是新问题。
- 稳态帧长抖动在该带内只有 **0.026 ms**（帧长均值 0.503 ms）⇒ **帧节奏侧没有可被感知的 1.5–5 s 波动**。

⚠ **这次差点被自己的工具骗**：未修剪的第一次运行给出 `ElapsedMs × GcPauseDeltaMs r = +0.998`、
`× ColumnMs +0.957` —— 帧时长不可能与按键列工时同相，回头查才发现是同一个 141 ms 首帧在两个序列里的余振。
现在工具用 `RMS/稳健σ` 和家族性阈值两条护栏把这类读数挡在表外。

### 3.4b 结果（2026-09-25：专测音频时钟链 → **洗清嫌疑**）

全帧局 `framestall_20260925_080438.csv`（36.5 s / 72463 帧 / 1983 fps，全程有按键）。

**已证实**：`AudioSrcMs` 稳态 **100.0 次/s、步长中位 10.0000 ms**、间隔 std 0.466 ms ⇒ 精确 10 ms 阶梯，无第二种量子；
`InterpMs` 在 72463 帧里有 **70641 个不同取值** ⇒ **插值时钟不是阶梯**，量化确实被抹平；排除歌曲结束后的停走，
`ΣΔinterp/ΣΔwall = 1.00023`，每 10 ms 窗内两者只差 0.006 ms ⇒ 无走时差。

**位置判据（把音频报告值 + 距上次跳变的时长还原成连续位置，再看 interp 离它多远）**：

| 量 | 稳态 t∈[5,30)s | 整局 |
|---|---|---|
| std | **0.408 ms** | 0.667 ms |
| p99 / max | — | 2.64 / 39.0 ms |
| >0.5 / >1 / >2 ms | — | 19.0 / 2.4 / 1.3 %（稳态 >1 ms 只有 0.55%） |
| 常数 offset | +10.25 ms | +10.31 ms |

- **std 0.41 ms ⇒ 插值时钟是一条平滑线**，仿真预测的「±1.4 ms 够不够成体感」到此为**否**。残差主频 109 Hz（+218 Hz），
  波纹确实在缓冲率上，但幅度只有 ~0.4 ms（≈600 px/s 下 0.25 px）。
- 那个 **+10.25 ms 常数**是**音频输出延迟**（`ChannelGetPosition` 报的是已拉进输出缓冲的位置，领先「听到的声音」约一个缓冲），
  被音频偏移吸收，不是抖动。探针用 500 ms EMA 在线估掉它再判阈值（否则 100% 的帧都「超标」）。

**⚠ 两个坑（都已写进探针/文档，勿重犯）**

1. **逐帧速率不能当抖动读**：`Δinterp/Δframe` p25 0.82 / p75 1.17 / std 0.28 / `|rate−1|>5%` 占 **85%**。
   这是抹平阶梯的必然机制，**位置**反而平滑。`InterpRate` 序列因此不可用于判「顺不顺滑」（改看 §2.4.11 的位置表）。
2. **歌曲结束最后 0.9 s 时钟停走**（1822 帧 / 917.5 ms），会把「平均速率」拉到 0.975；排除后是 1.00023。
   摘要现在把停走单列为 `clockStopped`。

**结论：更新线程侧的四条链（帧时间 / FSC 子树 / 按键延迟 / 音频时钟）全部测不出问题**，§3.5 第 2 项的三个修复方向不再需要动。
**⚠ 但这条「干净」有前提，2026-09-25 08:44 那一局把它测出了边界，见下。**

### 3.4c 结果（2026-09-25 08:44：present 局 → 量具要修；且音频链的「干净」有前提）

全帧局 `framestall_20260925_084436.csv`（40.5 s / 81006 帧 / 1998 fps，全程有按键）。

**一、`present` 行出来了，但这一局的数不可用（两处采样缺陷，已修）**

| 读数 | 值 | 问题 |
|---|---|---|
| `drawPeriod` | n=80988 mean 0.622 std 0.390 p50 0.510 p90 0.990 p99 1.59 p99.9 3.11 max 36.99 | 采样数 = update 帧数（1998/s）> draw 帧数（1210/s）⇒ **不是逐 draw 帧** |
| `presentAge` | mean 14.92 std 0.657 **min −6.412** p99 16.33 max 102.6 | 负值物理上不可能 ⇒ 一次性标定的原点差锁在了随机锯齿相位上 |

- 偏置方向可推：某个值在第 i 帧结束时写入、只保持到第 i+1 帧结束，被 update 采到的次数 ∝ **下一帧**长度。
  两帧长度负相关时（限帧器自带追赶）长帧被少采、短帧被多采 ⇒ 均值与分位一起偏低。
  实测 0.622 ms 对 `DrawThread.Clock.FramesPerSecond = 1210`（⇒ 0.826 ms）：按 p99 = 1.59 / max = 37，
  **摆不出 0.826 的均值**，两个数不可能同时成立。
- 修法（已落地）：① 按 `drawClock.CurrentTime` 变化去重（update 比 draw 快，每个 draw 帧至少被读一次）；
  ② 摘要加自检 `coverage = ΣdrawPeriod / 采样首末壁钟跨度`，≈1 才是「每帧恰好采一次」；
  ③ 原点差改取**运行最小值**；④ `presentAge` 的分辨率改按**一个 draw 周期**声明，不是「一个 update 帧长」。

**二、音频源这次在漏拉 —— 两次采集唯一的实质差别**

`audioStep top` 把 20 ms 级步进**截断藏掉了**（146 次 < 第 5 名的 228 次），所以只有 `pullMiss` 计数看得见它。

| 量 | A 局 `080438` | B 局 `084436` |
|---|---|---|
| 源停走 >15 ms 再补上 | **1** 次（0.03/s） | **145** 次（3.6/s，累计停走 2.8 s） |
| 漏拉间隔 | — | 中位 0.17 s，min 0.02 / max 1.23 s，**不对齐 100/200/250/500/1000 ms 任何网格** |
| `interpErr` std（稳态 t∈[5,30)） | 0.406 ms | 1.084 ms |
| `interpErr` >1 ms | 0.52 % | 14.56 % |
| 同上一项，**剔除每次漏拉 ±20 ms 邻域** | — | **0.455 ms / 5.5 %**（窗口放大到 ±200 ms 不再改善） |

- 机制：`BassMix.ChannelGetPosition` 只在 NAudio 渲染线程拉走一个 buffer 时前进。漏拉一拍 ⇒ 位置停走 ~20 ms
  （两次跳变间隔 20 ms 而不是 10 ms）再一次性补上，**而且距离不丢**（全段推进 39826.1 ms / 壁钟 39824.5 ms，
  差 1.7 ms = 0.004%）：2.8 s 是这 146 次 hold 的累计停走时间，全部被双倍读补回 ⇒ 只造成**相位阶跃**，不是走时差。
  **2026-09-25 已定案机制与「为什么必然补一回」，见 §3.4d。**
- 与 update 帧只**弱**相关：漏拉那一帧 `ElapsedMs` 均值 0.831 ms（全体 0.501），±5 帧内出现 >2 ms 帧的比例 4.1%
  （该窗口偶然水平 1.07%）⇒ 4x 富集，不足以断言「update 卡导致音频漏拉」。
- **两次采集音频配置逐字段相同**（`logs/1790294601.audio.log` vs `1790297002.audio.log`：同端点、48000 Hz/2ch、
  requested=actual=10 ms、lowLatency=true）⇒ 差别来自运行期，不是配置。

**结论**：§3.4b / §2.4.11 的「插值时钟干净」**只在音频源拉取规整时成立**；源一漏拉，位置误差立刻涨到
std ~1 ms / p99 1.4 ms / maxdev 7 ms，14.6% 的帧偏 >1 ms。够不够成体感还没判，但它符合「一顺一不顺」这一现象。

### 3.4d 「漏一拍、补一拍」的机制定案 + MMCSS 缺口（2026-09-25）

细节与源码引用在 `EZ-PERFORMANCE.md` §2.4.14，这里只留结论。

**稳态（t>3 s）里位置跳变只有两档，且「读次数」守恒**（开局 3 s 那批亚毫秒跳变是启动/seek 的解码位置，必须排除）：

| 稳态 t>3 s | A 局 `080438` | B 局 `084436` |
|---|---|---|
| 跳变档位 | 10 / 20 / 30 / 40 ms | **只有 10 / 20 ms** |
| 跳变次数 / 周期数 | 3492 / 3499（差 7 = 1+1+2+3） | 3761 / 3907（差 **146** = 每次多吞 1 拍） |
| 推进 / 壁钟 | 35453.7 / 35447.1 ms | 39826.1 / 39824.5 ms |
| 漏拉间隔 | —（n=4） | mean 264 ms、**CV 0.92**（泊松，20 ms 网格 ACF 全平） |

⇒ `跳变次数 = 周期数 − 超额周期数` 两局都精确成立：每次事件都是「**漏掉一次唤醒 → 下一次一次读两拍**」，
不是丢数据、不是走快（全段距离守恒到 0.004%，只有**相位**被重新分配）。
CV≈1 + 正常 hold `mean 10.004 / std 0.568`（无系统正漂）⇒ 是**随机抢不到调度**，不是固定相位漂移。

**为什么必然「补一回」**（NAudio `src/NAudio.Wasapi/WasapiPlayer.cs` 的 `PlayThread`，源码已核）：它读的是
`BufferSize − CurrentPadding`，也就是**当时所有空位**，不是固定一拍 ⇒ 漏一次唤醒必然读双倍（960 帧）。
`frameEvent` 是 **AutoReset**、信号不累积 ⇒ 超额读完之后重新对齐，**不会一漏一串**（实测 hold 全在
19.4–23.3 ms，`>30 ms` 一次都没有）。

**为什么一直在漏**：`WasapiPlayerBuilder.mmcssTaskName` 默认 `null`，只有显式 `.WithMmcssThreadPriority(...)`
才登记 MMCSS；本 fork 的构造链（`osu-framework/osu.Framework/Audio/Wasapi/NAudioWasapiOutput.cs:112`）没调它，
框架侧也从不设线程优先级 ⇒ 音频渲染线程与 2000 Hz 的 update / draw 线程**同为 Normal**，
而 Windows 默认时间片（15.6 ms 量级）比 10 ms 周期还长。

### 3.4e MMCSS 交付陷阱 + 单局判据被证伪 + 直读量具（2026-09-25 09:30）

**一、交付陷阱：改 `osu-framework` 源码不会自动进游戏**

`osu` 默认从 **NuGet 包 `ez2lazer.Framework 2026.921.0-ez2lazer`** 取框架
（`Ez2Lazer.Dependencies.props` 的 `UseEz2LazerLocalFrameworkProject` **默认被注释掉**）。所以 MMCSS 虽然已提交进
framework 源码（`c86e585e4`），但 09:22 那一局跑的是**旧 DLL**。两条证据：

- `osu.Desktop/bin/Release/net10.0/osu.Framework.dll` 时间戳 = **2026-9-21 14:52**（包），而同一目录里主程序与
  全部规则集是 9:20（当次重建）；
- `logs/1790299269.audio.log` 里**没有**新加的 `mmcss=` 字段。

⇒ 要让 framework 改动进游戏：打开 `UseEz2LazerLocalFrameworkProject`（props 注释里就是给这种情况用的，
本次已在本机打开、**不提交**），或走包发布。

**二、单局计数不能当判据**

同一份音频路径、framework 都没改的三次采集：`pullMiss` = **1（080438）/ 145（084436）/ 4（092213）**。
⇒ 原先写的「降到 ≤5 次即成立」在单局上无意义。

**三、新量具：`wasapiRead` / `wasapiPull`（默认关闭，诊断局才开）**

`osu-framework/osu.Framework/Audio/Wasapi/WasapiReadStats.cs` 在 `BassMixerWaveProvider.Read` 直读
「距上次拉取的壁钟间隔」与「本次要了几拍」，各进定长直方图；`Player.cs` 在诊断局的起止处开关。
汇总里输出 `reads/s`、间隔 mean/p50/p90/p99/max/`over15`、`periods/pull` mean/median/max/`over1.5`。
⇒ **单局**即可判「渲染线程有没有按时醒」，并顺带读出缓冲相当于几拍（`pullMiss` 那种稀有事件计数做不到）。

### 3.4f 结果（2026-09-25 10:11：真正的 MMCSS 局 → **MMCSS 无效，音频链结案**）

`framestall_20260925_101112`，39.4 s / 72196 帧，deep + 全程按键；本机已打开 `UseEz2LazerLocalFrameworkProject`，
framework 于 9:36:30 重建，音频日志 `logs/1790302201.audio.log` 出现 `… mmcss=Pro Audio (requested)`。

**一、漏拉：MMCSS 前后完全一样**

| | B 局（包，无 MMCSS） | **D 局（本地源码，有 MMCSS）** |
|---|---|---|
| `pullMiss`（位置停走 >15 ms） | 145（3.6/s，hold 2.8 s） | **142（3.6/s，hold 2874 ms）** |
| `wasapiRead` 间隔 p50 / p99 / max / `over15` | — | 10.25 / 20.25 / 20.71 ms / **162** |
| `wasapiPull` `periods/pull` max / `over1.5` | — | **2.00** / **162 / 3834（4.2%）** |

直读量具把机制从推断变成直读：渲染线程**每 0.24 s 一次、一次要两整拍**。
⇒ 按 §3.4e 三节写死的判据（「降到 ≤5 次即成立；仍是几十/几百次 = 与线程优先级无关」），
**音频链就地结案，不再为它安排采集**。剩下两个机制（线程晚醒 / `Read` 内 BASS 解码慢）当前量具分不开，
但两者的体感量级都只有「位置 std ~1 ms」，且应用侧无手段消除。

**二、局内帧管线 pristine（逐秒重建，之前被汇总数字掩盖）**

| 秒 | 帧数 | 帧长均值 | >5 ms | 子树均值 | 时钟停走帧 |
|---|---|---|---|---|---|
| 0–33 | 1794–2040/s | 0.490–0.558 ms | **除头两秒外全 0** | ≈0.10 ms | **0** |
| 34 / 35 | 1946 / 2026 | 0.514 / 0.494 | 2 / 0 | 0.048 / 0.000 | 793 / 2025 |
| **36 / 37** | **192 / 120** | **5.200 / 8.328** | **115 / 120** | 0.000 | 191 / 119 |
| 38 / 39 | 668 / 408 | 1.501 / 0.470 | 80 / 1 | 0.000 | 667 / 407 |

**该局 327 个 >5 ms 帧里 316 个在这 4.6 s 退场段**（子树已拆、update 掉到 120–192 Hz）；局内 33 s 只有 11 个。

**三、这解释了之前两笔读不懂的账**

- `present coverage = 0.869`：present 在 update 侧采样，退场段 update 掉到 120–190 Hz 而 draw 仍千帧级
  ⇒ 每秒漏掉近千个 draw 帧。**不是量具坏了，是采样者自己掉速。**
- 四局 `noPress over5` = 7 / 10 / 17 / **326**：各局在退场屏停留时长不同（`clockStopped` 1.7 s → 4.6 s），与局内无关。

**四、present 采样已加闸**：时钟停走的帧不再采（新增 `frozenSkipped=`），避免下一局同样的 `coverage` 自检失败；
帧耗时直方图保持不动以维持跨局可比，判读时人工减去退场段。

### 3.5 下一步（换量，不是继续找周期）

§3.1–3.4 的前提是「体感不流畅 = 某个 2–4 s 周期的帧时长波动」。**这个前提现在被证伪了**：
稳态帧时长在该带内只有 0.026 ms 抖动。所以要么现象不在这个量上，要么它根本不在「帧时长」这个维度。建议按序排查：

1. ~~**note 位置量化**~~ → **已测，见 §3.4b：干净**。`ScrollingHitObjectContainer` 用 FSC 的 `framedClock` 采样音频时钟，
   该时钟相对连续音频只有 **std 0.41 ms** 的偏差（含 109 Hz 纹波 ~0.4 ms）。所以「帧边界取时钟」这条链没有可感知抖动。
   剩下一个**本探针看不到**的变体：显示的那一帧用的是**上一帧边界**采到的时钟值（呈现时刻 ≠ 采样时刻），
   若 present 的节拍不均匀，画面上的位置就会**按呈现节拍**跳，而帧 CSV 只记录 update 侧。→ 见第 4 项。
2. ~~**音频时钟量化**~~ → **已测，见 §3.4b：干净，三个修复方向不需要动**。源确实是精确 10.000 ms 阶梯（100 Hz），
   但插值把它抹到了 std 0.41 ms。原先担心的「50 ms 半衰期追 10 ms 阶梯会注入 100 Hz 纹波」在**位置**上只有 ~0.4 ms，
   不足以成体感；VoiceMeeter 只给 10 ms 引擎周期这一事实仍然成立，但既然纹波不成问题，就**不必**为它换设备。
   （若将来另有证据指向音频，再回来考虑 ① 换设备/独占/ASIO ② 锁相或线性外推 ③ 只把音频钟当速率源。）
3. **换主观锚点**：让用户标出「不顺滑」的具体时刻（录屏 / 秒表 / 按键），再把探针时间轴对上去。
   四次测量都没测到，说明「约 2 s / 4 s」这个描述本身可能不准。
4. **present / 帧节拍**（更新线程之外，唯一没量过的一环）—— **量具已加，第一局暴露两处采样缺陷、已修（§3.4c）**：
   update 线程 ~2000 fps 平稳，但**呈现**只有显示器刷新率那么多次，要量的是**相邻两次 present 的间隔分布**
   与**上屏那一刻那份内容有多旧**。
   - **做不到在 draw 线程打点**：这个 fork 里 `Game` 是 `Container` 而不是 `GameHost`，`osu.Game` 无法 override
     `GameHost.DrawFrame`（它确实是 `protected virtual`，但不在继承链上）。
   - 改为在 update 侧读 `DrawThread.Clock`：`ElapsedFrameTime` **就是**「相邻两次 present 的间隔」，
     由 draw 线程自己的时钟测；`CurrentTime` 给出 draw 帧边界。
   - **采样口径（下次判读前先确认这四条，否则数全废）**：
     ① **按 draw 帧去重**（`dup=` 有值才对；update 比 draw 快，逐次采样会把长帧少采、均值拉低，实测 0.622 vs 0.826）；
     ② **`coverage` ≈ 1**（`ΣdrawPeriod / 首末壁钟跨度`；<1 = 有 draw 帧整个落在两次采样之间被漏掉。
     10:11 局实测 0.869，原因已定位 = 退场段采样者掉速，采样现在跳过时钟停走的帧 —— §3.4f 第四点）；
     ③ `drawPeriod` 的 **mean ≈ 1000 / `clocks` 里的 draw fps**，两个数对不上就是采样还没修对；
     ④ `presentAge` **min 不应为负**（为负 = 原点差标定错）。
   - 修好后的判读顺序：`drawPeriod` 的尾部（`over1/over2/over5`、`max`）→ `presentAge` 的 std / max
     **对照它自己的分辨率（一个 draw 周期）** → `refresh / fps` 是不是整数。
     `drawPeriod` 的 p50 落在 0.510 ms（= 限帧目标 2000 Hz）而 `sleptMean = 0`，说明 draw 是**工作受限**而非限帧受限。
   - ⚠ **不覆盖 DWM / 显示器扫描输出**。进程内量不到送屏之后的事；若 `present` 行也干净，
     剩下的就只可能是 tearing（Borderless 下 `AllowTearing` 是开着的）/ 组合器 / 主观锚定 → **见第 6 项**。
5. **音频源漏拉**（§3.4d 机制已定案；§3.4e 记录了交付陷阱与判据修正）：
   `interpErr` 一涨就先看摘要里的 `pullMiss`，但**不要用单局计数判断任何改动**（同代码三次采集 = 1 / 145 / 4）。
   - ✅ **MMCSS 已进 framework 源码**（`c86e585e4`：`NAudioWasapiOutput.cs:112` 加
     `.WithMmcssThreadPriority(AudioOutputDefaults.DEFAULT_NAUDIO_MMCSS_TASK)`，`"Pro Audio"`）。
     ⚠ 但**默认进不了游戏**——要打开 `UseEz2LazerLocalFrameworkProject` 或发包。
   - ✅ **直读量具已加**（`WasapiReadStats` + 汇总里的 `wasapiRead` / `wasapiPull` 两行，诊断局自动开）。
   - **判读顺序（一局即可判）**：先看音频日志有没有 `mmcss=`（证明跑的是本地 framework build）；
     再看 `wasapiPull periods/pull` 的 median 是否 = 1.0、`over1.5` 占比、以及 `wasapiRead` 间隔的
     p99 / max / `over15`。这些是**分布**，不依赖稀有事件的跨局可比性。
   - **❌ 结论（2026-09-25 10:11，§3.4f）：条目 5 关闭。** 带上 MMCSS 后 `pullMiss` 142 / `over1.5` 162 = 与不带时相同
     ⇒ 该漏拉**不是**渲染线程优先级问题，应用侧没有下一步动作。不要再为它排采集局。
6. **送屏 / 扫描输出（进程内量不到的一环）—— 现在只剩这个和「主观锚定」**
   - 事实：窗口是 `FullscreenBorderless`（DWM + `AllowTearing`），`FrameSync = Limit4x`、`FrameLimiterBase = 500`
     ⇒ draw 线程**工作受限**、以 ~1126 fps 送屏（该局 `clocks draw(fps=1126 jitter=0.520ms)`，`slept=0`）。
     显示器 175 Hz ⇒ **每次刷新期间发生约 6 次 swap**，画面是 6 帧拼起来的（撕裂/combing），
     而进程内探针永远看不到这一层（它们只看到「帧被交给驱动」）。
   - **唯一判据是 A/B，不是探针**：① `FrameSync = VSync`（draw 变 175 fps、节拍与刷新一一对应、无撕裂，
     代价是 +≤5.7 ms 呈现延迟）；② 或把限帧钉在刷新的整数倍（175 / 350）看撕裂是否从「移动的糊线」变成稳定分界。
     **若两种配置下体感不同 ⇒ 剩下的就是撕裂，不是游戏逻辑；若完全一样 ⇒ 回到第 3 项（主观锚定 / 录屏对时）。**
   - ⚠ 不要为了这个再动探针：它量不到扫描输出，加多少打点都一样。

## 4. 代码锚点

| 用途 | 位置 |
|---|---|
| 帧级 stall 探针 | `osu.Game/EzOsuGame/Diagnostics/EzFrameStallDiagnostics.cs` |
| 按键延迟分段探针 | `osu.Game/EzOsuGame/Diagnostics/EzPressLatencyDiagnostics.cs` |
| 判定诊断 | `osu.Game/EzOsuGame/Diagnostics/EzJudgmentDiagnostics.cs` |
| 探针接线 + 环境变量 | `osu.Game/Screens/Play/Player.cs`（约 344–368 行） |
| 子树 / 时钟 / 其余 归因 | `osu.Game/Rulesets/UI/FrameStabilityContainer.cs`（`UpdateSubTree`） |
| present / 帧节拍采样 | `EzFrameStallDiagnostics.samplePresent`（update 侧读 `DrawThread.Clock`，**按 draw 帧去重**，**时钟停走的帧跳过** —— 退场段会把 `coverage` 拉到 0.87，见 §3.4f）；host 由 `osu.Game/OsuGameBase.cs` 的 `SetHost` 挂上（**`DrawFrame` 无法 override，原因见 §3.5 第 4 项**） |
| 音频源漏拉计数 | 同文件 `accumulateClocks`（`pullMiss` / `pullMissHoldMs`，停走 >15 ms 且 <60 ms 记一次）。⚠ 同代码三次采集 = 1/145/4，**单局不可判**（§3.4e）；MMCSS 已试过无效，该链结案（§3.4f） |
| 退场段（时钟停走）判据 | 同文件 `accumulateClocks` 里的 `clockFrozen`（`interpMs == prevInterpMs`）：present 采样跳过它，帧直方图不跳过。逐秒重建见 §3.4f |
| 音频拉取直读（间隔 + 请求拍数） | `osu-framework/osu.Framework/Audio/Wasapi/WasapiReadStats.cs`（默认关闭；`BassMixerWaveProvider.Read` 里 `Observe`；`Player.cs` 诊断局起止处开关；汇总行 `wasapiRead` / `wasapiPull`） |
| **framework 改动进不了游戏的陷阱** | `Ez2Lazer.Dependencies.props`：`UseEz2LazerLocalFrameworkProject` 默认注释 ⇒ 走 NuGet 包 `ez2lazer.Framework`；验证办法 = 看输出 `osu.Framework.dll` 时间戳 + 音频日志有没有 `mmcss=` |
| 分析脚本 | `AnalyzeFrameStall.ps1`、`AnalyzePressLatency.ps1` |
| 音频输出路径（BASS mixer + NAudio WASAPI 拉流） | `osu-framework/osu.Framework/Audio/Wasapi/NAudioWasapiOutput.cs`；缓冲常量在 `Audio/AudioOutputDefaults.cs`（`DEFAULT_NAUDIO_LATENCY_MS = 10`） |
| 漏拉成因（NAudio 读法 + MMCSS 缺口） | `NAudio.Wasapi/WasapiPlayer.PlayThread`：读 `BufferSize − CurrentPadding`（**全部空位**，非固定一拍），`frameEvent` 是 AutoReset；`mmcssTaskName` 默认 null，只有 `.WithMmcssThreadPriority()` 才登记 MMCSS。构造链在 `NAudioWasapiOutput.cs:112`，**现已登记 `"Pro Audio"`，但实测无效**（§3.4f） |
| 音频源时钟 / 插值 | `osu-framework/osu.Framework/Audio/Track/TrackBass.cs`（`ChannelGetPosition`）、`osu-framework/osu.Framework/Timing/InterpolatingFramedClock.cs` |
| 周期 / 跨探针相位分析 | `AnalyzePeriod.py`（numpy；`--plot` 出图、`--trim 3,3` 去首尾过渡段） |
| 活文档 | `docs/EZ-PERFORMANCE.md` §2.4（周期性部分见 §2.4.10） |

## 5. 环境要点

- 配置：`F:\MUG OSU\EZ2OSU-lazer\framework.ini`（`ExecutionMode = MultiThreaded`、`FrameSync = Limit4x`）、`EzSkinSettings.ini`（`FrameLimiterBase`、`ColumnBlur`、`TurboMode`）。判 `Stage` 毛玻璃是否开启：`ColumnBlur × 50 > 0.01` 且 `TurboMode = False`。
- **送屏 A/B（§3.5 第 6 项）改的就是这两个键**：`framework.ini` 的 `FrameSync`（`Limit4x` ⇄ `VSync`）与 `EzSkinSettings.ini` 的 `FrameLimiterBase`（500 → 175 / 350）。改完必须重启游戏。
- 探针热路径不做 IO、不产字符串，落盘在局末；`RecordFrame()` 跑在输入派发**之前**。
- 改 Realm schema / 迁移后**不得**擅自启动客户端做验证（见 `.cursor/rules/realm-schema-development.mdc`）。

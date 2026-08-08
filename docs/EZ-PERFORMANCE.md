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
| 高 KPS 下 alloc 抬高 | `GetHitModeValidHitResults` 每次 `new[]`，经 `ResultFor` / `SelectFold` 放大 | 改静态表，`ResultFor` 零分配（Combo dense 约 1.8 KB → 45 B/press） | `osu` |
| 启动期大量谱面时 GC 爆炸、持续卡顿 | 全量 detached beatmap cache + 启动同时做核心分析与标签补算 | 见 [`EZ_ANALYSIS_STORAGE_REDESIGN.md`](./EZ_ANALYSIS_STORAGE_REDESIGN.md)：分析 / 标签 / 写库边界分离 | `osu` |
| 皮肤交互导致帧率与 GC 抖动 | 热路径创建绑定副本、频繁 `BindTo`/`Unbind`、绑定链上 `SkinInfo.TriggerChange()` | 见 [`EzSkinSystemNotes.md`](./EzSkinSystemNotes.md) 的禁止项 | `osu` |

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

## 修订记录

| 日期 | 说明 |
|------|------|
| 2026-08-08 | 初版：汇总各文档 FPS / 性能测试描述；记录音频后端排查与振幅限频（框架 `e22805587`） |

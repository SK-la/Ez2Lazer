# Mania ReplayJudge — ppy 合并 checklist

## 架构：HitMode 原生判定 + MapTo

每个 Ez HitMode 在 `EzMania/ReplayJudge/Mappings/{Mode}HitModeJudgement.cs` **单文件**内包含：

1. **Mode 原生 enum**（如 `BmsJudge.Bad`、`O2Judge.Cool`）
2. **`MapTo(judge) → HitResult`**（写入 ScoreProcessor / ApplyResult 边界才转换）
3. **核心判定**（窗口、KPoor 状态机、pill、tail 等）
4. **`IManiaHitModeJudgement` 实现**（Session 与 Drawable 共用）

对话与代码审查优先使用 Mode 名，避免与 Lazer Perfect/Great/Meh 混淆。

### 双轨

| 路径                             | 判定源                                                               |
|--------------------------------|-------------------------------------------------------------------|
| Lazer/Classic 游玩               | 官方 `DrawableNote.CheckForResult` inline（不抽离）                      |
| Lazer/Classic Session          | `Replicas/Lazer*JudgementReplica`                                 |
| Ez HitMode 游玩                  | `DrawableNote` 一行 Switcher → `ManiaEzDrawableJudgement` → Mapping（**禁止** HitMode 专用 Drawable 子类；Malody LN 等同理） |
| Ez HitMode Session / Generator | `ManiaReplaySession` → `ManiaJudgementRegistry` → 同一 Mapping      |

Drawable replay 播放时，`ManiaEzDrawableJudgement` 优先从 `DrawableRuleset.ReplayScore.ScoreInfo` 取 HitMode（`FromScore`），无 embedded 时回退 `FromLive`。

### 成绩统计环境

| 字段                                 | 来源                                                                                   |
|------------------------------------|--------------------------------------------------------------------------------------|
| HitMode / HealthMode（统计 HitEvents） | `ScoreInfo.ManiaHitMode` / `ManiaHealthMode`（提交时写入）→ `GameplayEnvironment.FromScore` |
| HitMode / HealthMode（角逐时间线）     | **当前全局** `FromLive`；不读成绩嵌入字段                                                     |
| JudgePrecedence / OffsetPlusMania  | 当前全局配置（`FromLive` fallback）                                                          |
| **BmsPoorHitResultEnable (KPoor)** | **当前全局配置**（未写入 ScoreInfo；统计重算沿用打开成绩时的全局 KPoor 开关，与当时游玩可能不一致）                         |

生产入口：`StatisticsPanel` → `EzScoreReloadBridge` → `ManiaScoreHitEventGenerator` → `ManiaReplaySession`。

`ManiaRuleset.RunReplayAsync` 为无 UI 公开 API，与 Generator 同源；CLI/工具可直调。

### 分数时间线（角逐 HUD 等下游消费）

| 路径 | 数据源 |
|------|--------|
| 统计 / HitEvents | `ManiaReplaySession.Run` → `ScoreProcessor.HitEvents` |
| **时间线** | `ManiaReplaySession.RunTimeline` → 同一遍 SP，每次 `ApplyResult` 后采 `TotalScore`/`Accuracy`/… 快照 → `EzScoreTimeline` |

- **禁止** Mania 生产路径：`HitEvents` → `buildFromHitEvents` → 第二遍 SP。
- 下游（如角逐 HUD）只调用 `EzScoreTimelineBuilder.TryBuild`；`EzScoreTimelineBridge` 在 Mania 侧注册 `RunTimeline`。
- 无本地 replay（`GetScore` 后 `Replay == null` 或空帧）→ `TryBuild` 返回 `null`；角逐 ghost 在 timeline 未就绪前显示 0，**禁止**用终局 `ScoreInfo.TotalScore` 充当实时分。
- 时钟轴 = replay 输入时刻（`input.Time`），与游玩 `GameplayClock` 对齐。
- Ez 满分制 `TotalScore` = 整谱 ex 累积（`MinimumAccuracy × 1M`），与正常游玩 ScoreProcessor 同源；ghost 分数轨 = 同一 SP 快照，HUD **只读** `QueryAtTime(...).TotalScore`，不在 EzOsuGame 复刻公式。

### 角逐 HUD 实机验收清单（Mania）

1. 本地 Mania 谱面 ≥2 条带 replay 的 ghost + 角逐排行榜：占位先出（分数 0），时间线异步填入后分数/准度/Combo 随时钟变化。
2. 同一时刻：主界面 ScoreCounter ≈ 角逐 Tracked 行 ≈ ghost 行（对应 replay 进度）；分数从 0 累积，无 acc 子集反算虚高。
3. 角逐榜排序可配置（默认 TotalScore，可选 Accuracy / MissCount）；只影响榜位，不参与计分。
4. ghost 分数轨统一按**当前全局** HitMode/HealthMode 重算（`FromLive`）；不用成绩嵌入 HitMode，也不按嵌入 HitMode 过滤 ghost 列表。
5. 无 replay 的 ghost：不进入角逐列表（`TryBuild` 返回 null）。
6. DT/HT：时钟与 ghost 分数轨无明显错位（若有问题记录谱面与 mod 组合）。

---

## 合并 ppy/osu master 后

1. **diff 官方判定入口**
   - `Objects/Drawables/DrawableNote.cs` → `CheckForResult`
   - `Objects/Drawables/DrawableHoldNoteTail.cs` → `CheckForResult` / `GetCappedResult`
   - `UI/OrderedHitPolicy.cs` → Earliest / Combo 路由

2. **同步 Ez Replica**（仅 Session，官方不引用）
   - `Replicas/LazerNoteJudgementReplica.cs`
   - `Replicas/LazerHoldJudgementReplica.cs`
   - `ManiaColumnSimulator.cs`（Earliest note lock）

3. **Ez Mapping 不需 diff 官方**；合并上游后若 HitMode 行为变更，只改对应 `Mappings/*HitModeJudgement.cs`。

4. **跑 parity**
   - `ManiaReplaySessionTest`（Session vs Generator，含 `FromScore`）
   - `TestSceneReplaySessionParity`（Drawable vs Session：Lazer tap/hold、IIDX tap/hold、O2 tap/hold/pill、Ez2AC hold、Malody hold）
   - `JudgePrecedenceRoutingRegressionTest`
   - `dotnet test osu.Game.Rulesets.Mania.Tests`（全量）

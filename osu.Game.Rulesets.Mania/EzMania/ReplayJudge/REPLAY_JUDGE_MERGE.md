# Mania ReplayJudge — ppy 合并 checklist

## 架构：HitMode 原生判定 + MapTo

每个 Ez HitMode 在 `EzMania/ReplayJudge/Mappings/{Mode}HitModeJudgement.cs` **单文件**内包含：

1. **Mode 原生 enum**（如 `BmsJudge.Bad`、`O2Judge.Cool`）
2. **`MapTo(judge) → HitResult`**（写入 ScoreProcessor / ApplyResult 边界才转换）
3. **核心判定**（窗口、KPoor 状态机、pill、tail 等）
4. **`IManiaHitModeJudgement` 实现**（Session 与 Drawable 共用）

对话与代码审查优先使用 Mode 名，避免与 Lazer Perfect/Great/Meh 混淆。

### 双轨

| 路径 | 判定源 |
|------|--------|
| Lazer/Classic 游玩 | 官方 `DrawableNote.CheckForResult` inline（不抽离） |
| Lazer/Classic Session | `Replicas/Lazer*JudgementReplica` |
| Ez HitMode 游玩 | `DrawableNote` 一行 Switcher → `ManiaEzDrawableJudgement` → Mapping |
| Ez HitMode Session / Generator | `ManiaReplaySession` → `ManiaJudgementRegistry` → 同一 Mapping |

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
   - `ManiaReplaySessionTest`（Session vs Generator）
   - `TestSceneReplaySessionParity`（Drawable vs Session，Lazer）
   - `JudgePrecedenceRoutingRegressionTest`

# Osu ReplayJudge — Session、Shadow 桥与毕业路径

Osu 曾用 **Shadow Judgement** 作为脱离 Drawable 的过渡桥（OSL-010）。  
**壳 + 桥已通 ≠ Mania 级能力**：缺共用判官、env 重算、ClassicNative。见 [REPLAY_JUDGE_SHADOW.md](./REPLAY_JUDGE_SHADOW.md)、[EZ-SR-TL-REGISTRY.md](./EZ-SR-TL-REGISTRY.md) OSL-011+。

---

## Session 黄金标准

**验收标准**：

> 同一 score + 同一 environment 下，`OsuReplaySession.Run` 产出的 **HitEvents + Score** 必须与 ReplayPlayer 回放一遍、进入结算时 `ScoreProcessor` 已填充的结果 **字段级一致**。

| 路径 | 是否绘制 | HitEvents 来源 |
|------|----------|----------------|
| ReplayPlayer 回放 | 是 | `ScoreProcessor` → PopulateScore |
| 排行榜 / StatisticsPanel / 角逐 | **否** | `OsuReplaySession.Run` → **必须等价** |

`OsuScoreHitEventGenerator` 仅为薄壳委托 `OsuReplaySessionService`；**不是**参考实现。

**Osu HitEvent 额外字段**：`CursorPositionAtHit`（`OsuHitCircleJudgementResult`）须在 parity 中一并断言。

Parity：`TestSceneOsuReplaySessionParity`（circle / slider / spinner）。

---

## 架构状态

| 组件 | 路径 | 状态 |
|------|------|------|
| Session API | `OsuReplaySession.cs` | done（OSL-007） |
| Service + cache | `OsuReplaySessionService.cs` | done |
| Timeline | `OsuReplayTimelineRecorder.cs` | done |
| Shadow 引擎（桥） | `Shadow/*` | **bridge done**（OSL-010） |
| 判定 helper / Drawable 一行 | `ReplayJudge/Judgement` 或 `Mappings/` | **open（OSL-011）** |
| Session 读 env | Simulator / Engine | **open（OSL-012）**；桥期曾丢弃 environment |
| ClassicNative 轨 | Mapping + ScoreProcessor | **open（OSL-013）** |

---

## OSL-010（归档）

- **S0–S4 done**：Circle + Slider + Spinner + Parity  
- 精度来源现为 `OsuReplayShadowEngine`；毕业后改为与 Drawable 共用的 helper（OSL-011）

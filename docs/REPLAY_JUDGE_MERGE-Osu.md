# Osu ReplayJudge — Session 与 Shadow 判定

Osu 采用 **Shadow Judgement**（影子判定），与 Mania 的 HitMode/Mapping 路径分离。三模式统一设计见 [REPLAY_JUDGE_SHADOW.md](./REPLAY_JUDGE_SHADOW.md)。

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

Parity 测试（OSL-010 S4）：`TestSceneOsuReplaySessionParity`（circle / slider / spinner）。

---

## 架构（OSL-007 + OSL-010）

| 组件 | 路径 | 状态 |
|------|------|------|
| Session API | `OsuReplaySession.cs` | done |
| Service + cache | `OsuReplaySessionService.cs` | done |
| Timeline | `OsuReplayTimelineRecorder.cs` | done |
| **Shadow 引擎** | `Shadow/OsuReplayShadowEngine.cs` 等 | **done**（OSL-010） |
| 旧 press 循环 | ~~`OsuReplaySessionSimulator` 内启发式~~ | S1 起由 Shadow 替代 |

---

## OSL-010 进度

- **S0–S4 done**：Circle + Slider tracking + Spinner 转速 + `TestSceneOsuReplaySessionParity`

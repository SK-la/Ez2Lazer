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
| Session 引擎（Mapping） | `ReplayJudge/Session/*` | **done（OSL-011）**；原 `Shadow/` 已删 |
| 判定 helper / Drawable 一行 | `ReplayJudge/Judgement` | **done（OSL-011）** |
| Session 读 env | Simulator / Engine | **OSL-012**：已读轨/offset；Offset 不进判窗；Panel `RunHitEventsAsync` 不可变门禁 |
| ClassicNative 轨 | Mapping + ScoreProcessor | **OSL-013 in progress**：窗注入 + Session 晚点 Lazer≠Classic 门禁；stable 满分细部仍 open |

### OffsetPlusNonMania（禁叠判）

权威：[EZ-SR-TL-REGISTRY.md](./EZ-SR-TL-REGISTRY.md) §1.7b Offset 原则。

- **Drawable 局内**：裸输入 → `timeOffset += OffsetPlusNonMania` 再进判窗。
- **Session 吃 replay**：帧时刻已是有效输入；`ResultForPress` / 滑条头窗用裸帧差；**禁止**再把 OffsetPlus 叠进判窗（相对帧 = 加两次）。
- Resolve：ForStored / 分析 ignoreOffset → `OffsetPlusNonMania=0`。非 0 时至多影响 HitEvent.`TimeOffset` 元数据，不改 `HitResult`。

---

## OSL-010（归档）

- **S0–S4 done**：Circle + Slider + Spinner + Parity  
- 精度来源：`OsuReplaySessionEngine` + Judgement helpers（OSL-011 done）

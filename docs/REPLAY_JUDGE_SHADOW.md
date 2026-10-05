# Replay Shadow Judgement（归档：桥，非终态）

Ez2Lazer 在 **Osu** 曾用 **Shadow** 作为 Session 脱离 Drawable 的**过渡桥**。  
**Shadow 不是终态。** Taiko / Catch **禁止**永久 Shadow 树；二者已按 Mapping 形态接线。

相关：

- 框架注册表：[EZ-SR-TL-REGISTRY.md](./EZ-SR-TL-REGISTRY.md)（验收线 1 / OSL-011+）
- Mania（独立路径）：[REPLAY_JUDGE_MERGE-Mania.md](./REPLAY_JUDGE_MERGE-Mania.md)
- Osu 落地：[REPLAY_JUDGE_MERGE-Osu.md](./REPLAY_JUDGE_MERGE-Osu.md)

---

## 1. 与 Mania 的关系

| | Mania | Osu（桥 → 毕业） | Taiko / Catch |
|---|--------|------------------|---------------|
| 判定源 | Ez Mapping 与 Drawable 共用；Lazer 用 Replica | **OSL-011**：抽出 helper/Mapping，删 Shadow 生产路径 | Mapping Session（已接线） |
| Session 目标 | env 可切换重算 | 共用判官 + env 真读 + 消费矩阵 | 原成绩全指标 + 深 parity |
| 产品轨 | 多 HitMode | `Lazer \| ClassicNative`（OSL-013） | 不引入多 HitMode 菜单 |

**禁止**把 Shadow 树复制到 Taiko/Catch。Osu 允许桥期残留，但 **OSL-011 关闭条件 = 删除 Shadow 生产路径**，禁止「Sparse Shadow 够用」停留关闭。

**Mania Session 金标收敛**：另开，不纳入本验收线。

---

## 2. 影子判定（Shadow Judgement）定义（归档）

**影子判定** = 无绘制、按 replay 时钟推进，维护与 Drawable/ReplayPlayer **等价的逻辑状态**，在正确时刻向 `ScoreProcessor.ApplyResult` 喂入 `JudgementResult`。

**禁止**：

- 生产路径 HeadlessGameHost 跑完整 `DrawableRuleset`
- 第二遍 HitEvents → SP（F 类）
- 为 Session 单独维护与 Drawable 无关的 press 启发式
- 将 Sparse 密采样 / Update 仿真正式化为终态

**允许 / 出口（OSL-011）**：

1. 纯函数提取到 ruleset helper，Drawable 一行调用  
2. Session 走 **非 Shadow** Mapping/事件引擎  
3. 删除 `*Shadow*State` / `*ReplayShadowEngine` 生产路径（cursor 可改名迁出作 replay I/O）

---

## 3. 分层（现行）

| 层级 | 职责 | Osu | Catch / Taiko |
|------|------|-----|----------------|
| `EzOsuGame/Scoring` | `IEzReplaySession`、Timeline、Race、cache | ✓ | ✓ 已接线 |
| `Rulesets.*/Ez*/ReplayJudge/` | `*ReplaySession` / Service / Judgement helpers | OSL-007 done；OSL-011 拆桥中 | Mapping Session **已接线**（TTL/TSL） |
| `.../ReplayJudge/Shadow/` | 过渡桥（待删） | OSL-010 归档；**OSL-011 删除里程碑** | **禁** |
| `*ScoreHitEventGenerator` | 薄壳委托 Service | done | 同形 |

### Osu Shadow 删除里程碑（OSL-011）

| 步骤 | 内容 | 关闭条件 |
|------|------|----------|
| H1 | Circle / Slider / Spinner helper 齐；Drawable 一行 | helpers 无双份语义 |
| H2 | 非 Shadow Session 引擎替换 `OsuReplayShadowEngine` | 生产路径无 ShadowEngine |
| H3 | 删 `OsuShadowSliderState` / `OsuShadowSpinnerState` / `OsuReplayObjectScheduler`（或迁出非 Shadow） | `Shadow/*State` 不存在 |
| H4 | cursor 改名迁出 `Shadow/`（可选） | 包名不再暗示永久 Shadow |

---

## 4. 黄金标准

> 同一 score + 同一 environment 下，`ReplaySession.Run` 的 **HitEvents + Score 全指标** 必须与原成绩 / ReplayPlayer 一遍后 `ScoreProcessor` 字段级一致。

Catch（TSL-001）硬门禁：TotalScore / MaxCombo / Accuracy / Rank / Statistics 全键 ≡ 原成绩（osr Header / Realm）。

Parity：各 ruleset `TestScene*ReplaySessionParity` + `*OsrAuditTest`。

---

## 5. 实施顺序（验收线 1）

| 阶段 | Ruleset | 注册 ID | 内容 | 状态 |
|------|---------|---------|------|------|
| done | Osu | OSL-007~010 | 壳 + Shadow 桥 + Parity | 归档 |
| **必达** | Osu | **OSL-011** | Mapping 毕业；删 Shadow 生产路径 | open |
| open | Osu | OSL-012 / 013 | env 矩阵 / ClassicNative | open |
| **必达** | Taiko | TTL-001 | Mapping + 深 parity + 全指标 | in progress |
| **必达** | Catch | TSL-001 | Mapping + **全指标 ≡ 原成绩** | in progress |
| 另开 | Mania | — | Session 金标收敛 | 另开 |

---

## 6. OSL-010 子阶段（已完成，归档）

| PR | 内容 | 状态 |
|----|------|------|
| S0 | 本文 + MERGE-Osu + REGISTRY | done |
| S1 | Cursor + Circle + Engine | done |
| S2 | `ShadowSliderState` | done |
| S3 | `ShadowSpinnerState` | done |
| S4 | `TestSceneOsuReplaySessionParity` | done |

# Session / Timeline / Race

跨模式一遍 SP、多出口、env、Offset。Mania 判定细节 → [`mania.md`](./mania.md)。

---

## 1. 原则

1. Realm Statistics **不可**被 Session 自动覆盖（例外：用户显式重算）。
2. 同 score+env **只仿真一遍** → Score / HitEvents / Timeline 多出口。
3. HitEvents 不持久化；Panel 补事件 = 同次 Run 的子集。
4. Mania **禁止** HitEvents→SP 建 Timeline（E/F）。
5. Shadow **不是终态**；禁止 Headless DrawableRuleset 当生产 Session。

---

## 2. 一遍 SP · 多出口

| API | 出口 |
|-----|------|
| `Run` | Score（含 HitEvents） |
| `RunTimeline` / `RunTimelineDirectAsync` | `EzScoreTimeline` |
| `RunHitEventsAsync` | HitEvents 子集（共 cache） |
| `RunRequestAsync` | Score + Timeline |

Service：`RunWithTimeline` + `sessionRunCache`。角逐 Builder 用 `RunTimelineDirectAsync`（不经 TimelineCache）。

| 消费 | Purpose | 备注 |
|------|---------|------|
| Panel 补 HitEvents | ForStored | 只 patch HitEvents |
| Graph Now / Race | ForLive，offset=0 | 共 cache |
| Graph 拖 offset（C） | — | **不跑** Session |
| Graph offset 落定（D） | ForLive | 新 env → 新 key |
| 重算 | ForStored / ForLive | 可写 Realm |

env：`Ez2ConfigManager.ResolveEnvironment(purpose, score?, ignoreOffset?)`。Session 内 resolve，不外传 env。

---

## 3. Offset（四模式）

| 路径 | OffsetPlus* 进判窗？ |
|------|---------------------|
| 真实对局 Drawable | **是** |
| replay Session（Panel/Graph/Race/重算） | **否**（帧已是有效输入） |

禁止：Drawable 已加 offset，Session `ResultForPress` 再叠一遍。

---

## 4. 模式状态

| 模式 | Session | 判定单源 | 开放 |
|------|---------|----------|------|
| Mania Ez | ✓ | Kernel+Mapping | 金标缝另开；删双份见 mania.md U1–U2 |
| Mania Lazer | ✓ | **仍双份**（inline↔Replica） | U1 |
| Osu | ✓ Mapping 引擎 | helpers 共用；编排状态机双份 | OSL-013 ClassicNative 细部 |
| Taiko | ✓ Mapping | ✓ | — |
| Catch | ✓ Mapping | ✓ | — |

Shadow：Osu OSL-011 **已删**生产路径。Taiko/Catch **禁止**永久 Shadow。

Osu 金标同 Mania：Session ≡ ReplayPlayer SP；额外字段 `CursorPositionAtHit`。

---

## 5. 禁令速查

| 代号 | 含义 | 态度 |
|------|------|------|
| C | Graph 拖 offset 预览 | 合法 UX |
| D | offset 落定再 Session | 合法 |
| E/F | HitEvents→SP→Timeline | **禁止**（F 已消） |

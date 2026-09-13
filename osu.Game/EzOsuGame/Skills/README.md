# EzOsuGame Skills

This folder contains the skill computation pipeline used by Ez2Lazer's mania-related features.

## What lives here

- Mania difficulty engine:
  - `IEzMsdEngine` / `EzCalcNote` — engine abstraction and its row type.
  - `EzNKeyMsdEngine` — the 4–18K n-key MinaCalc host (`minaclac-74.0.wasm` via Wasmtime).
  - `EzSkillsetVector` — the engine's eight skillset axes.
- Beatmap MSD computation and caching:
  - `EzBeatmapMsdComputer`
  - `EzMinaNoteConverter`
- Chart analysis and filing:
  - `EzChartSkillInfoComputer`
  - `EzChartDanEstimator`
  - `EzDanSkillsetFiling`
  - `EzDanSkillsetBuckets`
- The revision system that decides when the above recompute:
  - `EzAnalysisRevision` (`Analysis/` folder) — facet revisions + dependency edges.
  - `EzSkillDataStatus` — ready / unrateable / stale / missing per facet, via
    `EzSkillStore.GetSkillDataStatus()`.
- Player SSR aggregation:
  - `EzPlayerSsrAggregator`
  - `EzDanPlaySsrIndex`
- Pattern / motion / jack / LN helpers:
  - `EzManiaPatternAnalyzer`
  - `EzMotionFeaturesComputer`
  - `EzFourKeyJackDemand`
  - `EzLnSkillRadar`
- Dan ladder / label helpers:
  - `Dan/`
  - `EzDanLadders`
  - `EzDanSkillsetBuckets`

## Current state

- MSD/SSR run on the mania-hub n-key MinaCalc build (`0.74.0`), hosted in-process by
  Wasmtime. `osu.Game/Resources/EzSkills/NOTICE.md` records provenance, licence and the
  SSR clamp patch; the wasm is an embedded resource, so there is no network or file dependency.
- **4–18K** all go through the same note-array path (`EzCalcNote` = column bitmask + row
  time). There is no `.osu`-text branch and no per-keymode gate left in the compute path.
- 1–3K and 19K+ are unsupported: `IEzMsdEngine.SupportsKeyCount` is the single source of
  truth, and unsupported/too-short inputs return the zero vector instead of throwing.
- MSD is `goal = 0.93`; SSR passes the score's goal clamped to `[0.8, 0.9975]`. The engine
  has no separate MSD/SSR switch.
- The vendored module aborts on a chord wider than 16 columns. The engine discards the
  instance and rebuilds, so one bad chart costs a rebuild rather than poisoning the batch.
- Wasmtime ships native libraries for desktop RIDs only (win/linux/osx, x64 + arm64).
  On Android/iOS `EzNKeyMsdEngine` cannot instantiate; the failure surfaces as
  `EzMsdEngineException`, which the callers already treat as "this chart has no MSD/SSR".
- `EzManiaSkillAlgorithm.VERSION` gates MSD. The derived caches do not need a bulk clear:
  ChartSkillInfo and ChartDan rows are stamped with a composed revision (`EzAnalysisRevision`) that
  folds this constant in, so they read as missing and the ordinary incremental backfill recomputes
  them — see 「版本号 / 影响面」 below.
- Chart dan routing: 4/6/7K use their own community xxy→dan interval tables (`EzSunnyDanIntervals`),
  with the calibrated MSD means table only as the no-xxy / dual-half fallback. No other keymode has
  a table, so it borrows the 4K table on the same side (`EzDanLabels.TryResolveFallbackDan`): the raw
  4K MSD means table is off-scale above 4K and inflated those labels by several levels, and the star
  rating is the only community-calibrated number those keymodes have. No star rating = no invented dan.
- `EzDanAlgorithm.VERSION` gates player clears and is folded into the ChartDan revision; bump it when
  the dan mapping changes (currently `4`) so old chart rows are filtered out rather than read as stale.
- Downstream coverage is not yet uniform above 9K — see `DATA-FOLLOWUPS.md`.

## 版本号 / 影响面（`EzAnalysisRevision`）

图表技能链是三级级联 `MSD → ChartSkillInfo(CSI) → ChartDan(Dan)`。每一级的持久化行只存一个
**组合修订号**（不是单一算法常量），读回时要求完全相等，不相等就是"缺失"，交给常规增量补算重算。

| 改了哪个常量 | 谁自动失效 | 生效方式 |
|---|---|---|
| `EzManiaSkillAlgorithm.VERSION` | MSD → CSI → Dan | MSD 行由 `RulesetInfo.LastAppliedManiaSkillVersion` 标量清空；CSI/Dan 行因组合号变化自然读到缺失 |
| `EzChartSkillInfo.VERSION` | CSI → Dan | Dan 的组合号含 CSI 版本，因此只动 CSI 也会废掉 Dan |
| `EzDanAlgorithm.VERSION` | Dan | 只有 Dan 行受影响 |
| `EzAnalysisRevision.CSI_POLICY` / `DAN_POLICY` | 该级 + 下游 | 生产逻辑变了但没有算法常量能表达时（新增依赖边、改动读取路径）bump 它 |
| `EzXxyStarRatingSupport` 版本 | Dan（显式清表） | xxy 落在 `BeatmapInfo` / ruleset 标量上，不进组合号，所以由 `clearOutdatedXxyStarRatings` 显式 `ClearChartDan()` |

组合规则：`revision = compose(自身常量 + 所有上游常量)`，每槽 base-100（`SLOT_RADIX`），
所以每个参与常量必须小于 100。`EzAnalysisRevision.DownstreamOf(facet)` 是依赖边的可执行声明；
新增一条依赖边时，同时改它和上面这张表（`EzAnalysisRevisionTest` 覆盖了 `SLOT_RADIX` 约束和下游集合）。

### 为什么不用官方 star / xxySR 的单标量

官方 star 与 xxySR 各是 ruleset 上的一个 `LastApplied*Version` 标量：比对 → 把磁盘值清成 -1 → 整类重算。
对本链有两个问题：

- **单标量表达不了级联。** CSI 变了而 MSD 仍然有效时，标量只能整体重算，把最大的 MSD 一起废掉。
- **兄弟项互不感知。** xxy 只影响 Dan，但标量机制里没有"xxy 只让 Dan 失效"的位置，只能靠显式清表补丁。

代价是编号本身不透明：`10003` 要靠 `DescribeChartSkillInfo` / `DescribeChartDan` 展开成
`policy=1 csi=3 msd=2`。收益是失效范围精确到一级、不需要清库，并且"过期"本身可观测
（`GetSkillDataStatus()` 的 stale 计数）。

### MSD 为什么不进组合

`EzBeatmapSkillValue` 是 MSD / 玩家 SSR / pattern 共用的表，多个读取端显式传算法版本；把 MSD 换成
组合号会变成跨 facet 风险。MSD 失效继续由 `LastAppliedManiaSkillVersion` 标量负责。

### 可见性

- 启动日志：`Ez chart chain revisions: MSD v…, CSI v… (policy=… …), Dan v… (…)`。
- 每一段补算仍各自 `showProgressNotification`；因为过期行对增量集合就是"缺失"，换版后启动会自动弹进度。
- 设置 → Ez → 实验性 → **技能数据状态**：`EzSkillStore.GetSkillDataStatus()` 的
  ready / unrateable / stale / missing，回答"数据有没有缺、有没有过期"。

## Key entry points

- `IEzMsdEngine` / `EzNKeyMsdEngine`: the difficulty engine abstraction and its 4–18K implementation.
- `EzBeatmapMsdComputer`: chart-side MSD compute and persistence.
- `EzChartSkillInfoComputer`: chart metadata, pattern tags, motion, LeoBlack cluster fallback.
- `EzChartDanEstimator`: chart dan calculation and live snapshot flow.
- `EzDanSkillsetFiling`: skillset bucketing and quorum logic.
- `EzPlayerSsrAggregator`: player-side SSR aggregation.

## Relevant supporting docs

- `DATA-FOLLOWUPS.md` — tracked long-running follow-up items.
- `418K-Alignment-PLAN.md` — step-by-step plan for 4–18K parity work.

## Notes for contributors

- Keep behavior changes covered by tests in `osu.Game.Tests`.
- New interfaces added in this folder should start with the `IEz` prefix.
- Engine selection is not a per-keymode decision any more: route through `IEzMsdEngine`
  and gate on `SupportsKeyCount`, never on a hard-coded 4/6/7 list.
- Changing note conversion or aggregation semantics means bumping
  `EzManiaSkillAlgorithm.VERSION`. Derived rows (MSD → ChartSkillInfo → ChartDan) retire themselves
  through the composed revisions — do **not** add a bulk clear for the chain, which would erase the
  "stale" evidence the status row reports. A change that no algorithm constant captures gets a
  `CSI_POLICY` / `DAN_POLICY` bump plus a `DownstreamOf` update.
- When introducing new keycount support, update both the compute path and the filing / ladder consumers.
- The vendored wasm is single-threaded with mutable globals: one engine instance per thread,
  and never share a `Store`/`Instance` across concurrent computes.




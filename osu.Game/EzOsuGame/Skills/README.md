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
  ready / unrateable / stale / missing，回答"数据有没有缺、有没有过期"；同一行末尾附带玩家链的
  「玩家技能 落后账本 N」（`GetStalePlayerSkillUsernames()`）与派生的「等图表链 N」
  （`EzChartChainDebt`），所以两条链的状态在一个地方能看完。
- 玩家侧另有一套可见性：`EzPlayerSkillValue.Stale` 在个人主页显示为「待更新」
  （`LOCAL_PROFILE_SKILL_STALE`）；手动「计算成绩分析」弹 `EzLocalProfileComputeNotification` 进度
  通知（一次点击只弹一轮）。

## 玩家链（成绩分析）：与图表链不同的第二套失效机制

图表链（上面那套）失效靠**组合修订号**；玩家链（SSR / pattern / Dan 的玩家侧汇总）失效靠
**脏位 + 派生债务**，因为它的"上游"是 Realm 里不断新增的成绩，而不是磁盘上的一批谱面行。

两级、各有一个真源：

| 阶段 | 真源 | 写点 |
|---|---|---|
| SQLite 切片：`drill_scores`（每局明细 + 账本）+ `username_partitions`（每玩家计数器） | `drill_scores` 就是"已分析"账本 | 一局结算入库时立即追加（`Player.ImportScore` → `EzLocalProfileService.IngestSettledScore`） |
| Realm 技能行：`EzPlayerSkillValue`（SSR / pattern）、`EzDanEstimate` / `EzPlayerDanSkillsetValue` | 从上一步的切片聚合而来 | 只在整段 compute 里写（`writePlayerSkills`） |

一局结算只做第一步，然后把该玩家（和 `All`）的 Realm 技能行置 `Stale`——**不**在结算路径上重算，
否则每局都要做一次玩家级聚合。第二步只在用户手动「计算成绩分析」时收口：**一次点击 = 一轮**，
跑完不留任何后台回折（启动时、图表链收工后都不再自动补算）。

### 一轮计算覆盖谁

默认技能范围（`ComputeAsync` 未显式传 `skillsUsernames` 时）是两类玩家的并集，判据都从持久化
状态现读，不需要额外记账列：

| 来源 | 判据 | 覆盖的场景 |
|---|---|---|
| 脏位 | `EzPlayerSkillValue.Stale`（`EzSkillProvider.PlayerSkills.SetStale`） | 已写进切片、技能行还没跟上（只有结算入库会置位；一次覆盖该玩家的 pass 清除） |
| 图表链债务 | `EzChartChainDebt`：`drill_scores` 的 (玩家, 谱面) 对图表链覆盖现算 | 该谱面还没被图表链评级，这一轮读不到；跑完「重算 Realm」后再点一次即可折入 |

两类都在同一遍 compute 内收口：覆盖到的玩家按玩家整体清脏位；无法重算的玩家（该切片没有 mania
成绩）直接删行，避免脏位永远清不掉。`Stale` 只表示"值落后账本"，不再兼表"等图表链"——后者由派生
债务表达，链一写出行就自己消失。

### 为什么玩家链不复用组合修订号

- 上游不是"一批会被整体换版的谱面行"，而是**可增可删的成绩**：新成绩天然是"账本里没有"，
  删除的成绩则要让对应玩家整段重算（drill 明细删掉后计数必须一起扣）。
- 因此账本 diff 本身就是最精确的失效判据，"过期"与"新增"是同一个判据的两个方向；
  再加一层组合号只会多一份会和账本漂移的簿记。
- 代价：Realm 技能行的**删除**没有脏位可用（Dan 行无 `Stale` 字段），只能靠实际重算修正；
  这也是对账选增量而非清库重建的原因——增量足够精确，清库会把每局缓存一起废掉。

### 玩家链只读图表侧数据，缺失的交回图表链

玩家段需要图表侧三样东西：SSR 的 pattern 标签（CSI）、Dan 的 `EzPersistedChartDan`、以及
mod 影响谱面时的现场估算。前两样**只读不写**：读不到就跳到下一局，并把该谱面记进
`MissingChartHashes`。这一轮**不去排图表链**，也**不写脏位**——缺的局根本没进这一遍写的行，
说清楚它们少算了是日志（`N chart(s) have no chart-side skill data yet`）和派生的「等图表链」的事：
`Stale` 只表示"切片领先了这些行"，写上去只有再跑一遍才能清，把它兼给"等图表链"就会让状态行
在链已补齐之后仍报落后（一次点击要跑两轮才算完）。状态行报「等图表链」时用户跑一次「重算 Realm」
（scope = MSD | CSI | ChartDan），再点一次「计算成绩分析」就折进去了。

这样切开的理由：图表侧一行 MSD/CSI/Dan 是**谱面属性**，与玩家无关。玩家段顺手算一份，就会在
每个玩家、每次 compute 上重复同一张谱面的 MinaCalc 计算，而且写的是临时值——正是这轮性能问题的来源。

判定"该不该报缺"要对着图表链自己的候选门槛，不能只看有没有行，否则会给永不补的谱面反复排队：

| 情况 | 图表链会补吗 | 玩家段视为 |
|---|---|---|
| 行存在但版本旧 | 会（增量重算） | 缺失，排队 |
| 行不存在，MSD 未结算 | 会（同一轮里 MSD → CSI → Dan） | 缺失，排队 |
| 键数不在引擎 `4–18` 范围 | **不会**（候选期就被丢弃） | 已结算，不报缺 |
| 没有 beatmap set / 转谱图（mania 成绩挂在 non-mania 谱面） | **不会**（`collectManiaChartCandidates` 只收 `Ruleset == mania` 且有 set 的谱面） | 已结算，不报缺 |
| MSD 结算为 unrateable | **不会**（CSI 与 Dan 都显式跳过） | 已结算，不报缺 |
| CSI 结算为 unavailable stub | 不会 | 已结算，不报缺 |

前两类由 `EzChartChainCoverage.IsRateableChart` / `EzSkillStore.GetUnrateableMsdHashes` 区分：前者按图表链
自己的候选门槛（set 非空 + `Ruleset.OnlineID == 3` + 键数）判断，不只是看有没有行，否则会给链永不看的谱面
反复排队、把玩家的脏位永久挂住。后两者是**落结算行**的结果，所以"缺行"一定会终止：`EzBeatmapMsdComputer.ComputeAndStore` 把零向量、
超出引擎列数、内容不可加载都落成 `__unrateable`；唯一允许不落的是引擎自己抛异常的瞬态失败。

同一套口径也用在 `GetSkillDataStatus()` 上：不可评的键数不计入图表总数，MSD unrateable 的谱面在
CSI 与 Dan 一栏都记 `unrateable` 而不是 `missing`——即使它带着一张早先从 `__unrateable` 伪轴算出的
CSI 行（那种行由 BDSP 的修复集改写为 stub）。否则"全部最新"永远达不到，状态行会一直报警。

这条修复链有个前提：**读 `__unrateable` 标记要读原始行，不能经 `EzSkillProvider.GetBeatmapMsd`**。
那个读法为了让 CSI 计算拿不到伪轴，会把标记整行吞掉（返回空字典），于是 `IsUnrateableMsd(GetBeatmapMsd(...))`
恒为假 —— BDSP 的修复集每轮都重排同一批图（`ok=N, fail=0` 却什么都没写），热路径还会把 stub 轴重新算成
一张完整 CSI 行。两个判断都用 `EzSkillProvider.isSettledUnrateableMsd`（现读 `GetBeatmapSkills(hash, BEATMAP_MSD)`）。

## Key entry points

- `IEzMsdEngine` / `EzNKeyMsdEngine`: the difficulty engine abstraction and its 4–18K implementation.
- `EzBeatmapMsdComputer`: chart-side MSD compute and persistence.
- `EzChartSkillInfoComputer`: chart metadata, pattern tags, motion, LeoBlack cluster fallback.
- `EzChartDanEstimator`: chart dan calculation and live snapshot flow.
- `EzDanSkillsetFiling`: skillset bucketing and quorum logic.
- `EzPlayerSsrAggregator` / `EzPlayerDanAggregator`: player-side folds. Both take `EzSkillPlayRow` sets plus a
  resolver rather than scores, so a cached play folds straight from its cache row (no mods re-parse, no beatmap) and
  only the plays no cache answers are detached — in bounded windows, never a whole library.
- `EzLocalProfileService` (`LocalProfile/`): the SQLite slice + Realm skill writer; `IngestSettledScore`
  is the per-play hook, `ComputeAsync` is the one entry the manual compute drives (the startup /
  chart-chain reconcile was removed with the automatic follow-up).
- `EzChartChainDebt`: derives what the chart chain still owes a play, so neither the status readout nor
  the manual compute's default scope needs a stored flag for it.
- `EzLocalProfileComputeNotification` (`LocalProfile/`): the progress notification the manual compute
  uses, so a long run is never invisible.

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




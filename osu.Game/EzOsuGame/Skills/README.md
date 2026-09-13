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
- `EzManiaSkillAlgorithm.VERSION` gates the derived caches: bumping it clears MSD and, via
  `BackgroundDataStoreProcessor`, ChartSkillInfo + ChartDan as well.
- Chart dan routing: 4/6/7K use their own community xxy→dan interval tables (`EzSunnyDanIntervals`),
  with the calibrated MSD means table only as the no-xxy / dual-half fallback. No other keymode has
  a table, so it borrows the 4K table on the same side (`EzDanLabels.TryResolveFallbackDan`): the raw
  4K MSD means table is off-scale above 4K and inflated those labels by several levels, and the star
  rating is the only community-calibrated number those keymodes have. No star rating = no invented dan.
- `EzDanAlgorithm.VERSION` gates player clears / chart dan rows; bump it when the dan mapping
  changes (currently `4`) so old rows are filtered out rather than read as stale.
- Downstream coverage is not yet uniform above 9K — see `DATA-FOLLOWUPS.md`.

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
  `EzManiaSkillAlgorithm.VERSION`; derived caches (MSD → ChartSkillInfo → ChartDan) only
  fall if `clearOutdatedManiaBeatmapMsd` clears them together.
- When introducing new keycount support, update both the compute path and the filing / ladder consumers.
- The vendored wasm is single-threaded with mutable globals: one engine instance per thread,
  and never share a `Store`/`Instance` across concurrent computes.




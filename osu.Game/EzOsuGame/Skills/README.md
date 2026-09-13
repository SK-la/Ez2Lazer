# EzOsuGame Skills

This folder contains the skill computation pipeline used by Ez2Lazer's mania-related features.

## What lives here

- Beatmap MSD computation and caching:
  - `EzBeatmapMsdComputer`
  - `EzMinaCalcFacade`
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

- The current implementation is still rooted in `MinaCalc 0.4.2`.
- 4K uses the note-array path.
- 4K / 6K / 7K use the `.osu` text path when available.
- 5K and 8K+ are currently blocked by the engine limitation documented in `DATA-FOLLOWUPS.md`.
- Some downstream features are already hub-shaped, but not yet full 4–18K parity.

## Key entry points

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
- Prefer small, explicit helpers when changing engine selection or keycount routing.
- When introducing new keycount support, update both the compute path and the filing / ladder consumers.




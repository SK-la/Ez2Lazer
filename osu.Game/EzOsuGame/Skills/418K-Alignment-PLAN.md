# 4–18K Alignment Plan

Goal: move the current Ez skill pipeline toward mania-hub-style 4–18K support, while keeping the codebase stable and testable.

## Status

- **Phase 1 (abstraction)** — done: `IEzMsdEngine` + `EzCalcNote`; `SupportsKeyCount` is the
  single capability check; `.osu` file-hint plumbing is gone with the text path.
- **Phase 2 (compute)** — done: NuGet `MinaCalc 0.4.2` was replaced by the mania-hub n-key
  build (`minaclac-74.0.wasm`, Wasmtime host). MSD and SSR share one note-array path, so
  beatmap-side and play-side axes come from the same engine.
- **Phase 3 (downstream)** — partially done: `DATA-ChartDan-Keymode-Routing` is closed
  (5K / 8K–18K chart dan is star-fitted; the radar no longer draws the engine's 0.18 sliver).
  Still open: `DATA-PatternAxis-KeymodeSet` and `DATA-Skillset-Dan-Coverage-10K+` in
  `DATA-FOLLOWUPS.md`.
- **Phase 4 (regression coverage)** — engine-level done (4–18K routing, zero vector for
  unsupported keys, chord masks, negative-time shift, trap recovery); chart-dan /
  player-aggregation end-to-end coverage still open.
- **Phase 5 (extraction)** — not started, still optional.

## Scope

This plan focuses on the skill pipeline under `osu.Game/EzOsuGame/Skills`:

- beatmap MSD / SSR calculation
- chart analysis and chart-dan derivation
- player SSR aggregation
- dan skillset filing / buckets / ladders
- test coverage for supported keymodes

## Current blockers

1. The current calculation engine is `MinaCalc 0.4.2`.
2. That engine only supports:
   - 4K via note-array APIs
   - 4K / 6K / 7K via `.osu` text parsing
3. 5K and 8K+ are currently blocked in the live pipeline.
4. Several downstream consumers still encode old assumptions about supported keymodes.

## Recommended implementation strategy

### Phase 1 — Stabilize the abstraction layer

- Keep the game code calling a single internal skill engine interface.
- New interfaces introduced during this work must use the `IEz` prefix.
- Ensure all `.osu` text calls use a stable file hint convention.
- Centralize keycount capability checks in one place.
- Add tests for 4K, 6K, 7K, 8K, and 18K routing decisions.

### Phase 2 — Expand compute support inside `osu.Game`

- Replace the current limited engine path with a 4–18K-capable implementation.
- Update MSD and SSR compute paths together.
- Make sure beatmap-side and play-side paths return the same axis vocabulary.
- Keep existing persistence and filing contracts stable.

### Phase 3 — Align downstream consumers

- Update `EzChartSkillInfoComputer` so pattern / motion / LN metadata remains consistent across keymodes.
- Review `EzDanSkillsetFiling`, `EzDanSkillsetBuckets`, and `EzDanLadders` for keymode-specific assumptions.
- Decide which features are truly 4K-only and which should be generalized.

### Phase 4 — Add regression coverage

- Add test charts for representative keymodes.
- Verify non-zero results for supported keys.
- Verify unsupported or intentionally skipped keys fail cleanly.
- Add end-to-end tests for chart-side MSD, chart-dan, and player aggregation.

### Phase 5 — Optional later extraction

If the engine becomes stable and reusable, consider extracting the pure analysis core into a separate DLL or project.

This is optional. For the initial 4–18K alignment, keeping the logic inside `osu.Game` is usually the lower-risk path.

## Decision rule for architecture

Use the following rule of thumb:

- If the goal is fastest alignment with the least integration work: keep it in `osu.Game`.
- If the goal is long-term reuse across multiple products: extract a DLL later.

## Tracking

- Keep long-running follow-ups in `DATA-FOLLOWUPS.md`.
- Mark each phase complete only when code and tests are updated together.

## Suggested first implementation target

1. Introduce a shared skill-engine interface.
2. Route `EzBeatmapMsdComputer`, `EzChartDanEstimator`, and `EzPlayerSsrAggregator` through it.
3. Add tests proving the routing works before changing the heavy compute logic.


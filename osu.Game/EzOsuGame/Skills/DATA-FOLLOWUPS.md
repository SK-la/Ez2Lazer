# Skillset / Dan DATA follow-ups

Tracked so later PRs stay light. **EZ Realm frozen at 9** for this track — fill existing columns/tables only.

Do **not** re-encode lasting follow-ups as only `// TODO(data)` in product code — keep a row here. **Temp wires** must dual-write: code `TODO` pointing at a row ID **and** Status below (`Temp wire → …`).

| ID | Plan name | Scope | Status |
|----|-----------|--------|--------|
| — | SCHEMA EZ9 | Typed `EzBeatmapChartSkillInfo` + `EzPlayerDanSkillsetValue` in Realm | **Done** |
| — | Debt: SQLite ChartSkillInfo | Abandoned analysis `chart_skill_info` JSON; no import/migrate; miss → recompute | **Done** |
| — | Debt: single Realm skill facade | ChartSkillInfo CRUD merged into `EzSkillStore`; deleted `EzChartSkillInfoStore` | **Done** |
| — | DATA-Skillset-Cache | Writers + Provider cache-hit; LocalProfile rebuild calls `RefreshDanSkillsets` | **Done** |
| — | DATA-ChartSkillInfo-Batch | BDSP `populateMissingChartSkillInfo` (+ rebuild target); runs after MSD | **Done** |
| — | DATA-Dan-Headline-Anchor | Hub `anchoredSkillsetDans` / skillset mean → side `GetDan` via `EzDanSideHeadline` (dan algo v2); aggregator no longer writes clears-average GetDan | **Done** |
| — | MSD 6K/7K + no wipe-every-launch | FromString path; `RulesetInfo.Clone` + live LastAppliedManiaSkillVersion | **Done** |
| — | DATA-Skills-PatternRatings | Hub `aggregateModePatternRatings` + Track/HUD `skillModeEntries` (6/7/8K). Store via existing `EzPlayerSkillValue` (`SystemId=player_pattern`) — no schema bump. SSR path: stored ChartSkillInfo preferred; miss → in-memory `EzChartSkillInfoComputer` (no per-score Upsert). | **Done** |
| — | DATA-Skills-ValueFloor | Hub `skillModeEntries` `value >= 1` filter (Mina fallback + pattern axes) | **Done** (with PatternRatings) |
| 1 | DATA-LeoBlack-ChartDan | Full LeoBlack chart-side aggregate dan (optional). Also fills cluster columns if still null. | Wish-list |
| — | DATA-LeoBlack-Clusters | Fill `JackShare` / `StreamShare` / `TechCategory` / `ClusterTrill` / `HandstreamCluster`; revive filing branches that need them. Overlaps row 1. | Wish-list |
| — | DATA-SSR-VibroExclude | Hub `chart_vibro` / `rate_vibro` eviction from SSR pool (`Vibro` currently always false) | Wish-list |
| — | DATA-SSR-GoalExtrapolate | Hub `runMsdAtGoal` log-linear extrapolate past calc 0.965 | Wish-list |
| — | DATA-SSR-LnTailBlend | Hub `LN_TAIL_BLEND_BY_KEYMODE` (`EzMinaNoteConverter` lnTailTaps=false) | Wish-list |
| — | MinaCalc 5K / 8K+ MSD | Hub rates **4–18K** (vendored MinaCalc). Ez NuGet **0.4.2** only 4/6/7 (`FromString`) / 4K (note-array); backfill skips 5/8+. Align engine separately. | Blocked on engine upgrade |

UI read path (all of HUD DualPanel/Radar, Analysis Wedge, LocalProfile Track, display tags): **`EzSkillProvider` only**.

When starting any of the above, check this file and the matching Cursor/plan todos, then tick or remove the row here in the same PR.

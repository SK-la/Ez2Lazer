# Skillset / Dan DATA follow-ups

Tracked so later PRs stay light. Fill existing columns/tables when possible; bump EZ only for new persisted shapes (see ChartDan EZ10).

Do **not** re-encode lasting follow-ups as only `// TODO(data)` in product code — keep a row here. **Temp wires** must dual-write: code `TODO` pointing at a row ID **and** Status below (`Temp wire → …`).

| ID | Plan name | Scope | Status |
|----|-----------|--------|--------|
| — | SCHEMA EZ9 | Typed `EzBeatmapChartSkillInfo` + `EzPlayerDanSkillsetValue` in Realm | **Done** |
| — | SCHEMA EZ10 / DATA-ChartDan-Realm | Nomod `EzBeatmapChartDan` + skillset stamps; BDSP `populateMissingChartDan`; DualPanel/Provider song-select **zero engine** (no sync playable/Mina/LeoBlack); rate-mod chart dan stays out | **Done** |
| — | Debt: SQLite ChartSkillInfo | Abandoned analysis `chart_skill_info` JSON; no import/migrate; miss → recompute | **Done** |
| — | Debt: single Realm skill facade | ChartSkillInfo CRUD merged into `EzSkillStore`; deleted `EzChartSkillInfoStore` | **Done** |
| — | DATA-Skillset-Cache | Writers + Provider cache-hit; LocalProfile rebuild calls `RefreshDanSkillsets` | **Done** |
| — | DATA-ChartSkillInfo-Batch | BDSP `populateMissingChartSkillInfo` (+ rebuild target); runs after MSD | **Done** |
| — | DATA-Dan-Headline-Anchor | Hub `anchoredSkillsetDans` / skillset mean → side `GetDan` via `EzDanSideHeadline` (dan algo v2); aggregator no longer writes clears-average GetDan | **Done** |
| — | MSD 6K/7K + no wipe-every-launch | FromString path; `RulesetInfo.Clone` + live LastAppliedManiaSkillVersion | **Done** |
| — | DATA-Skills-PatternRatings | Hub `aggregateModePatternRatings` + Track/HUD `skillModeEntries` (6/7/8K). Store via existing `EzPlayerSkillValue` (`SystemId=player_pattern`) — no schema bump. SSR path: stored ChartSkillInfo preferred; miss → in-memory `EzChartSkillInfoComputer` (no per-score Upsert). | **Done** |
| — | DATA-Skills-ValueFloor | Hub `skillModeEntries` `value >= 1` filter (Mina fallback + pattern axes) | **Done** (with PatternRatings) |
| — | DATA-Dan-Skillset-PlaySsr | DualPanel 4K filing: hub `play.values` via `EzDanPlaySsrIndex` from `axis_play_evidence` (`player_ssr.*`), not beatmap MSD. CSI miss on Refresh: sync `TryGetOrCompute` (hub load+heal; no async job queue). Quorum 4 unchanged. | **Done** |
| — | DATA-Dan-Skillset-EmptySlots | User: DualPanel 4K jack / 6–7K speed empty; other tiles + radar OK. Local `All` clears×CSI: 4K jack-like SSR argmax≈1 / chordjack tags≈2; 6K&7K delay-tagged clears=1 each (CSI has 133 delay charts but almost none in clear set). primary≪4 → **legal quorum**, no filing code change. Detail: gitignored `artifacts/skills-evidence-map.md` Pass B–D. | **Done** (docs only) |
| — | DATA-Dan-Skillset-CsiQueue | Hub `enqueueMissingChartAnalyses` async next-pass; Ez currently sync-heals in Refresh. Optional queue if sync cost hurts rebuild. | Wish-list |
| 1 | DATA-LeoBlack-ChartDan | Full LeoBlack chart-side aggregate dan (optional). Also fills cluster columns if still null. | Wish-list |
| — | DATA-Dan-ClearWindow-v3 | Hub `weightedDanClearWindow` (window=20, family 0.9^rank, stray ignore); aggregator fail/EZ reject + (hash,rate) dedupe; `EzDanAlgorithm.VERSION=3`. SSR history = rolling Aggregate (not career prefix). Chart DualPanel labels = same aggregate on every hit bucket (Sunny priority matches headline). UI Speed label kept (hub display). | **Done** |
| — | DATA-No-Empty-Fillers | Ban skillset `__empty__` sentinel + CSI `Unavailable` persistence; miss stays miss so VERSION bump / transient load fail can recompute. DualPanel passes playable into chart labels. | **Done** |
| — | DATA-DualPanel-ChartSkillsetDans | Idea: DualPanel chart column shows independent per-skillset chart dans (MSD→`SrToRawDan` or LeoBlack), not hub aggregate stamps. Current UI is 3-col grid (label\|player\|chart) with hub filing stamps only. | Wish-list |
| — | DATA-SSR-VibroExclude | Hub `chart_vibro` / `rate_vibro` eviction from SSR pool (`Vibro` currently always false) | Wish-list |
| — | DATA-SSR-GoalExtrapolate | Hub `runMsdAtGoal` log-linear extrapolate past calc 0.965 | Wish-list |
| — | DATA-SSR-LnTailBlend | Hub `LN_TAIL_BLEND_BY_KEYMODE` (`EzMinaNoteConverter` lnTailTaps=false) | Wish-list |
| — | MinaCalc 5K / 8K+ MSD | Hub rates **4–18K** (vendored MinaCalc). Ez NuGet **0.4.2** only 4/6/7 (`FromString`) / 4K (note-array); backfill skips 5/8+. Align engine separately. | Blocked on engine upgrade |

UI read path (all of HUD DualPanel/Radar, Analysis Wedge, LocalProfile Track, display tags): **`EzSkillProvider` only**. Song-select DualPanel chart half: Realm ChartDan / MSD / CSI **read-only** (`GetChartDanSkillsetLabelsReadOnly`); cold miss stays empty until BDSP / 「Realm ChartDan」 maintenance.

When starting any of the above, check this file and the matching Cursor/plan todos, then tick or remove the row here in the same PR.

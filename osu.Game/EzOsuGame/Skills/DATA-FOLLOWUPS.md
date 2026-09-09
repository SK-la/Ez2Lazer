# Skillset / Dan DATA follow-ups

Tracked so later PRs stay light. **EZ Realm frozen at 9** for this track — fill existing columns/tables only.

Do **not** re-encode these as `// TODO(data)` in product code. Open a dedicated PR/plan per row.

| ID | Plan name | Scope | Status |
|----|-----------|--------|--------|
| — | SCHEMA EZ9 | Typed `EzBeatmapChartSkillInfo` + `EzPlayerDanSkillsetValue` in Realm | **Done** |
| — | Debt: SQLite ChartSkillInfo | Abandoned analysis `chart_skill_info` JSON; no import/migrate; miss → recompute | **Done** |
| — | Debt: single Realm skill facade | ChartSkillInfo CRUD merged into `EzSkillStore`; deleted `EzChartSkillInfoStore` | **Done** |
| — | DATA-Skillset-Cache | Writers + Provider cache-hit; LocalProfile rebuild calls `RefreshDanSkillsets` | **Done** |
| 1 | DATA-LeoBlack-ChartDan | Full LeoBlack chart-side aggregate dan (optional). Also fills cluster columns if still null. | Wish-list |
| 3 | DATA-Dan-Headline-Anchor | 7K LN `anchoredSkillsetDans`-style side headline (only if product moves titles off side-level `GetDan`). | Optional |
| 4 | DATA-ChartSkillInfo-Batch | Background / library-wide ChartSkillInfo warm (beyond on-demand DualPanel + clear hashes). | Optional |

UI read path (all of HUD DualPanel/Radar, Analysis Wedge, LocalProfile Track, display tags): **`EzSkillProvider` only**.

When starting any of the above, check this file and the matching Cursor/plan todos, then tick or remove the row here in the same PR.

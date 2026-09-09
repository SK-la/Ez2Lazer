# Skillset / Dan DATA follow-ups

Tracked so later PRs do not forget these. **EZ Realm is frozen at 9** for this track — fill existing columns/tables only.

Do **not** re-encode these as `// TODO(data)` in product code. Open a dedicated PR/plan per row.

| ID | Plan name | Scope | Status |
|----|-----------|--------|--------|
| — | SCHEMA EZ9 | Typed `EzBeatmapChartSkillInfo` + `EzPlayerDanSkillsetValue` in Realm | **Done in PR1** |
| 1 | DATA-LeoBlack-ChartDan | Full LeoBlack chart-side aggregate dan (optional; not required for maintainability). Also fills cluster columns if still null. | Wish-list; may never ship |
| 2 | DATA-Skillset-Cache | Writers for `EzPlayerDanSkillsetValue` so DualPanel / profile do not re-file every clear on read. Table already exists (EZ9). | Optional next |
| 3 | DATA-Dan-Headline-Anchor | 7K LN `anchoredSkillsetDans`-style side headline (only if product moves titles off side-level `GetDan`). | Optional |
| 4 | DATA-ChartSkillInfo-Batch | Background / library-wide ChartSkillInfo warm (beyond on-demand DualPanel + clear hashes). | Optional |

When starting any of the above, check this file and the matching Cursor/plan todos, then tick or remove the row here in the same PR.

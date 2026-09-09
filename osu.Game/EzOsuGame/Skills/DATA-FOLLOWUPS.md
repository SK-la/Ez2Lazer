# Skillset / Dan DATA follow-ups

Tracked with the Skillset Filing DATA PR so later PRs do not forget these.

Do **not** re-encode these as `// TODO(data)` in product code. Open a dedicated PR/plan per row.

| ID | Plan name | Scope |
|----|-----------|--------|
| 1 | DATA-LeoBlack-ChartDan | Full LeoBlack chart-side aggregate dan (replace/augment Sunny + `EzChartDanEstimator` heuristics). Also supplies LeoBlack cluster fields (`jackShare` / `streamShare` / `clusterCategory`) if still thin after filing PR. |
| 2 | DATA-Skillset-Cache | Persist skillset verdicts so DualPanel / profile do not re-file every clear on read. |
| 3 | DATA-Dan-Headline-Anchor | 7K LN `anchoredSkillsetDans`-style side headline (only if product moves titles off side-level `GetDan`). |
| 4 | DATA-ChartSkillInfo-Batch | Background / library-wide `EzChartSkillInfo` warm (beyond on-demand DualPanel + clear hashes). |

When starting any of the above, check this file and the matching Cursor/plan todos, then tick or remove the row here in the same PR.

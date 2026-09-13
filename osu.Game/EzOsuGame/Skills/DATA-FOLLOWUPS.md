# Skillset / Dan DATA follow-ups

Tracked so later PRs stay light. Fill existing columns/tables when possible; bump EZ only for new persisted shapes (see ChartDan EZ10).

Do **not** re-encode lasting follow-ups as only `// TODO(data)` in product code — keep a row here. **Temp wires** must dual-write: code `TODO` pointing at a row ID **and** Status below (`Temp wire → …`).

| ID | Plan name | Scope | Status |
|----|-----------|--------|--------|
| — | SCHEMA EZ9 | Typed `EzBeatmapChartSkillInfo` + `EzPlayerDanSkillsetValue` in Realm | **Done** |
| — | SCHEMA EZ10 / DATA-ChartDan-Realm | Nomod `EzBeatmapChartDan` + skillset stamps; BDSP after MSD; DualPanel: Realm → session memory compute (no Upsert); panel: Realm/session only; MSD `ComputeAndStore` also Upsert ChartDan | **Done** |
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
| — | DATA-SSR-LnTailBlend | Hub `LN_TAIL_BLEND_BY_KEYMODE` (`EzMinaNoteConverter` lnTailTaps=false). Per-keymode blend weights also unported; the converter still treats every keymode identically. | Wish-list |
| — | Engine unify 4–18K | Replaced NuGet `MinaCalc 0.4.2` (4K note-array / 4/6/7K `.osu` text) with the mania-hub n-key build `minaclac-74.0.wasm` hosted by Wasmtime (`IEzMsdEngine` / `EzNKeyMsdEngine`). One note-array path for 4–18K; `.osu`-text branch and every per-keymode gate removed; 5K/8K+ now backfill. `EzManiaSkillAlgorithm.VERSION` 1→2, and MSD version loss now also clears ChartSkillInfo + ChartDan. Values change — full recompute required. | **Done** |
| — | Engine 17/18-column chord trap | The vendored module aborts on a chord wider than 16 columns (verified at 17). `EzNKeyMsdEngine` discards the instance and rebuilds, and the callers skip that chart, so it costs a rebuild + a retried pass — not a crash. No mitigation planned unless real charts hit it. | Known limit |
| — | DATA-ChartDan-Keymode-Routing | ChartDan still assumes the 4/6/7 ladder; 5K and 8K+ charts now have MSD but their chart-dan routing / unsupported handling was not revisited. | Wish-list |
| — | DATA-PatternAxis-KeymodeSet | Pattern axes (hub `skillModeEntries`) were shaped for 6/7/8K; the narrower/wider keymode sets (5K, 9K, 10–18K) have not been re-derived. | Wish-list |
| — | DATA-Skillset-Dan-Coverage-10K+ | `tracked_skillset_key_counts = {4,5,6,7,8,9}`, so 10–18K produce MSD/SSR but stay out of DualPanel段位格, filing and ladders. | Wish-list |

UI read path (all of HUD DualPanel/Radar, Analysis Wedge, LocalProfile Track, display tags): **`EzSkillProvider` only**. Song-select DualPanel chart half: Realm ChartDan / MSD / CSI **read-only** (`GetChartDanSkillsetLabelsReadOnly`); cold miss stays empty until BDSP / 「Realm ChartDan」 maintenance.

Recompute after an `EzManiaSkillAlgorithm.VERSION` bump: data maintenance → 「Realm 谱面技能链 (MSD+CSI+Dan)」 (or let the startup BDSP backfill run), then Local Profile → Skills for player SSR. Old-version rows are filtered out, not migrated, so a skipped recompute reads as empty rather than stale.

When starting any of the above, check this file and the matching Cursor/plan todos, then tick or remove the row here in the same PR.

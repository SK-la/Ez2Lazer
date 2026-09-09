// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Localization
{
    public class EzSettingsProfile
    {
        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_COMPUTE =
            new EzLocalizationManager.EzLocalisableString("计算成绩分析", "Compute Score Analysis");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_COMPUTE_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "扫描本地成绩，按玩家名勾选后做成绩分析并写入共享个人统计存档（与当前登录名无关）。",
            "Scan local scores, pick player names, run score analysis, then write into the shared local profile archive (independent of the logged-in name).");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_IMPORT_HEADER =
            new EzLocalizationManager.EzLocalisableString("选择要导入的玩家名", "Select player names to import");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_IMPORT_BODY = new EzLocalizationManager.EzLocalisableString(
            "勾选要【重新分析】的玩家名：只覆盖这些名称对应的统计切片，其它已计算名称不受影响。打开「替换模式」会删除未勾选名称的旧切片。",
            "Check names to re-analyse: only those players’ stat slices are overwritten; other computed names stay. Enable replace mode to drop unchecked names’ old slices.");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_IMPORT_REPLACE =
            new EzLocalizationManager.EzLocalisableString(
                "替换模式：删除未勾选名称的旧统计（不保留）",
                "Replace mode: delete old stats for unchecked names");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_IMPORT_CONFIRM =
            new EzLocalizationManager.EzLocalisableString("开始分析", "Analyse");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_IMPORT_CANCEL =
            new EzLocalizationManager.EzLocalisableString("取消", "Cancel");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_COMPUTE_STARTED =
            new EzLocalizationManager.EzLocalisableString("正在计算成绩分析…", "Computing score analysis…");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_COMPUTE_PROGRESS =
            new EzLocalizationManager.EzLocalisableString("正在分析成绩… {0}/{1}", "Analysing scores… {0}/{1}");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_COMPUTE_SAVING =
            new EzLocalizationManager.EzLocalisableString("正在写入成绩分析…", "Saving score analysis…");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_COMPUTE_SKILLS =
            new EzLocalizationManager.EzLocalisableString("正在计算技能与段位… {0}/{1}", "Computing skills & dan… {0}/{1}");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_COMPUTE_DONE =
            new EzLocalizationManager.EzLocalisableString("成绩分析已更新。", "Score analysis updated.");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_COMPUTE_FAILED =
            new EzLocalizationManager.EzLocalisableString("成绩分析计算失败。", "Failed to compute score analysis.");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_NO_SCORES =
            new EzLocalizationManager.EzLocalisableString("未找到可导入的本地成绩。", "No local scores found to import.");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_NONE_SELECTED =
            new EzLocalizationManager.EzLocalisableString("请至少勾选一个玩家名。", "Select at least one player name.");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_SELECT_SCORE =
            new EzLocalizationManager.EzLocalisableString("在左侧选择一条成绩", "Select a score on the left");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_TITLE =
            new EzLocalizationManager.EzLocalisableString("本地个人主页", "Local Profile");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_DESCRIPTION =
            new EzLocalizationManager.EzLocalisableString("共享本地游玩统计", "Shared local play statistics");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_SHARED_HINT =
            new EzLocalizationManager.EzLocalisableString("本地统计（共享存档）", "Local stats (shared archive)");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_NEEDS_RECOMPUTE =
            new EzLocalizationManager.EzLocalisableString(
                "算法已更新，请到设置 → Ez → 实验性功能 重新计算成绩分析",
                "Stats logic updated — recompute Score Analysis under Settings → Ez → Experimental");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_BADGE =
            new EzLocalizationManager.EzLocalisableString("本地", "Local");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_ACCOUNT_BUTTON =
            new EzLocalizationManager.EzLocalisableString("账号", "Account");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_PANEL_BUTTON =
            new EzLocalizationManager.EzLocalisableString("本地档案", "Local Profile");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_SELECT_PLAYER =
            new EzLocalizationManager.EzLocalisableString("选择玩家", "Select player");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_EMPTY_HINT =
            new EzLocalizationManager.EzLocalisableString("暂无统计。请到 设置 → Ez → 实验性功能 中点击「计算成绩分析」。", "No stats yet. Open Settings → Ez → Experimental and run “Compute Score Analysis”.");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_SECTION_KEYS =
            new EzLocalizationManager.EzLocalisableString("按键与 KPS", "Keys & KPS");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_SECTION_PERFORMANCE =
            new EzLocalizationManager.EzLocalisableString("成绩表现", "Performance");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_SECTION_MANIA =
            new EzLocalizationManager.EzLocalisableString("Mania 键数 / 列", "Mania Keys / Columns");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_SECTION_STD =
            new EzLocalizationManager.EzLocalisableString("最擅长 AR / CS", "Best AR / CS");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_SECTION_GRADES =
            new EzLocalizationManager.EzLocalisableString("成绩评级", "Grades");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_SECTION_STARS =
            new EzLocalizationManager.EzLocalisableString("星级分布", "Star rating");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_SECTION_CAREER =
            new EzLocalizationManager.EzLocalisableString("生涯总览", "Career overview");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_SECTION_MODE_DATA =
            new EzLocalizationManager.EzLocalisableString("各模式统计", "Stats by mode");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_ANALYSIS_SYSTEM =
            new EzLocalizationManager.EzLocalisableString("分析系统", "Analysis");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_SECTION_TRACK_SKILLS =
            new EzLocalizationManager.EzLocalisableString("Track 技能", "Track Skills");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_TRACK_EMPTY =
            new EzLocalizationManager.EzLocalisableString(
                "暂无 Track 技能。请到设置重新计算成绩分析（All 与各玩家名会一并写入）。",
                "No Track skills yet. Recompute Score Analysis in Settings (All and each name are stored together).");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_TRACK_NEEDS_PLAYER =
            new EzLocalizationManager.EzLocalisableString(
                "暂无 Track 技能。请到设置重新计算成绩分析。",
                "No Track skills yet. Recompute Score Analysis in Settings.");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_SKILL_RATING =
            new EzLocalizationManager.EzLocalisableString("{0}K 技能评分", "{0}K skill rating");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_SKILL_PLAYS =
            new EzLocalizationManager.EzLocalisableString("{0} 局分析", "{0} analyzed plays");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_SKILL_PROVISIONAL =
            new EzLocalizationManager.EzLocalisableString("临时评分", "Provisional");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_SKILL_STALE =
            new EzLocalizationManager.EzLocalisableString("待更新", "Updating");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_SKILL_HISTORY =
            new EzLocalizationManager.EzLocalisableString("技能历史", "Skill History");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_SKILL_HISTORY_FOR =
            new EzLocalizationManager.EzLocalisableString("技能历史 · {0}", "Skill History · {0}");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_SKILL_HISTORY_EMPTY =
            new EzLocalizationManager.EzLocalisableString(
                "暂无历史点。重新计算成绩分析后会按游玩时间重建。",
                "No history points yet. Recompute Score Analysis to rebuild them from play dates.");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_AXIS_PLAYS_FOR =
            new EzLocalizationManager.EzLocalisableString("支撑成绩 · {0}", "Supporting plays · {0}");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_AXIS_PLAYS_EMPTY =
            new EzLocalizationManager.EzLocalisableString(
                "暂无该轴的支撑成绩。请重新计算成绩分析。",
                "No supporting plays for this axis yet. Recompute Score Analysis.");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_AXIS_TREND =
            new EzLocalizationManager.EzLocalisableString("趋势", "Trend");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_AXIS_UNKNOWN_MAP =
            new EzLocalizationManager.EzLocalisableString("未知谱面", "Unknown beatmap");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_DAN_CHIP_TITLE =
            new EzLocalizationManager.EzLocalisableString("段位（启发式）", "Dan (heuristic)");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_DAN_SIDE_RC =
            new EzLocalizationManager.EzLocalisableString("RC", "RC");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_DAN_SIDE_LN =
            new EzLocalizationManager.EzLocalisableString("LN", "LN");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_DAN_CHIP_HINT =
            new EzLocalizationManager.EzLocalisableString(
                "点击查看 Clear · 启发式非 LeoBlack",
                "Tap for Clear · heuristic, not LeoBlack");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_DAN_CLEARS_FOR =
            new EzLocalizationManager.EzLocalisableString("Clear · {0}", "Clear · {0}");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_DAN_CLEARS_EMPTY =
            new EzLocalizationManager.EzLocalisableString(
                "暂无该侧 Clear。请重新计算成绩分析。",
                "No Clear for this side yet. Recompute Score Analysis.");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_DAN_CELL_CLEARS =
            new EzLocalizationManager.EzLocalisableString("Clear", "Clear");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_DAN_HEURISTIC_HINT =
            new EzLocalizationManager.EzLocalisableString(
                "MSD→Dan 近似，非 LeoBlack",
                "MSD→Dan approx, not LeoBlack");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_SECTION_TRACK_INSIGHTS =
            new EzLocalizationManager.EzLocalisableString("成绩洞察", "Profile Insights");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_INSIGHTS_EMPTY =
            new EzLocalizationManager.EzLocalisableString(
                "暂无可用的 Mania 成绩洞察（需要已计算的 PP）。",
                "No mania profile insights yet (computed PP scores required).");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_INSIGHTS_KEY_SPLIT =
            new EzLocalizationManager.EzLocalisableString("键数分布", "Key Split");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_INSIGHTS_MOST_USED_MOD =
            new EzLocalizationManager.EzLocalisableString("最常用 Mod", "Most Used Mod");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_INSIGHTS_MEDIAN_BPM =
            new EzLocalizationManager.EzLocalisableString("中位 BPM", "Median BPM");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_INSIGHTS_PP_RANGE =
            new EzLocalizationManager.EzLocalisableString("PP 范围", "PP Range");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_INSIGHTS_KEY_PP =
            new EzLocalizationManager.EzLocalisableString("各键数加权 PP", "PP by Keymode");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_INSIGHTS_MOD_USAGE =
            new EzLocalizationManager.EzLocalisableString("Mod 使用", "Mod Usage");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_INSIGHTS_BPM_BREAKDOWN =
            new EzLocalizationManager.EzLocalisableString("BPM 明细", "BPM Breakdown");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_INSIGHTS_PP_DISTRIBUTION =
            new EzLocalizationManager.EzLocalisableString("PP 分布", "PP Distribution");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_INSIGHTS_PP_CUMULATIVE =
            new EzLocalizationManager.EzLocalisableString("累计（≥ 阈值）", "Cumulative (≥ threshold)");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_INSIGHTS_BPM_RANGE =
            new EzLocalizationManager.EzLocalisableString("范围 {0}–{1} BPM", "Range {0}–{1} BPM");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_INSIGHTS_CONVERTS_EXCLUDED =
            new EzLocalizationManager.EzLocalisableString(
                "已排除 {0} 条 convert（不计入键数 PP）",
                "{0} convert plays excluded from keymode PP");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_INSIGHTS_NO_MODS =
            new EzLocalizationManager.EzLocalisableString("窗口内成绩均为无 Mod。", "All window plays are nomod.");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_INSIGHTS_NEWEST_TOP =
            new EzLocalizationManager.EzLocalisableString("最新 Top Play", "Newest Top Play");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_INSIGHTS_OLDEST_TOP =
            new EzLocalizationManager.EzLocalisableString("最早 Top Play", "Oldest Top Play");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_XXY_PLAY_DISTRIBUTION =
            new EzLocalizationManager.EzLocalisableString("xxySR 分布", "Plays by xxySR");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_STAR_PLAY_LINE =
            new EzLocalizationManager.EzLocalisableString("星级游玩分布", "Plays by star rating");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_XXY_PLAY_LINE =
            new EzLocalizationManager.EzLocalisableString("xxySR 游玩分布", "xxySR distribution");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_SECTION_SCORE_DRILL =
            new EzLocalizationManager.EzLocalisableString("成绩记录", "Score history");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_SECTION_BEATMAP_PERF =
            new EzLocalizationManager.EzLocalisableString("同图对比", "On this beatmap");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_SECTION_SCORE_KEYS =
            new EzLocalizationManager.EzLocalisableString("本局数据", "This score");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_SECTION_TRENDS =
            new EzLocalizationManager.EzLocalisableString("命中趋势", "Hit trends");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_DRILL_SEARCH_PLACEHOLDER =
            new EzLocalizationManager.EzLocalisableString("按 PP、曲名或艺术家搜索", "Search by PP, title, or artist");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_DRILL_NO_MATCHES =
            new EzLocalizationManager.EzLocalisableString("没有符合条件的成绩", "No scores match your search");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_BEATMAP_PERF_PP =
            new EzLocalizationManager.EzLocalisableString("PP", "PP");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_BEATMAP_PERF_ACC =
            new EzLocalizationManager.EzLocalisableString("Acc", "Acc");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_BEATMAP_PERF_OFFSET =
            new EzLocalizationManager.EzLocalisableString("Offset", "Offset");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_TREND_OFFSET =
            new EzLocalizationManager.EzLocalisableString("击打偏移", "Hit offset");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_TREND_ACCURACY =
            new EzLocalizationManager.EzLocalisableString("准确率", "Accuracy");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_TREND_EMPTY =
            new EzLocalizationManager.EzLocalisableString("暂无趋势数据（需保留本地回放）", "No trend data yet (local replay required).");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_TOTAL_KEYS =
            new EzLocalizationManager.EzLocalisableString("按键总数", "Total keys");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_AVG_KPS =
            new EzLocalizationManager.EzLocalisableString("平均 KPS", "Avg KPS");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_MAX_KPS =
            new EzLocalizationManager.EzLocalisableString("最大 KPS", "Max KPS");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_SCORE_COUNT =
            new EzLocalizationManager.EzLocalisableString("成绩数", "Scores");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_TOTAL_PP =
            new EzLocalizationManager.EzLocalisableString("PP 合计", "Total PP");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_TOTAL_DURATION =
            new EzLocalizationManager.EzLocalisableString("游戏时长", "Play Time");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_BEST_AR =
            new EzLocalizationManager.EzLocalisableString("最擅长 AR", "Best AR");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_BEST_CS =
            new EzLocalizationManager.EzLocalisableString("最擅长 CS", "Best CS");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_NO_RULESET_DATA =
            new EzLocalizationManager.EzLocalisableString("此模式暂无数据。", "No data for this ruleset.");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_COL_HEADER =
            new EzLocalizationManager.EzLocalisableString("列", "Col");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_MANIA_PLAYS_BY_KEY =
            new EzLocalizationManager.EzLocalisableString("各键数游玩次数", "Plays by key count");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_MANIA_AVG_KPS_LINE =
            new EzLocalizationManager.EzLocalisableString("各键数平均 KPS", "Avg KPS by key count");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_ONLINE_PULL =
            new EzLocalizationManager.EzLocalisableString("从线上下载成绩或谱面（实验）", "Download Online Scores or Beatmaps (Experimental)");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_ONLINE_PULL_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "需 Online 登录。下拉模式过滤 BP / 玩过的图；每批 50（BP100=两次）。可选下载缺图并加入「BP」或「玩过的图」收藏夹。",
            "Requires Online login. Ruleset filters BP / most-played; batch size 50 (BP100 = two runs). Optionally download missing maps into the BP or most-played collection.");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_ONLINE_PULL_HEADER =
            new EzLocalizationManager.EzLocalisableString("从线上下载成绩或谱面", "Download Online Scores or Beatmaps");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_ONLINE_PULL_BODY = new EzLocalizationManager.EzLocalisableString(
            "模式对两种来源都生效。每批 50：BP 用 offset 0 与 50 各下载一次即可凑满约 100。玩过的图同样按起始 offset 推进。勾选下载缺图时，边下边加入对应收藏夹（仅「BP」「玩过的图」两个，不重复创建）。",
            "Ruleset applies to both sources. Batch size 50: run BP at offset 0 then 50 for ~100. Most-played advances the same way. With download enabled, maps are added to the matching collection as they finish (only “BP” / “玩过的图”, no duplicates).");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_ONLINE_PULL_RULESET =
            new EzLocalizationManager.EzLocalisableString("模式（过滤）", "Ruleset (filter)");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_ONLINE_PULL_KIND =
            new EzLocalizationManager.EzLocalisableString("来源", "Source");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_ONLINE_PULL_OFFSET_HINT =
            new EzLocalizationManager.EzLocalisableString(
                "当前来源+模式已存进度 offset = {0}（每批 {1}）。下方可改本次起始值。",
                "Stored offset for current source+ruleset = {0} (batch {1}). Edit the start value below.");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_ONLINE_PULL_OFFSET_INPUT =
            new EzLocalizationManager.EzLocalisableString("起始 offset（填 0 重置）", "Start offset (0 = reset)");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_ONLINE_PULL_INCLUDE_STATS =
            new EzLocalizationManager.EzLocalisableString(
                "缺图也写入个人统计（用 API 元数据，无 KPS；默认开）",
                "Also write profile stats without local map (API metadata, no KPS; on by default)");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_ONLINE_PULL_DOWNLOAD_MAPS =
            new EzLocalizationManager.EzLocalisableString(
                "下载缺失谱面并加入收藏夹（BP→「BP」/ 玩过的图→「玩过的图」，节流）",
                "Download missing maps into collection (BP→“BP” / most-played→“玩过的图”, throttled)");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_ONLINE_PULL_CONFIRM =
            new EzLocalizationManager.EzLocalisableString("开始下载", "Start Download");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_ONLINE_PULL_NEED_ONLINE =
            new EzLocalizationManager.EzLocalisableString("请先 Online 登录自己的账号（不能用本地账号模式）。", "Online login as yourself is required (not local-only mode).");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_ONLINE_PULL_BUSY =
            new EzLocalizationManager.EzLocalisableString("正在下载线上成绩…", "Downloading online scores…");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_ONLINE_PULL_DONE =
            new EzLocalizationManager.EzLocalisableString(
                "线上下载完成：候选 {0}，成绩导入 {1}，已有成绩 {2}，无回放 {3}，缺图成绩 {4}，失败 {5}，统计写入 {6}，下图 {7}，本地已有图 {8}，收藏夹新增 {9}。",
                "Online download done: candidates {0}, scores imported {1}, owned {2}, no replay {3}, missing map scores {4}, failed {5}, stats {6}, maps downloaded {7}, maps already local {8}, collection adds {9}.");

        public static readonly EzLocalizationManager.EzLocalisableString LOCAL_PROFILE_ONLINE_PULL_FAILED =
            new EzLocalizationManager.EzLocalisableString("线上成绩下载失败。", "Failed to download online scores.");
    }
}

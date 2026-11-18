// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Analysis;

namespace osu.Game.EzOsuGame.Localization
{
    /// <summary>
    /// Background backfill notifications. The text names the data being computed by its abbreviation (SR, PP, MSD,
    /// CSI), so the notification says what it is writing rather than which internal pass is running.
    /// </summary>
    public static class EzAnalysisNotificationStrings
    {
        public static readonly LocalisableString SUNNY_RUNNING = new EzLocalizationManager.EzLocalisableString("补算谱面 Sunny SR / PP", "Filling chart Sunny SR / PP");
        public static readonly LocalisableString SUNNY_COMPLETED = new EzLocalizationManager.EzLocalisableString("谱面 Sunny SR / PP 已更新", "beatmaps' Sunny SR / PP updated");

        public static readonly LocalisableString SUNNY_SKILL_AXES_RUNNING =
            new EzLocalizationManager.EzLocalisableString("补算谱面 Sunny 技能轴", "Filling chart Sunny skill axes");

        public static readonly LocalisableString SUNNY_SKILL_AXES_COMPLETED =
            new EzLocalizationManager.EzLocalisableString("谱面 Sunny 技能轴已更新", "beatmaps' Sunny skill axes updated");

        public static readonly LocalisableString KEYPATTERN_RUNNING =
            new EzLocalizationManager.EzLocalisableString("补算谱面 EKP", "Filling chart EKP");

        public static readonly LocalisableString KEYPATTERN_COMPLETED =
            new EzLocalizationManager.EzLocalisableString("谱面 EKP 已更新", "beatmaps' EKP updated");

        public static readonly LocalisableString OFFICIAL_RUNNING = new EzLocalizationManager.EzLocalisableString("补算原版 SR / PP", "Filling original SR / PP");
        public static readonly LocalisableString OFFICIAL_COMPLETED = new EzLocalizationManager.EzLocalisableString("原版 SR / PP 已更新", "beatmaps' original SR / PP updated");

        public static readonly LocalisableString BASELINE_RUNNING = new EzLocalizationManager.EzLocalisableString(
            "补算 Sunny PP、Mina SR", "Filling Sunny PP and Mina SR");

        public static readonly LocalisableString BASELINE_COMPLETED = new EzLocalizationManager.EzLocalisableString(
            "Sunny PP / Mina SR 已更新", "beatmaps' Sunny PP / Mina SR updated");

        public static readonly LocalisableString TAGS_RUNNING = new EzLocalizationManager.EzLocalisableString("补算谱面标签（Tag）", "Filling chart tags");
        public static readonly LocalisableString TAGS_COMPLETED = new EzLocalizationManager.EzLocalisableString("谱面标签（Tag）已更新", "beatmaps' tags updated");

        public static readonly LocalisableString SCORES_RUNNING = new EzLocalizationManager.EzLocalisableString("重算本地成绩", "Recalculating local scores");
        public static readonly LocalisableString SCORES_COMPLETED = new EzLocalizationManager.EzLocalisableString("本地成绩已重算", "local scores recalculated");

        public static readonly LocalisableString MAINTENANCE_PREP_RUNNING = new EzLocalizationManager.EzLocalisableString(
            "数据维护：扫描谱面库…", "Data maintenance: scanning beatmaps…");

        public static readonly LocalisableString MAINTENANCE_PREP_COMPLETED = new EzLocalizationManager.EzLocalisableString(
            "数据维护完成", "Data maintenance finished");

        public static readonly LocalisableString MAINTENANCE_NOTHING_PENDING = new EzLocalizationManager.EzLocalisableString(
            "所选项目无待补算", "Nothing pending for the selected maintenance item(s)");

        /// <summary>L2：选歌分析 SQLite 主库 NoMod 切片（KPS/KPC 等），不是 Realm 谱面链。</summary>
        public static readonly LocalisableString SQLITE_MAIN_SLICE_RUNNING = new EzLocalizationManager.EzLocalisableString(
            "补算选歌分析切片（KPS/KPC）", "Filling song-select analysis slice (KPS/KPC)");

        public static readonly LocalisableString SQLITE_MAIN_SLICE_COMPLETED = new EzLocalizationManager.EzLocalisableString(
            "选歌分析切片已更新", "Song-select analysis slices updated");

        public static LocalisableString SqliteMainSliceQueuedWithBacklog(int total, int remaining)
            => new EzLocalizationManager.EzLocalisableString(
                $"已排队 {total} 张谱的选歌分析切片（KPS/KPC），后台仍在处理 {remaining} 张",
                $"Queued song-select analysis slices (KPS/KPC) for {total} beatmaps; {remaining} still in the backlog.");

        /// <summary>L2：分支曲库 Mod 快照，依赖主库切片。</summary>
        public static LocalisableString SqliteBranchesQueued(int branchCount, int beatmapEstimate)
            => new EzLocalizationManager.EzLocalisableString(
                $"补算分支曲库快照（{branchCount} 个分支，约 {beatmapEstimate} 张谱）",
                $"Refreshing songs-branch snapshots ({branchCount} branches, ~{beatmapEstimate} beatmaps)");

        public static readonly LocalisableString SQLITE_BRANCHES_SIMPLE_QUEUED = new EzLocalizationManager.EzLocalisableString(
            "已排队分支曲库快照刷新", "Songs-branch snapshot refresh queued");

        public static readonly LocalisableString SQLITE_BRANCHES_COMPLETED = new EzLocalizationManager.EzLocalisableString(
            "分支曲库快照已更新", "Songs-branch snapshots updated");

        public static LocalisableString SqliteBranchesRefreshing(string branchName, int processedBeatmaps, int branchTotal, int branchIndex, int branchCount)
            => new EzLocalizationManager.EzLocalisableString(
                $"分支「{branchName}」快照（{processedBeatmaps} / {branchTotal}）— 分支 {branchIndex} / {branchCount}",
                $"Branch \"{branchName}\" snapshot ({processedBeatmaps} / {branchTotal}) — branch {branchIndex} / {branchCount}");

        /// <summary>Chart chain, naming the stages that are actually running.</summary>
        public static LocalisableString ChartChainRunning(bool msd, bool csi, bool dan, bool ppp)
            => new EzLocalizationManager.EzLocalisableString(
                $"补算谱面链（{joinStages(true, msd, csi, dan, ppp)}）",
                $"Filling chart chain ({joinStages(false, msd, csi, dan, ppp)})");

        /// <inheritdoc cref="ChartChainRunning"/>
        public static LocalisableString ChartChainCompleted(bool msd, bool csi, bool dan, bool ppp)
            => new EzLocalizationManager.EzLocalisableString(
                $"谱面链（{joinStages(true, msd, csi, dan, ppp)}）已更新",
                $"chart chain ({joinStages(false, msd, csi, dan, ppp)}) updated");

        /// <summary>Progress line, e.g. <c>812 / 900</c>.</summary>
        public static LocalisableString Progress(int processed, int total)
            => new EzLocalizationManager.EzLocalisableString($"{processed} / {total}", $"{processed} / {total}");

        /// <summary>Count prefix for a completed line.</summary>
        public static LocalisableString CompletedCount(int count)
            => new EzLocalizationManager.EzLocalisableString($"完成 {count}：", $"{count}: ");

        /// <summary>Failures appended to a cancelled line.</summary>
        public static LocalisableString FailedCount(int count)
            => new EzLocalizationManager.EzLocalisableString($"（{count} 项失败，详见日志）", $" Check logs for issues with {count} failed items.");

        /// <summary>
        /// The stage abbreviations in dependency order. MSD is the Mina axis set; PP+ carries its own axes.
        /// </summary>
        private static string joinStages(bool chinese, bool msd, bool csi, bool dan, bool ppp)
        {
            var stages = new List<string>(4);

            if (msd)
                stages.Add("MSD");

            if (csi)
                stages.Add("CSI");

            if (dan)
                stages.Add(chinese ? "段位" : "Dan");

            if (ppp)
                stages.Add("PP+");

            return string.Join(" / ", stages);
        }

        /// <summary>The abbreviation of one system's data, for logs and single-stage notifications.</summary>
        public static string DataAbbreviation(EzAnalysisSystem system) => system switch
        {
            EzAnalysisSystem.Official => "SR / PP",
            EzAnalysisSystem.SSR => "Sunny SR",
            EzAnalysisSystem.PPP => "PP+",
            EzAnalysisSystem.Mina => "MSD",
            EzAnalysisSystem.CSI => "CSI",
            EzAnalysisSystem.Dan => "Dan",
            EzAnalysisSystem.KeyPattern => "EKP",
            _ => system.ToString(),
        };
    }
}

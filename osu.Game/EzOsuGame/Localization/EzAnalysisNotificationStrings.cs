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
        public static readonly LocalisableString SUNNY_RUNNING = new EzLocalizationManager.EzLocalisableString("补算谱面 Sunny SR", "Filling chart Sunny SR");
        public static readonly LocalisableString SUNNY_COMPLETED = new EzLocalizationManager.EzLocalisableString("谱面 Sunny SR 已更新", "beatmaps' Sunny SR updated");

        public static readonly LocalisableString OFFICIAL_RUNNING = new EzLocalizationManager.EzLocalisableString("补算官方 SR / PP", "Filling official SR / PP");
        public static readonly LocalisableString OFFICIAL_COMPLETED = new EzLocalizationManager.EzLocalisableString("官方 SR / PP 已更新", "beatmaps' official SR / PP updated");

        public static readonly LocalisableString BASELINE_RUNNING = new EzLocalizationManager.EzLocalisableString(
            "补算 Sunny PP、Mina SR", "Filling Sunny PP and Mina SR");

        public static readonly LocalisableString BASELINE_COMPLETED = new EzLocalizationManager.EzLocalisableString(
            "Sunny PP / Mina SR 已更新", "beatmaps' Sunny PP / Mina SR updated");

        public static readonly LocalisableString TAGS_RUNNING = new EzLocalizationManager.EzLocalisableString("补算谱面标签（Tag）", "Filling chart tags");
        public static readonly LocalisableString TAGS_COMPLETED = new EzLocalizationManager.EzLocalisableString("谱面标签（Tag）已更新", "beatmaps' tags updated");

        public static readonly LocalisableString SCORES_RUNNING = new EzLocalizationManager.EzLocalisableString("重算本地成绩", "Recalculating local scores");
        public static readonly LocalisableString SCORES_COMPLETED = new EzLocalizationManager.EzLocalisableString("本地成绩已重算", "local scores recalculated");

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
            _ => system.ToString(),
        };
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.EzOsuGame.Skills.Dan;
using osu.Game.EzOsuGame.Skills.SkillTag;

namespace osu.Game.EzOsuGame.Analysis
{
    /// <summary>
    /// 通用分析概要。
    /// 当前仅包含所有规则集都可消费的 KPS 相关数据。
    /// </summary>
    public readonly record struct KpsSummary
    {
        private static readonly IReadOnlyList<double> empty_kps_list = Array.Empty<double>();
        private readonly IReadOnlyList<double>? kpsList;

        public double AverageKps { get; }

        public double MaxKps { get; }

        public IReadOnlyList<double> KpsList => kpsList ?? empty_kps_list;

        public KpsSummary(double averageKps, double maxKps, IReadOnlyList<double>? kpsList)
        {
            AverageKps = averageKps;
            MaxKps = maxKps;
            this.kpsList = kpsList ?? empty_kps_list;
        }
    }

    /// <summary>
    /// Mania-specific analysis summary for Mania (column-based) beatmaps.
    ///
    /// Contract (unified):
    /// - <see cref="ColumnCounts"/> contains the total number of hit objects in each column, including long notes (LNs).
    /// - <see cref="HoldNoteCounts"/> contains the number of long notes (LNs) in each column.
    ///
    /// Invariants:
    /// - For any column index c: 0 &lt;= HoldNoteCounts[c] &lt;= ColumnCounts[c].
    /// - Regular (non-LN) count for column c can be derived as ColumnCounts[c] - HoldNoteCounts[c].
    ///
    /// Producers which populate these dictionaries must follow the above contract so that UI code can
    /// consistently render bar heights as: totalHeight = ColumnCounts[c] / max(ColumnCounts).
    /// </summary>
    public readonly record struct EzManiaSummary
    {
        private static readonly Dictionary<int, int> empty_counts = new Dictionary<int, int>();
        private readonly Dictionary<int, int>? columnCounts;
        private readonly Dictionary<int, int>? holdNoteCounts;

        public static readonly EzManiaSummary EMPTY = new EzManiaSummary(null, null, null);

        public double? SunnySR { get; }

        // TODO: 仅预留字段。当前阶段不参与计算、不参与持久化版本升级，也不接入 UI / 本地化。
        public double? SunnySRFullLN4 { get; }

        public double? SunnySRFullLN8 { get; }

        /// <summary>
        /// Total number of hit objects in each column (includes both regular notes and LNs).
        /// Key: column index; Value: total hit object count for that column.
        /// </summary>
        public Dictionary<int, int> ColumnCounts => columnCounts ?? empty_counts;

        /// <summary>
        /// Number of long notes (LNs) in each column.
        /// Key: column index; Value: LN count for that column.
        /// </summary>
        public Dictionary<int, int> HoldNoteCounts => holdNoteCounts ?? empty_counts;

        /// <summary>
        /// Whether <see cref="HoldNoteCounts"/> were supplied by the producer.
        /// When false, consumers should not treat an empty hold map as "zero LNs".
        /// </summary>
        public bool HasHoldNoteCounts => holdNoteCounts != null;

        public EzManiaSummary(Dictionary<int, int>? columnCounts, Dictionary<int, int>? holdNoteCounts, double? sunnySR,
                              double? sunnySRFullLN4 = null, double? sunnySRFullLN8 = null)
        {
            this.columnCounts = columnCounts;
            this.holdNoteCounts = holdNoteCounts;
            SunnySR = sunnySR;
            SunnySRFullLN4 = sunnySRFullLN4;
            SunnySRFullLN8 = sunnySRFullLN8;
        }
    }

    /// <summary>
    /// 轻量标签概要。
    /// </summary>
    public readonly record struct EzBeatmapTagSummary
    {
        public static readonly EzBeatmapTagSummary EMPTY = new EzBeatmapTagSummary(false, false);

        public bool HasVideo { get; }

        public bool HasStoryboard { get; }

        public bool HasAnyTag => HasVideo || HasStoryboard;

        public EzBeatmapTagSummary(bool hasVideo, bool hasStoryboard)
        {
            HasVideo = hasVideo;
            HasStoryboard = hasStoryboard;
        }
    }

    public readonly record struct EzAnalysisResult
    {
        private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>> no_axes
            = new Dictionary<string, IReadOnlyDictionary<string, double>>();

        private static readonly IReadOnlyDictionary<string, double> no_axis_values
            = new Dictionary<string, double>(StringComparer.Ordinal);

        public readonly double AverageKps;
        public readonly double MaxKps;
        public readonly KpsSummary? CommonSummary;
        public readonly EzManiaSummary? ManiaSummary;
        public readonly EzBeatmapTagSummary? TagSummary;

        private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>>? axes;

        /// <summary>
        /// Axis values grouped by the channel that emitted them, keyed by bare axis id.
        /// </summary>
        /// <remarks>
        /// A channel missing from this table did not run for this chart; one present with no entries ran
        /// and had nothing to say. Never null: a <c>default</c> result (a bindable's pre-computation value) has no
        /// table at all, and consumers must read "no axes" rather than crash on it.
        /// </remarks>
        public IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>> Axes => axes ?? no_axes;

        /// <summary>Dan estimates for the sides this chart can be rated on.</summary>
        public readonly EzDanSet? Dan;

        /// <summary>
        /// Ruleset-declared radar payload for the current mods, or null when the ruleset provides none.
        /// </summary>
        public readonly EzRulesetSpecificRadarResult? Radar;

        /// <summary>
        /// Typed structure summary for the same pass that produced <see cref="Axes"/>' CSI values, or null when the
        /// ruleset has no structure analysis. Consumers read this instead of running the structure pass again.
        /// </summary>
        public readonly EzChartSkillInfo? ChartSkillInfo;

        public EzAnalysisResult(KpsSummary commonSummary, EzManiaSummary? maniaSummary = null, EzBeatmapTagSummary? tagSummary = null,
                                IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>>? axes = null,
                                EzDanSet? dan = null, EzRulesetSpecificRadarResult? radar = null,
                                EzChartSkillInfo? chartSkillInfo = null)
        {
            AverageKps = double.IsFinite(commonSummary.AverageKps) ? commonSummary.AverageKps : 0;
            MaxKps = double.IsFinite(commonSummary.MaxKps) ? commonSummary.MaxKps : 0;
            CommonSummary = commonSummary;
            ManiaSummary = maniaSummary;
            TagSummary = tagSummary;
            this.axes = axes;
            Dan = dan;
            Radar = radar;
            ChartSkillInfo = chartSkillInfo;
        }

        public IReadOnlyList<double> KpsList => CommonSummary?.KpsList ?? Array.Empty<double>();

        /// <summary>Axis values of one channel, empty when it did not run.</summary>
        public IReadOnlyDictionary<string, double> AxesOf(string channel) => Axes.GetValueOrDefault(channel, no_axis_values);

        /// <summary>Axis values of the channel a stored skill namespace is read back under.</summary>
        public IReadOnlyDictionary<string, double> AxesOfSystem(string systemId) => AxesOf(EzAnalysisChannels.ChannelOfSystem(systemId));

        public EzAnalysisResult WithTagSummary(EzBeatmapTagSummary? tagSummary) => CommonSummary is KpsSummary commonSummary
            ? new EzAnalysisResult(commonSummary, ManiaSummary, tagSummary, Axes, Dan, Radar, ChartSkillInfo)
            : this;
    }
}

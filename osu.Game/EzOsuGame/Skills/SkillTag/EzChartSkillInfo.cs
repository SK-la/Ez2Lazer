// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Skills.SkillTag
{
    /// <summary>
    /// 谱面技能 Tag 产品（CSI / ChartSkillInfo）。
    /// <see cref="SkillTags"/> 是从轴分抽出的标签；其余字段是同一次 filing 的结构量。
    /// Persisted as typed Realm <see cref="EzBeatmapChartSkillInfo"/> (EZ≥9).
    /// </summary>
    public sealed class EzChartSkillInfo
    {
        /// <summary>
        /// Unavailable stub (<c>KeyCount = -1</c>). Never written by <see cref="EzChartSkillStore.UpsertChartSkillInfo"/>
        /// (which rejects it) - only by the explicit <see cref="EzChartSkillStore.WriteChartSkillInfoUnavailable"/>
        /// settle path, and only from the BDSP backfill, so a transient hot-path miss cannot retire a chart.
        /// Legacy rows with a negative <see cref="KeyCount"/> read back as the same stub.
        /// </summary>
        public static EzChartSkillInfo Unavailable { get; } = new EzChartSkillInfo
        {
            KeyCount = -1,
            DanEligible = false,
        };

        /// <summary>True when this is an unavailable stub (see <see cref="Unavailable"/>).</summary>
        public bool IsUnavailable => KeyCount is < 0;

        /// <summary>抽出的谱面技能 Tag id，不是 KeyPattern 轴。</summary>
        public string[] SkillTags { get; init; } = [];

        /// <summary>Structurally detected 4K quadstream/minijack/jack-marathon demand.</summary>
        public bool JackDemand { get; init; }

        public double? JackShare { get; init; }

        public double? StreamShare { get; init; }

        public bool? TechCategory { get; init; }

        public bool? ClusterTrill { get; init; }

        public bool? HandstreamCluster { get; init; }

        public double TechScore { get; init; }

        public double ChordjackScore { get; init; }

        /// <summary>Pattern analyzer jack score; null when unset.</summary>
        public double? JackScore { get; init; }

        public EzMotionFeatures? Motion { get; init; }

        public double? LnRatio { get; init; }

        public bool Vibro { get; init; }

        /// <summary>False when object structure makes dan evidence unsafe.</summary>
        public bool DanEligible { get; init; } = true;

        public double? LengthSeconds { get; init; }

        public int? KeyCount { get; init; }
    }
}

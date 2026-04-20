// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;

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
    /// mania 专属分析概要。
    /// </summary>
    public readonly record struct EzManiaSummary
    {
        private static readonly Dictionary<int, int> empty_counts = new Dictionary<int, int>();
        private readonly Dictionary<int, int>? columnCounts;
        private readonly Dictionary<int, int>? holdNoteCounts;

        public static readonly EzManiaSummary EMPTY = new EzManiaSummary(null, null, null);

        public double? XxySr { get; }

        // TODO: 仅预留字段。当前阶段不参与计算、不参与持久化版本升级，也不接入 UI / 本地化。
        public double? XxySrFullLN4 { get; }

        public double? XxySrFullLN8 { get; }

        public Dictionary<int, int> ColumnCounts => columnCounts ?? empty_counts;

        public Dictionary<int, int> HoldNoteCounts => holdNoteCounts ?? empty_counts;

        public EzManiaSummary(Dictionary<int, int>? columnCounts, Dictionary<int, int>? holdNoteCounts, double? xxySr,
                              double? xxySrFullLN4 = null, double? xxySrFullLN8 = null)
        {
            this.columnCounts = columnCounts;
            this.holdNoteCounts = holdNoteCounts;
            XxySr = xxySr;
            XxySrFullLN4 = xxySrFullLN4;
            XxySrFullLN8 = xxySrFullLN8;
        }
    }

    public readonly record struct EzAnalysisResult
    {
        public readonly double AverageKps;
        public readonly double MaxKps;
        public readonly KpsSummary? CommonSummary;
        public readonly EzManiaSummary? ManiaSummary;

        public EzAnalysisResult(KpsSummary commonSummary, EzManiaSummary? maniaSummary = null)
        {
            AverageKps = double.IsFinite(commonSummary.AverageKps) ? commonSummary.AverageKps : 0;
            MaxKps = double.IsFinite(commonSummary.MaxKps) ? commonSummary.MaxKps : 0;
            CommonSummary = commonSummary;
            ManiaSummary = maniaSummary;
        }

        public IReadOnlyList<double> KpsList => CommonSummary?.KpsList ?? Array.Empty<double>();
    }
}

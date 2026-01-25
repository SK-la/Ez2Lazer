// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;

namespace osu.Game.LAsEzExtensions.Analysis
{
    /// <summary>
    /// 轻量级的 KPS 概要，用于 UI 快速展示/下采样。
    /// </summary>
    public readonly struct KpsSummary
    {
        public readonly double AverageKps;
        public readonly double MaxKps;
        public readonly List<double> KpsList;

        public KpsSummary(double averageKps, double maxKps, List<double> kpsList)
        {
            AverageKps = averageKps;
            MaxKps = maxKps;
            KpsList = kpsList;
        }
    }

    /// <summary>
    /// 更详细的 mania 统计信息：列统计、长按统计、scratch 描述与可选的 xxy_sr。
    /// </summary>
    public readonly struct ManiaDetails
    {
        public readonly Dictionary<int, int> ColumnCounts;
        public readonly Dictionary<int, int> HoldNoteCounts;
        public readonly double? XxySr;

        public ManiaDetails(Dictionary<int, int> columnCounts, Dictionary<int, int> holdNoteCounts, double? xxySr)
        {
            ColumnCounts = columnCounts;
            HoldNoteCounts = holdNoteCounts;
            XxySr = xxySr;
        }
    }

    /// <summary>
    /// 综合分析结果，包含概要与详细两部分。UI 应默认使用 `Summary`，只有在需要时再访问 `Details`。
    /// </summary>
    public readonly struct EzAnalysisResult
    {
        public readonly KpsSummary Summary;
        public readonly ManiaDetails Details;

        public EzAnalysisResult(KpsSummary summary, ManiaDetails details)
        {
            Summary = summary;
            Details = details;
        }
    }
}

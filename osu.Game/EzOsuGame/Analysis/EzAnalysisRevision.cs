// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Analysis
{
    /// <summary>
    /// The single revision stamped on every analysis row.
    /// </summary>
    /// <remarks>
    /// One number for the whole analysis layer, chart side and player side alike: a stored row is current only
    /// when its stamp equals <see cref="VERSION"/>. Bumping it therefore retires everything and the ordinary
    /// incremental backfill recomputes on the next pass, with no per-system cascade to reason about.
    /// </remarks>
    public static class EzAnalysisRevision
    {
        /// <summary>
        /// Bump when any analysis algorithm, note conversion or rating scale changes.
        /// 4: dan reads the 4K interval table above 4K; CSI adds LeoBlack clusters.
        /// </summary>
        public const int VERSION = 4;

        /// <summary>Revision stamped on the chart-side MSD axis rows.</summary>
        public static int Msd => VERSION;

        /// <summary>Revision stamped on <c>EzBeatmapChartSkillInfo.InfoVersion</c>.</summary>
        public static int ChartSkillInfo => VERSION;

        /// <summary>Revision stamped on <c>EzBeatmapChartDan.AlgorithmVersion</c>.</summary>
        public static int ChartDan => VERSION;

        /// <summary>Revision stamped on the PPP chart and player rows.</summary>
        public static int Ppp => VERSION;

        /// <summary>Human-readable breakdown of the CSI revision.</summary>
        public static string DescribeChartSkillInfo() => $"v{VERSION}";

        /// <summary>Human-readable breakdown of the dan revision.</summary>
        public static string DescribeChartDan() => $"v{VERSION}";
    }
}

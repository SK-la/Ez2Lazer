// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Column hold (LN) measures over a chart.
    /// </summary>
    /// <remarks>
    /// These are hitobject ratios derived from the object counts, not analysis results.
    /// </remarks>
    public static class EzHoldMetrics
    {
        /// <summary>Hold objects over column objects on <paramref name="playable"/>. 0 for an empty chart.</summary>
        public static double ComputeHoldRatio(IBeatmap playable)
        {
            var (holds, total) = countHoldAndTotal(playable);
            return total <= 0 ? 0 : (double)holds / total;
        }

        /// <summary>
        /// Hold ratio derived from the beatmap object counts. Returns <see cref="double.NaN"/> when those counts
        /// are not stamped yet.
        /// </summary>
        public static double HoldRatio(BeatmapInfo beatmapInfo)
        {
            ArgumentNullException.ThrowIfNull(beatmapInfo);

            int total = beatmapInfo.TotalObjectCount;
            int holds = beatmapInfo.EndTimeObjectCount;

            if (total <= 0 || holds < 0)
                return double.NaN;

            return Math.Clamp((double)holds / total, 0, 1);
        }

        /// <summary>
        /// Stored object counts when usable, otherwise the playable's own counts.
        /// </summary>
        public static double HoldRatioOr(IBeatmap playable, BeatmapInfo beatmapInfo)
        {
            double stored = HoldRatio(beatmapInfo);

            return double.IsNaN(stored) ? ComputeHoldRatio(playable) : stored;
        }

        /// <summary>Column hold (LN) object count on <paramref name="playable"/>.</summary>
        public static int ComputeHoldCount(IBeatmap playable) => countHoldAndTotal(playable).Holds;

        /// <summary>Hold count already stamped on the beatmap, or -1 when unset.</summary>
        public static int TryHoldCountFromBeatmapInfo(BeatmapInfo beatmapInfo) => beatmapInfo.EndTimeObjectCount >= 0 ? beatmapInfo.EndTimeObjectCount : -1;

        /// <summary>
        /// Column count of a playable chart: rounded circle size when it is a sane column count, otherwise
        /// one past the highest column, otherwise 4.
        /// </summary>
        public static int ResolveKeyCount(IBeatmap beatmap)
        {
            ArgumentNullException.ThrowIfNull(beatmap);

            int fromCs = (int)Math.Round(beatmap.Difficulty.CircleSize);
            if (fromCs is >= 1 and <= 18)
                return fromCs;

            int maxColumn = -1;

            foreach (HitObject obj in beatmap.HitObjects)
            {
                if (obj is IHasColumn columnObj)
                    maxColumn = Math.Max(maxColumn, columnObj.Column);
            }

            return maxColumn >= 0 ? maxColumn + 1 : 4;
        }

        /// <summary>
        /// Column count for a chart that is not loaded: the difficulty's circle size when it is a sane column
        /// count, otherwise 4.
        /// </summary>
        /// <remarks>
        /// The single fallback chain for callers without a playable. Deriving it per call site is how one chart
        /// ended up filed under a different keymode slice than the one it was measured on.
        /// </remarks>
        public static int ResolveKeyCount(BeatmapInfo beatmapInfo)
        {
            ArgumentNullException.ThrowIfNull(beatmapInfo);

            int fromCs = (int)Math.Round(beatmapInfo.Difficulty.CircleSize);
            return fromCs is >= 1 and <= 18 ? fromCs : 4;
        }

        private static (int Holds, int Total) countHoldAndTotal(IBeatmap playable)
        {
            ArgumentNullException.ThrowIfNull(playable);

            int total = 0;
            int holds = 0;

            foreach (HitObject obj in playable.HitObjects)
            {
                if (obj is not IHasColumn)
                    continue;

                total++;

                if (obj is IHasDuration duration && duration.Duration > 0)
                    holds++;
            }

            return (holds, total);
        }
    }
}

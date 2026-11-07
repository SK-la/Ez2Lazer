// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Column hold (LN) measures over a playable beatmap. Shared by the Mina axis writer and the
    /// chart-dan derivation, so neither has to depend on the other.
    /// </summary>
    public static class EzHoldMetrics
    {
        /// <summary>Hold objects over column objects on <paramref name="playable"/>. 0 for an empty chart.</summary>
        public static double ComputeHoldRatio(IBeatmap playable)
        {
            var (holds, total) = countHoldAndTotal(playable);
            return total <= 0 ? 0 : (double)holds / total;
        }

        /// <summary>Column hold (LN) object count on <paramref name="playable"/>.</summary>
        public static int ComputeHoldCount(IBeatmap playable)
            => countHoldAndTotal(playable).Holds;

        /// <summary>Hold count already stamped on the beatmap, or -1 when unset.</summary>
        public static int TryHoldCountFromBeatmapInfo(BeatmapInfo beatmapInfo)
            => beatmapInfo.EndTimeObjectCount >= 0 ? beatmapInfo.EndTimeObjectCount : -1;

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

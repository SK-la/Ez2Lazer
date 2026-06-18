// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.EzOsuGame.Scoring
{
    internal static class EzScoreRaceMetricOrdering
    {
        public static IOrderedEnumerable<T> ApplyMetricOrdering<T>(
            IEnumerable<T> source,
            EzScoreRaceMetric metric,
            Func<T, long> totalScore,
            Func<T, double> accuracy,
            Func<T, int> combo,
            Func<T, int> missCount)
        {
            return metric switch
            {
                EzScoreRaceMetric.Accuracy => source
                                              .OrderByDescending(accuracy)
                                              .ThenByDescending(totalScore),

                EzScoreRaceMetric.MaxCombo => source
                                              .OrderByDescending(combo)
                                              .ThenByDescending(totalScore),

                EzScoreRaceMetric.MissCount => source
                                               .OrderBy(missCount)
                                               .ThenByDescending(totalScore),

                _ => source.OrderByDescending(totalScore),
            };
        }
    }
}

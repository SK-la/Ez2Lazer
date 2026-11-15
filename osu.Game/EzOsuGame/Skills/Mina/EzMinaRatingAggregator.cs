// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.EzOsuGame.Skills.Mina
{
    /// <summary>
    /// Aggregates per-play <see cref="IEzMinaEngine.CalculatePlayerMinaSkillAxis"/> axis values into player skill ratings
    /// (Etterna ScoreManager::AggregateSunnySRs / mania-hub <c>aggregateSsrs</c> — upstream name, not chart SSR).
    /// </summary>
    public static class EzMinaRatingAggregator
    {
        private const double aggregate_rating_scaler = 1.04;

        public static double Aggregate(IEnumerable<double> values)
        {
            double[] playValues = values.Where(v => double.IsFinite(v) && v > 0).ToArray();
            if (playValues.Length == 0)
                return 0;

            double rating = 0;
            double res = 10.24;

            for (int iter = 1; iter <= 11; iter++)
            {
                double sum;

                do
                {
                    rating += res;
                    sum = 0;

                    foreach (double playValue in playValues)
                        sum += Math.Max(0, 2 / (1 - EzAbramowitzErf.Erf(0.1 * (playValue - rating))) - 2);
                }
                while (Math.Pow(2, rating * 0.1) < sum);

                if (iter == 11)
                    break;

                rating -= res;
                res /= 2;
            }

            return Math.Round(rating * aggregate_rating_scaler * 100) / 100;
        }

        public static EzMinaSkillAxisData AggregateVectors(IReadOnlyList<EzMinaSkillAxisData> plays)
        {
            if (plays.Count == 0)
                return default;

            return new EzMinaSkillAxisData(
                Aggregate(plays.Select(p => p.Overall)),
                Aggregate(plays.Select(p => p.Stream)),
                Aggregate(plays.Select(p => p.Jumpstream)),
                Aggregate(plays.Select(p => p.Handstream)),
                Aggregate(plays.Select(p => p.Stamina)),
                Aggregate(plays.Select(p => p.JackSpeed)),
                Aggregate(plays.Select(p => p.Chordjack)),
                Aggregate(plays.Select(p => p.Technical)));
        }
    }
}

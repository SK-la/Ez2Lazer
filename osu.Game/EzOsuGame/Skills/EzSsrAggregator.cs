// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Etterna ScoreManager::AggregateSSRs port (mania-hub player-skills.ts aggregateSsrs).
    /// </summary>
    public static class EzSsrAggregator
    {
        private const double aggregate_rating_scaler = 1.04;

        public static double Aggregate(IEnumerable<double> values)
        {
            double[] ssrs = values.Where(v => double.IsFinite(v) && v > 0).ToArray();
            if (ssrs.Length == 0)
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

                        foreach (double ssr in ssrs)
                        sum += Math.Max(0, 2 / (1 - EzAbramowitzErf.Erf(0.1 * (ssr - rating))) - 2);
                }
                while (Math.Pow(2, rating * 0.1) < sum);

                if (iter == 11)
                    break;

                rating -= res;
                res /= 2;
            }

            return Math.Round(rating * aggregate_rating_scaler * 100) / 100;
        }

        public static EzSkillsetVector AggregateVectors(IReadOnlyList<EzSkillsetVector> plays)
        {
            if (plays.Count == 0)
                return default;

            return new EzSkillsetVector(
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

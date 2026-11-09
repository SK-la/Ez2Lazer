// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.EzOsuGame.Skills.PPP
{
    /// <summary>
    /// PP+ performance formulas per ruleset, for a perfect (SS, full-combo, no-miss) play.
    /// </summary>
    /// <remarks>
    /// These are the formulas documented in <c>docs/skills/difficulty-skills-algorithms copy.md</c> sections
    /// 1.8 / 2.7 / 3.1 / 4.1. Only the perfect-play case is implemented because that is the only value the
    /// per-beatmap pp column carries: a missed-hit weight, a combo ratio and a guaranteed miss count would all
    /// need a score, which a chart does not have. The inputs are skill ratings already produced by the PPP
    /// chart calculators, so nothing here re-runs difficulty analysis.
    /// </remarks>
    public static class EzPPPPerformance
    {
        /// <summary>osu! performance points from the ratings that enter the total (section 1.8).</summary>
        /// <param name="greatHitWindow">Great (300) hit window in milliseconds, already clock-rate adjusted.</param>
        public static double Osu(double aim, double speed, double stamina, double accuracyDifficulty,
                                 int totalHits, int circleCount, double greatHitWindow, bool hidden, bool flashlight)
        {
            if (totalHits <= 0 || !double.IsFinite(greatHitWindow) || greatHitWindow <= 0)
                return -1;

            // OD is recovered from the hit window rather than the beatmap's difficulty value, so rate mods and
            // difficulty mods are already folded in. The window is the difficulty attribute's, not the raw one.
            double overallDifficulty = (80 - greatHitWindow) / 6;
            double deviation = accuracyDeviation(circleCount, totalHits, greatHitWindow, overallDifficulty);

            if (!double.IsFinite(deviation) || deviation <= 0)
                return -1;

            double missWeight = 1;
            double aimWeight = Math.Pow(0.995, deviation) * 1.04 * missWeight;

            if (flashlight)
                aimWeight *= 1 + Math.Atan(totalHits / 2000.0);

            double speedWeight = Math.Pow(0.985, deviation) * 1.12 * missWeight;
            double accuracyWeight = 1.2 * Math.Tanh((circleCount + 400) / 1050.0);

            if (hidden)
                accuracyWeight *= 1.02;

            if (flashlight)
                accuracyWeight *= 1.04;

            double aimValue = difficultyToPerformance(aim) * aimWeight;
            double speedValue = difficultyToPerformance(speed) * speedWeight;
            double staminaValue = difficultyToPerformance(stamina) * speedWeight;
            double accuracyValue = 560 * Math.Pow(0.85, deviation) * Math.Max(0, accuracyDifficulty) * accuracyWeight;

            // JumpAim / FlowAim / Precision share the aim weight but are not part of the total (section 1.8).
            double total = Math.Pow(aimValue, 1.1) + Math.Pow(Math.Max(speedValue, staminaValue), 1.1)
                                                   + Math.Pow(accuracyValue, 1.1);

            return 1.12 * Math.Pow(total, 1 / 1.1);
        }

        /// <summary>Taiko performance points (section 2.7).</summary>
        /// <param name="greatHitWindow">Great hit window in milliseconds, already clock-rate adjusted.</param>
        /// <param name="okHitWindow">Ok hit window in milliseconds, already clock-rate adjusted.</param>
        public static double Taiko(double starRating, int totalHits, double greatHitWindow, double okHitWindow, bool hidden, bool easy, bool hardRock, bool flashlight)
        {
            if (totalHits <= 0 || !double.IsFinite(starRating) || starRating <= 0)
                return -1;

            double deviation = taikoDeviation(totalHits, greatHitWindow, okHitWindow);

            if (!double.IsFinite(deviation) || deviation <= 0)
                return -1;

            double urgency = 10 * deviation;
            double multiplier = 1.13;

            if (hidden)
                multiplier *= 1.075;

            if (easy)
                multiplier *= 0.975;

            double lengthBonus = 1 + 0.1 * Math.Min(1, totalHits / 1500.0);
            double difficulty = Math.Pow(5 * Math.Max(1, starRating / 0.115) - 4, 2.25) / 1150
                                * lengthBonus
                                * (easy ? 0.985 : 1)
                                * (hidden ? 1.025 : 1)
                                * (hardRock ? 1.10 : 1)
                                * (flashlight ? 1 + 0.05 * lengthBonus : 1)
                                * Math.Pow(erf(400 / (Math.Sqrt(2) * urgency)), 2);

            double accuracy = Math.Pow(70 / urgency, 1.1)
                              * Math.Pow(starRating, 0.4)
                              * 100;

            if (flashlight && hidden)
                accuracy *= Math.Max(1, 1.05 * Math.Min(1.15, Math.Pow(totalHits / 1500.0, 0.3)));

            double total = Math.Pow(Math.Max(0, difficulty), 1.1) + Math.Pow(Math.Max(0, accuracy), 1.1);
            return Math.Pow(total, 1 / 1.1) * multiplier;
        }

        /// <summary>osu!catch performance points (section 3.1).</summary>
        /// <param name="comboObjects">Objects that award combo, i.e. everything except bananas and droplets.</param>
        public static double Catch(double starRating, double approachRate, int totalHits, int comboObjects, bool hidden, bool flashlight, bool noFail)
        {
            if (totalHits <= 0 || comboObjects <= 0 || !double.IsFinite(starRating) || starRating <= 0)
                return -1;

            double baseValue = Math.Pow(5 * Math.Max(1, starRating / 0.0049) - 4, 2) / 1e5;
            double lengthBonus = 0.95 + 0.3 * Math.Min(1, comboObjects / 2500.0);

            if (comboObjects > 2500)
                lengthBonus += Math.Log10(comboObjects / 2500.0) * 0.475;

            double arBonus = approachRate switch
            {
                > 10 => 1 + 0.1 * (approachRate - 9) + 0.1 * (approachRate - 10),
                > 9 => 1 + 0.1 * (approachRate - 9),
                < 8 => 1 + 0.025 * (8 - approachRate),
                _ => 1,
            };

            double hdBonus = approachRate <= 10
                ? 1.05 + 0.075 * (10 - approachRate)
                : 1.01 + 0.04 * (11 - Math.Min(11, approachRate));

            double total = baseValue * lengthBonus;

            if (hidden)
                total *= hdBonus;

            // A perfect play has no misses, so the miss penalty and the NoFail leniency are both identity here.
            if (flashlight)
                total *= 1 + 0.35 * lengthBonus;

            if (noFail)
                total *= 1;

            return total * arBonus;
        }

        /// <summary>osu!mania performance points (section 4.1).</summary>
        public static double Mania(double starRating, int totalHits, bool noFail, bool easy)
        {
            if (totalHits <= 0 || !double.IsFinite(starRating) || starRating <= 0)
                return -1;

            // Perfect accuracy: the accuracy multiplier saturates at 1.
            double difficulty = 8 * Math.Pow(Math.Max(starRating - 0.15, 0.05), 2.2)
                                  * (1 + 0.1 * Math.Min(1, totalHits / 1500.0));

            if (noFail)
                difficulty *= 0.75;

            if (easy)
                difficulty *= 0.5;

            return difficulty;
        }

        /// <summary>Average hit error a perfect play implies on this chart, in the same unit as the hit window.</summary>
        private static double accuracyDeviation(int circleCount, int totalHits, double greatHitWindow, double overallDifficulty)
        {
            // c' = N300 - (N - c); a perfect play has N300 = N, so c' = c.
            int effectiveCircles = circleCount;

            if (effectiveCircles <= 0)
                return 200 - 10 * overallDifficulty;

            // Beta(c, 1) has CDF x^c, so its 0.2 quantile is 0.2^(1/c) in closed form.
            double lower = Math.Pow(0.2, 1.0 / effectiveCircles);
            double quantile = lower + (1 - lower) / 2;
            return greatHitWindow / EzPPPMath.InverseNormalCdf(quantile);
        }

        private static double taikoDeviation(int totalHits, double greatHitWindow, double okHitWindow)
        {
            const double z = 2.32634787404;

            // Perfect play: p = 1 for both the Great and the positive-hit share.
            double lower = (totalHits + z * z / 2) / (totalHits + z * z) - z / (totalHits + z * z) * (z / 2);

            double inverse = EzPPPMath.InverseErf(lower);

            if (!double.IsFinite(inverse) || inverse <= 0)
                return double.NaN;

            double great = greatHitWindow / (Math.Sqrt(2) * inverse);
            double ok = okHitWindow / (Math.Sqrt(2) * inverse);
            return Math.Min(great, ok);
        }

        private static double difficultyToPerformance(double difficulty) => 3.9 * difficulty * difficulty * difficulty;

        /// <summary>Error function, A&amp;S 7.1.26 (five-term expansion, absolute error below 1.5e-7).</summary>
        private static double erf(double x)
        {
            double sign = x < 0 ? -1 : 1;
            double ax = Math.Abs(x);
            double t = 1 / (1 + 0.3275911 * ax);
            double poly = ((((1.061405429 * t - 1.453152027) * t + 1.421413741) * t - 0.284496736) * t + 0.254829592) * t;
            return sign * (1 - poly * Math.Exp(-ax * ax));
        }
    }
}

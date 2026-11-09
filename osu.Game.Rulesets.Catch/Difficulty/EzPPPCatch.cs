// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.LocalProfile;
using osu.Game.EzOsuGame.Skills.PPP;
using osu.Game.Rulesets.Catch.Beatmaps;
using osu.Game.Rulesets.Catch.Objects;
using osu.Game.Rulesets.Catch.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Rulesets.Catch.Difficulty
{
    internal static class EzPPPCatchRegistration
    {
#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void Register()
        {
            EzPPPCalculators.Register(EzLocalProfileConstants.CATCH_RULESET_ID, static (beatmap, mods, rate) => EzPPPCatch.Calculate(beatmap, mods, rate));
            EzPPPPerformanceCalculators.Register(EzLocalProfileConstants.CATCH_RULESET_ID, static (skills, beatmap, mods, rate) => performance(skills, beatmap, mods, rate));
        }

        /// <summary>PP+ performance for a perfect play (documented section 3.1).</summary>
        private static double performance(IReadOnlyDictionary<string, double> skills, IBeatmap beatmap, IReadOnlyList<Mod> mods, double clockRate)
        {
            var difficulty = beatmap.Difficulty.Clone();

            foreach (var mod in mods)
            {
                if (mod is IApplicableToDifficulty applicable)
                    applicable.ApplyToDifficulty(difficulty);
            }

            double rate = clockRate > 0 ? clockRate : 1;
            double preempt = IBeatmapDifficultyInfo.DifficultyRange(difficulty.ApproachRate, 1800, 1200, 450) / rate;
            double approachRate = preempt > 1200 ? -(preempt - 1800) / 120 : -(preempt - 1200) / 150 + 5;

            int totalHits = 0;
            int comboObjects = 0;

            foreach (var hitObject in beatmap.HitObjects)
            {
                count(hitObject, ref totalHits, ref comboObjects);
            }

            bool hidden = false;
            bool flashlight = false;
            bool noFail = false;

            foreach (var mod in mods)
            {
                switch (mod)
                {
                    case ModHidden:
                        hidden = true;
                        break;

                    case ModFlashlight:
                        flashlight = true;
                        break;

                    case ModNoFail:
                        noFail = true;
                        break;
                }
            }

            return EzPPPPerformance.Catch(
                EzPPPPerformanceCalculators.Rating(skills, EzPPPSkillCatalog.STAR_RATING),
                approachRate,
                totalHits,
                comboObjects,
                hidden,
                flashlight,
                noFail);
        }

        private static void count(HitObject hitObject, ref int totalHits, ref int comboObjects)
        {
            // A perfect play awards the maximum judgement to every object, so the totals are the object counts.
            // Bananas are not judged and droplets award only a small tick, which is not a combo object.
            if (hitObject is Banana)
                return;

            if (hitObject is TinyDroplet)
            {
                totalHits++;
                return;
            }

            if (hitObject is PalpableCatchHitObject)
            {
                totalHits++;
                comboObjects++;
            }

            foreach (var nested in hitObject.NestedHitObjects)
                count(nested, ref totalHits, ref comboObjects);
        }
    }

    /// <summary>osu!catch PPP movement skill and its star rating.</summary>
    internal static class EzPPPCatch
    {
        public static Dictionary<string, double> Calculate(IBeatmap beatmap, IReadOnlyList<Mod> mods, double clockRate)
        {
            var difficulty = beatmap.Difficulty.Clone();

            foreach (var mod in mods)
            {
                if (mod is IApplicableToDifficulty applicable)
                    applicable.ApplyToDifficulty(difficulty);
            }

            var fruits = new List<PalpableCatchHitObject>();

            foreach (var hitObject in CatchBeatmap.GetPalpableObjects(beatmap.HitObjects))
            {
                if (hitObject is Banana or TinyDroplet)
                    continue;

                fruits.Add(hitObject);
            }

            if (fruits.Count < 2)
                return new Dictionary<string, double>(EzPPPSkillCatalog.ChartZeros(EzLocalProfileConstants.CATCH_RULESET_ID), StringComparer.Ordinal);

            double rate = clockRate > 0 ? clockRate : 1;
            float halfWidth = Catcher.CalculateCatchWidth(difficulty) * 0.5f;
            halfWidth *= Math.Max(0.1f, 1f - 0.0625f * Math.Max(0, difficulty.CircleSize - 5.5f));
            halfWidth = Math.Max(1, halfWidth);

            var times = new List<double>();
            var strains = new List<double>();
            double state = 0;
            double previousPlayer = fruits[0].EffectiveX * (41.0 / halfWidth);
            double previousDelta = 0;
            double previousStrain = 40;

            for (int i = 1; i < fruits.Count; i++)
            {
                double x = fruits[i].EffectiveX * (41.0 / halfWidth);
                double player = Math.Clamp(previousPlayer, x - 25, x + 25);

                if (fruits[i - 1].HyperDash)
                    player = x;

                double delta = player - previousPlayer;
                double strainTime = Math.Max(40, (fruits[i].StartTime - fruits[i - 1].StartTime) / rate);
                double timeWeight = strainTime + 13 + 3 / rate;
                double basis = Math.Pow(Math.Abs(delta), 1.3) / 510.0;
                double stream = 12.5 * Math.Min(Math.Abs(delta), 82) / 246.0 * (1 / Math.Sqrt(timeWeight));
                double turn = 0;

                if (Math.Sign(delta) != Math.Sign(previousDelta) && Math.Abs(delta) > 0.1 && Math.Abs(previousDelta) > 0.1)
                {
                    turn = 21 / Math.Sqrt(previousStrain + 16)
                           * Math.Min(50, Math.Abs(delta)) / 50.0
                           * Math.Max(0.38, Math.Min(70, Math.Abs(previousDelta)) / 70.0)
                           * Math.Max(1 - Math.Pow(timeWeight / 1000.0, 3), 0);
                }

                double edge = 1;
                float hyperDistance = fruits[i - 1].DistanceToHyperDash;

                if (hyperDistance <= 20)
                {
                    // The exponent uses the raw strain time, not the weighted time.
                    double edgeBonus = fruits[i - 1].HyperDash ? 0 : 5.7;
                    edge = 1 + edgeBonus * ((20 - hyperDistance) / 20.0) * Math.Pow(Math.Min(strainTime * rate, 265) / 265.0, 1.5);
                }

                double strain = (basis + stream + turn) / timeWeight * edge;
                state = EzPPPMath.Decay(strainTime, 0.2) * state + strain;
                times.Add(fruits[i].StartTime / rate);
                strains.Add(state);
                previousPlayer = player;
                previousDelta = delta;
                previousStrain = strainTime;
            }

            double value = EzPPPMath.Aggregate(times, strains, 750, 0.94, 0.2);
            double star = 4.59 * Math.Sqrt(Math.Max(0, value));

            return new Dictionary<string, double>(StringComparer.Ordinal)
            {
                [EzPPPSkillCatalog.ChartSkillId(EzPPPSkillCatalog.MOVEMENT)] = EzPPPMath.Finite(star),
                [EzPPPSkillCatalog.ChartSkillId(EzPPPSkillCatalog.STAR_RATING)] = EzPPPMath.Finite(star),
            };
        }
    }
}

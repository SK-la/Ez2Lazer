// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.LocalProfile;
using osu.Game.EzOsuGame.Skills.PPP;
using osu.Game.Rulesets.Catch.Objects;
using osu.Game.Rulesets.Catch.UI;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Rulesets.Catch.Difficulty
{
    internal static class EzPPPCatchRegistration
    {
#pragma warning disable CA2255
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void Register()
            => EzPPPCalculators.Register(EzLocalProfileConstants.CATCH_RULESET_ID, static (beatmap, mods, rate) => EzPPPCatch.Calculate(beatmap, rate));
    }

    /// <summary>osu!catch PPP movement skill and its star rating.</summary>
    internal static class EzPPPCatch
    {
        public static Dictionary<string, double> Calculate(IBeatmap beatmap, double clockRate)
        {
            var fruits = new List<PalpableCatchHitObject>();

            foreach (var hitObject in beatmap.HitObjects)
                collect(hitObject, fruits);

            if (fruits.Count < 2)
                return new Dictionary<string, double>(EzPPPSkillCatalog.ChartZeros(EzLocalProfileConstants.CATCH_RULESET_ID), StringComparer.Ordinal);

            fruits.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));

            double rate = Math.Max(0.1, clockRate);
            float halfWidth = Catcher.CalculateCatchWidth(beatmap.Difficulty) * 0.5f;

            if (beatmap.Difficulty.CircleSize > 5.5f)
                halfWidth *= Math.Max(0.5f, 1f - 0.08f * (beatmap.Difficulty.CircleSize - 5.5f));

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
                double strainTime = Math.Max(40, fruits[i].StartTime - fruits[i - 1].StartTime);
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
                    double edgeBonus = fruits[i - 1].HyperDash ? 0 : 5.7;
                    edge = 1 + edgeBonus * ((20 - hyperDistance) / 20.0) * Math.Pow(Math.Min(timeWeight * rate, 265) / 265.0, 1.5);
                }

                double strain = (basis + stream + turn) / timeWeight * edge;
                state = EzPPPMath.Decay(strainTime, 0.2) * state + strain;
                times.Add(fruits[i].StartTime);
                strains.Add(state);
                previousPlayer = player;
                previousDelta = delta;
                previousStrain = strainTime;
            }

            double value = EzPPPMath.Aggregate(times, strains, 750, 0.94);
            double star = 4.59 * Math.Sqrt(Math.Max(0, value));

            return new Dictionary<string, double>(StringComparer.Ordinal)
            {
                [EzPPPSkillCatalog.ChartSkillId(EzPPPSkillCatalog.MOVEMENT)] = EzPPPMath.Finite(star),
                [EzPPPSkillCatalog.ChartSkillId(EzPPPSkillCatalog.STAR_RATING)] = EzPPPMath.Finite(star),
            };
        }

        private static void collect(HitObject hitObject, List<PalpableCatchHitObject> fruits)
        {
            if (hitObject is Banana or TinyDroplet)
                return;

            if (hitObject is PalpableCatchHitObject fruit)
                fruits.Add(fruit);

            foreach (var nested in hitObject.NestedHitObjects)
                collect(nested, fruits);
        }
    }
}

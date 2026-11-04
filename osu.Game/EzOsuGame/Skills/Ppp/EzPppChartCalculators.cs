// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mods;

namespace osu.Game.EzOsuGame.Skills.Ppp
{
    /// <summary>
    /// Ruleset projects register their PPP chart calculators here. <c>osu.Game</c> does not reference ruleset assemblies.
    /// </summary>
    public static class EzPppChartCalculators
    {
        private static readonly Dictionary<int, Func<IBeatmap, IReadOnlyList<Mod>, double, IReadOnlyDictionary<string, double>>> calculators = new Dictionary<int, Func<IBeatmap, IReadOnlyList<Mod>, double, IReadOnlyDictionary<string, double>>>();

        public static void Register(int rulesetOnlineId, Func<IBeatmap, IReadOnlyList<Mod>, double, IReadOnlyDictionary<string, double>> calculate)
        {
            ArgumentNullException.ThrowIfNull(calculate);
            calculators[rulesetOnlineId] = calculate;
        }

        public static bool TryCalculate(int rulesetOnlineId, IBeatmap beatmap, IReadOnlyList<Mod> mods, double clockRate, out IReadOnlyDictionary<string, double> skills)
        {
            if (calculators.TryGetValue(rulesetOnlineId, out var calculate))
            {
                skills = calculate(beatmap, mods, clockRate);
                return true;
            }

            skills = EzPppSkillCatalog.ChartZeros(rulesetOnlineId);
            return false;
        }
    }
}

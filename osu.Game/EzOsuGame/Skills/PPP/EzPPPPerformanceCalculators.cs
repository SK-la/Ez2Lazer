// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mods;

namespace osu.Game.EzOsuGame.Skills.PPP
{
    /// <summary>
    /// Reads a mode's own hit windows and object counts and turns a PP+ chart vector into performance points.
    /// </summary>
    /// <remarks>
    /// Ruleset projects register here for the same reason <see cref="EzPPPCalculators"/> exists: the inputs a
    /// performance formula needs (hit windows, which objects award combo) are typed per ruleset, and
    /// <c>osu.Game</c> does not reference ruleset assemblies.
    /// </remarks>
    /// <param name="chartSkills">The PP+ chart vector, keyed by stored skill id.</param>
    public delegate double EzPPPPerformanceCalculator(
        IReadOnlyDictionary<string, double> chartSkills,
        IBeatmap beatmap,
        IReadOnlyList<Mod> mods,
        double clockRate);

    /// <summary>Per-ruleset PP+ performance calculators.</summary>
    public static class EzPPPPerformanceCalculators
    {
        private static readonly Dictionary<int, EzPPPPerformanceCalculator> calculators = new Dictionary<int, EzPPPPerformanceCalculator>();

        public static void Register(int rulesetOnlineId, EzPPPPerformanceCalculator calculate)
        {
            ArgumentNullException.ThrowIfNull(calculate);
            calculators[rulesetOnlineId] = calculate;
        }

        /// <summary>
        /// Performance points for a perfect play of <paramref name="beatmap"/>. Returns -1 when the ruleset has no
        /// PP+ performance formula or when its inputs are missing.
        /// </summary>
        public static double Calculate(int rulesetOnlineId, IReadOnlyDictionary<string, double> chartSkills, IBeatmap beatmap,
                                       IReadOnlyList<Mod> mods, double clockRate)
        {
            if (!calculators.TryGetValue(rulesetOnlineId, out var calculate))
                return -1;

            double pp = calculate(chartSkills, beatmap, mods, clockRate);
            return double.IsFinite(pp) && pp > 0 ? pp : -1;
        }

        /// <summary>Reads one chart skill rating, or 0 when the axis is absent.</summary>
        public static double Rating(IReadOnlyDictionary<string, double> chartSkills, string metricId)
            => chartSkills.TryGetValue(EzPPPSkillCatalog.ChartSkillId(metricId), out double value) && double.IsFinite(value) ? value : 0;
    }
}

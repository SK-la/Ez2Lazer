// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osu.Game.Utils;

namespace osu.Game.EzOsuGame.Skills.PPP
{
    /// <summary>
    /// PPP chart skills for osu, taiko, catch, and mania.
    /// Play skills scale the chart vector by accuracy and combo; that curve is a stand-in.
    /// </summary>
    public sealed class EzPPPEngine : IEzPPPEngine
    {
        public IReadOnlyDictionary<string, double> CalculateChart(IBeatmap beatmap, IReadOnlyList<Mod>? mods)
        {
            ArgumentNullException.ThrowIfNull(beatmap);
            mods ??= Array.Empty<Mod>();

            int rulesetId = beatmap.BeatmapInfo.Ruleset.OnlineID;

            if (!EzPPPSkillCatalog.SupportsRuleset(rulesetId))
                return new Dictionary<string, double>(StringComparer.Ordinal);

            double clockRate = ModUtils.CalculateRateWithMods(mods);
            EzPPPCalculators.TryCalculate(rulesetId, beatmap, mods, clockRate, out var skills);
            return skills;
        }

        public IReadOnlyDictionary<string, double> CalculatePlay(IReadOnlyDictionary<string, double> chartSkills, ScoreInfo score)
        {
            ArgumentNullException.ThrowIfNull(chartSkills);
            ArgumentNullException.ThrowIfNull(score);

            double accuracy = Math.Clamp(score.Accuracy, 0, 1);
            int possible = score.GetMaximumAchievableCombo();
            double comboScale = possible > 0 ? Math.Pow(Math.Clamp(score.MaxCombo / (double)possible, 0, 1), 0.5) : 1;
            double scale = Math.Pow(accuracy, 1.5) * comboScale;
            var result = new Dictionary<string, double>(chartSkills.Count, StringComparer.Ordinal);

            foreach (var (metric, value) in chartSkills)
            {
                if (string.IsNullOrEmpty(metric))
                    continue;

                bool accuracyAxis = metric is "accuracy" or "accuracy_difficulty";
                double scaled = value * (accuracyAxis ? Math.Pow(accuracy, 2) * comboScale : scale);
                result[EzPPPSkillCatalog.PlayerSkillId(metric)] = EzPPPMath.Finite(scaled);
            }

            return result;
        }
    }
}

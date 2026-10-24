// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Scoring;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// PP+-shaped osu skill engine. Swap implementations after authorisation
    /// (<see cref="EzPpPlusStubEngine"/> is temporary and is not real PP+).
    /// </summary>
    public interface IEzPpPlusEngine
    {
        /// <summary>Chart-side axes from official difficulty attrs + map difficulty knobs.</summary>
        EzPpPlusAttributes CalculateChart(DifficultyAttributes attributes, IBeatmapDifficultyInfo difficulty, double lengthSeconds);

        /// <summary>Per-play axes: scale chart vector by score quality (stub); real PP+ replaces this.</summary>
        EzPpPlusAttributes CalculatePlay(EzPpPlusAttributes chart, ScoreInfo score);
    }

    /// <summary>PP+-shaped attribute vector (Aim Total + six radar axes).</summary>
    public readonly record struct EzPpPlusAttributes(
        double Aim,
        double JumpAim,
        double FlowAim,
        double Precision,
        double Speed,
        double Stamina,
        double Accuracy)
    {
        public IReadOnlyDictionary<string, double> ToChartSkills()
            => toSkills(chart: true);

        public IReadOnlyDictionary<string, double> ToPlayerSkills()
            => toSkills(chart: false);

        public static bool IsCompleteChartCache(IReadOnlyDictionary<string, double>? skills)
        {
            if (skills == null || skills.Count == 0)
                return false;

            foreach (var axis in EzOsuSkillAxisExtensions.All)
            {
                if (!skills.ContainsKey(axis.ToChartSkillId()))
                    return false;
            }

            return true;
        }

        private IReadOnlyDictionary<string, double> toSkills(bool chart)
        {
            var dict = new Dictionary<string, double>(EzOsuSkillAxisExtensions.All.Length);

            foreach (var axis in EzOsuSkillAxisExtensions.All)
            {
                double value = axis switch
                {
                    EzOsuSkillAxis.Aim => Aim,
                    EzOsuSkillAxis.JumpAim => JumpAim,
                    EzOsuSkillAxis.FlowAim => FlowAim,
                    EzOsuSkillAxis.Precision => Precision,
                    EzOsuSkillAxis.Speed => Speed,
                    EzOsuSkillAxis.Stamina => Stamina,
                    EzOsuSkillAxis.Accuracy => Accuracy,
                    _ => 0,
                };

                dict[chart ? axis.ToChartSkillId() : axis.ToPlayerSkillId()] = value;
            }

            return dict;
        }
    }
}

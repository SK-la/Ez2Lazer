// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

// TEMPORARY — not PP+; replace after authorisation with a real IEzPpPlusEngine.
// Do not copy GPL Syriiin/difficalcy-performanceplus sources into this tree.

using System;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Scoring;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Heuristic stand-in that fills PP+-shaped axes from official difficulty attrs.
    /// Values are for pipeline / UI validation only — they are not PerformancePlus.
    /// </summary>
    public sealed class EzPpPlusStubEngine : IEzPpPlusEngine
    {
        // Mirrors DifficultyAttributes attrib ids (osu chart export).
        private const int attrib_aim = 1;
        private const int attrib_speed = 3;
        private const int attrib_slider_factor = 19;
        private const int attrib_speed_note_count = 21;

        public EzPpPlusAttributes CalculateChart(DifficultyAttributes attributes, IBeatmapDifficultyInfo difficulty, double lengthSeconds)
        {
            ArgumentNullException.ThrowIfNull(attributes);
            ArgumentNullException.ThrowIfNull(difficulty);

            double aim = 0;
            double speed = 0;
            double sliderFactor = double.NaN;
            double speedNoteCount = 0;

            foreach (var (id, raw) in attributes.ToDatabaseAttributes())
            {
                if (!tryToDouble(raw, out double value))
                    continue;

                switch (id)
                {
                    case attrib_aim:
                        aim = value;
                        break;

                    case attrib_speed:
                        speed = value;
                        break;

                    case attrib_slider_factor:
                        sliderFactor = value;
                        break;

                    case attrib_speed_note_count:
                        speedNoteCount = value;
                        break;
                }
            }

            // Prefer official SliderFactor; fall back to a CS-derived stand-in.
            if (!double.IsFinite(sliderFactor))
                sliderFactor = Math.Clamp(difficulty.CircleSize / 10.0, 0.15, 0.85);
            else
                sliderFactor = Math.Clamp(sliderFactor, 0, 1);

            double jumpAim = aim * (0.55 + 0.45 * (1.0 - sliderFactor));
            double flowAim = aim * (0.35 + 0.65 * sliderFactor);
            double precision = Math.Max(0, (difficulty.CircleSize - 1.0) * 0.55);
            double staminaScale = speedNoteCount > 0
                ? 0.45 + 0.55 * Math.Clamp(speedNoteCount / 2000.0, 0, 1.5)
                : 0.55 + 0.45 * Math.Clamp(lengthSeconds / 180.0, 0, 1.5);
            double stamina = speed * staminaScale;
            double accuracyAxis = Math.Max(0, difficulty.OverallDifficulty * 0.45);
            double aimTotal = Math.Max(jumpAim, flowAim);

            return new EzPpPlusAttributes(
                Aim: aimTotal,
                JumpAim: jumpAim,
                FlowAim: flowAim,
                Precision: precision,
                Speed: speed,
                Stamina: stamina,
                Accuracy: accuracyAxis);
        }

        public EzPpPlusAttributes CalculatePlay(EzPpPlusAttributes chart, ScoreInfo score)
        {
            ArgumentNullException.ThrowIfNull(score);

            double acc = Math.Clamp(score.Accuracy, 0, 1);
            // Stub only: avoid BeatmapInfo.MaxCombo (obsolete). Combo weight is folded into accuracy.
            double scale = Math.Pow(acc, 1.5);

            return new EzPpPlusAttributes(
                Aim: chart.Aim * scale,
                JumpAim: chart.JumpAim * scale,
                FlowAim: chart.FlowAim * scale,
                Precision: chart.Precision * scale,
                Speed: chart.Speed * scale,
                Stamina: chart.Stamina * scale,
                Accuracy: chart.Accuracy * Math.Pow(acc, 2.0));
        }

        private static bool tryToDouble(object? raw, out double value)
        {
            switch (raw)
            {
                case double d:
                    value = d;
                    return true;

                case float f:
                    value = f;
                    return true;

                case int i:
                    value = i;
                    return true;

                case long l:
                    value = l;
                    return true;

                case IConvertible c:
                    value = c.ToDouble(null);
                    return true;

                default:
                    value = 0;
                    return false;
            }
        }
    }
}

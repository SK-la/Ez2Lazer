// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Localization;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// LN skill (float) radar axes from chart LN structure features.
    /// 7K subtype pattern scores (≥0.2) replace matching structure slots.
    /// Not MinaCalc MSD — do not call this "MSD LN".
    /// </summary>
    public static class EzLnSkillRadar
    {
        /// <summary>Hub pattern visibility floor reused for subtype replacement.</summary>
        public const double SUBTYPE_REPLACE_MIN = 0.2;

        public readonly record struct Axis(
            LocalisableString Label,
            double RawValue,
            float Normalized);

        private static readonly LocalisableString label_hold =
            new EzLocalizationManager.EzLocalisableString("Hold", "Hold");

        private static readonly LocalisableString label_density =
            new EzLocalizationManager.EzLocalisableString("Density", "Density");

        private static readonly LocalisableString label_overlap =
            new EzLocalizationManager.EzLocalisableString("Overlap", "Overlap");

        private static readonly LocalisableString label_release =
            new EzLocalizationManager.EzLocalisableString("Release", "Release");

        private static readonly LocalisableString label_chord =
            new EzLocalizationManager.EzLocalisableString("Chord", "Chord");

        private static readonly LocalisableString label_hold_p90 =
            new EzLocalizationManager.EzLocalisableString("Hold P90", "Hold P90");

        private static readonly LocalisableString label_lngeneral =
            new EzLocalizationManager.EzLocalisableString("LN General", "LN General");

        private static readonly LocalisableString label_lninverse =
            new EzLocalizationManager.EzLocalisableString("LN Inverse", "LN Inverse");

        private static readonly LocalisableString label_lnrelease =
            new EzLocalizationManager.EzLocalisableString("LN Release", "LN Release");

        private static readonly LocalisableString label_lntech =
            new EzLocalizationManager.EzLocalisableString("LN Tech", "LN Tech");

        /// <summary>
        /// Six LN structure axes; 7K subtype scores replace Hold / Overlap / Release / Chord when ≥ <see cref="SUBTYPE_REPLACE_MIN"/>.
        /// </summary>
        public static IReadOnlyList<Axis> BuildAxes(
            EzDanFeatureMetrics metrics,
            IReadOnlyDictionary<string, double>? subtypeScores,
            int keyCount)
        {
            ArgumentNullException.ThrowIfNull(metrics);

            subtypeScores ??= new Dictionary<string, double>(StringComparer.Ordinal);

            var hold = axisOrSubtype(
                label_hold,
                metrics.HoldRatio,
                cap: 1,
                subtypeScores,
                "lngeneral",
                label_lngeneral);

            var density = structureOnly(label_density, metrics.LnDensity, cap: 1);

            var overlap = axisOrSubtype(
                label_overlap,
                metrics.LnOverlapPressure,
                cap: 5,
                subtypeScores,
                "lninverse",
                label_lninverse);

            var release = keyCount == 7
                ? axisOrSubtype(
                    label_release,
                    metrics.LnReleasePressure,
                    cap: 46,
                    subtypeScores,
                    "lnrelease",
                    label_lnrelease)
                : structureOnly(label_release, metrics.LnReleasePressure, cap: 46);

            var chord = axisOrSubtype(
                label_chord,
                metrics.LnChordPressure,
                cap: 1,
                subtypeScores,
                "lntech",
                label_lntech);

            var holdP90 = structureOnly(label_hold_p90, metrics.LnHoldDurationP90, cap: 800);

            return new[] { hold, density, overlap, release, chord, holdP90 };
        }

        private static Axis structureOnly(LocalisableString label, double raw, double cap)
        {
            double safe = double.IsFinite(raw) && raw > 0 ? raw : 0;
            return new Axis(label, safe, normalize(safe, cap));
        }

        private static Axis axisOrSubtype(
            LocalisableString structureLabel,
            double structureRaw,
            double cap,
            IReadOnlyDictionary<string, double> subtypeScores,
            string subtypeId,
            LocalisableString subtypeLabel)
        {
            if (subtypeScores.TryGetValue(subtypeId, out double score)
                && double.IsFinite(score)
                && score >= SUBTYPE_REPLACE_MIN)
            {
                double clamped = Math.Clamp(score, 0, 1);
                return new Axis(subtypeLabel, clamped, (float)clamped);
            }

            return structureOnly(structureLabel, structureRaw, cap);
        }

        private static float normalize(double raw, double cap)
        {
            if (cap <= 0 || !double.IsFinite(raw) || raw <= 0)
                return 0;

            return (float)Math.Clamp(raw / cap, 0, 1);
        }
    }
}

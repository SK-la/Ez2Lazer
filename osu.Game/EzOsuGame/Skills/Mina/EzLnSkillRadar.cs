// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Skills.SkillTag;

namespace osu.Game.EzOsuGame.Skills.Mina
{
    /// <summary>
    /// LN skill radar: six spokes built from the long-note axis declaration.
    /// Not MinaCalc MSD - the spoke values come from the structure pass, not the rating engine.
    /// </summary>
    /// <remarks>
    /// Axis identities and labels come from <see cref="EzManiaLNAxis"/>. This type owns the normalisation caps
    /// and the rule for which spoke a subtype may stand in for.
    /// </remarks>
    public static class EzLnSkillRadar
    {
        /// <summary>Subtype scores at or above this replace the spoke value instead of being ignored.</summary>
        public const double SUBTYPE_REPLACE_MIN = 0.2;

        public readonly record struct Axis(
            LocalisableString Label,
            double RawValue,
            float Normalized);

        /// <summary>Spoke order, with the normalisation cap each raw value is divided by.</summary>
        private static readonly (EzManiaLNAxis Axis, double Cap)[] spokes =
        {
            (EzManiaLNAxis.Density, 1),
            (EzManiaLNAxis.Overlap, 5),
            (EzManiaLNAxis.Release, 46),
            (EzManiaLNAxis.Chord, 1),
            (EzManiaLNAxis.HoldP90, 800),
        };

        /// <summary>
        /// Which subtype may replace which spoke. The hold spoke is absent because its value is the base hold
        /// ratio rather than a structure axis, so it is passed in separately.
        /// </summary>
        private static readonly Dictionary<EzManiaLNAxis, EzManiaLNAxis> replacements = new Dictionary<EzManiaLNAxis, EzManiaLNAxis>
        {
            [EzManiaLNAxis.Overlap] = EzManiaLNAxis.Inverse,
            [EzManiaLNAxis.Release] = EzManiaLNAxis.ReleaseSubtype,
            [EzManiaLNAxis.Chord] = EzManiaLNAxis.Tech,
        };

        /// <summary>
        /// Six ordered spokes: the hold ratio first, then the structure axes.
        /// </summary>
        /// <param name="holdRatio">Base hold ratio, not a structure measure.</param>
        /// <param name="axes">Axis values by id; empty renders a zeroed frame with the right labels.</param>
        /// <param name="keyCount">The release spoke is only replaceable on 7K.</param>
        public static IReadOnlyList<Axis> BuildAxes(double holdRatio, IReadOnlyDictionary<string, double> axes, int keyCount)
        {
            var result = new List<Axis>(spokes.Length + 1)
            {
                // Hold: the value is the base ratio, and only a general LN subtype may stand in for it.
                substituteOrDefault(hold_label, holdRatio, cap: 1, axes, EzManiaLNAxis.General),
            };

            foreach (var (axis, cap) in spokes)
            {
                bool replaceable = keyCount == 7 || axis != EzManiaLNAxis.Release;

                result.Add(replaceable && replacements.TryGetValue(axis, out var subtype)
                    ? substituteOrDefault(axis.Meta().DisplayName, get(axes, axis), cap, axes, subtype)
                    : structureOnly(axis.Meta().DisplayName, get(axes, axis), cap));
            }

            return result;
        }

        private static readonly LocalisableString hold_label = "Hold";

        private static double get(IReadOnlyDictionary<string, double> axes, EzManiaLNAxis axis)
            => axes.GetValueOrDefault(axis.ToId(), 0);

        private static Axis substituteOrDefault(LocalisableString label, double raw, double cap, IReadOnlyDictionary<string, double> axes, EzManiaLNAxis subtype)
        {
            if (axes.TryGetValue(subtype.ToId(), out double score) && double.IsFinite(score) && score >= SUBTYPE_REPLACE_MIN)
            {
                double clamped = Math.Clamp(score, 0, 1);
                return new Axis(subtype.Meta().DisplayName, clamped, (float)clamped);
            }

            return structureOnly(label, raw, cap);
        }

        private static Axis structureOnly(LocalisableString label, double raw, double cap)
        {
            double safe = double.IsFinite(raw) && raw > 0 ? raw : 0;
            return new Axis(label, safe, normalize(safe, cap));
        }

        private static float normalize(double raw, double cap)
        {
            if (cap <= 0 || !double.IsFinite(raw) || raw <= 0)
                return 0;

            return (float)Math.Clamp(raw / cap, 0, 1);
        }
    }
}

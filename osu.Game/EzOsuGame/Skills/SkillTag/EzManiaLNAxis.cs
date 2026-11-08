// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.EzOsuGame.Skills.SkillTag
{
    /// <summary>
    /// Long-note measures produced by the structure pass over a playable.
    /// </summary>
    /// <remarks>
    /// <c>InRadar</c> separates the two roles: a measure with it is drawn as its own spoke, a subtype without it
    /// replaces a spoke's value when the score is high enough. Axis ids are storage keys and must stay stable.
    /// </remarks>
    public enum EzManiaLNAxis
    {
        [EzSkillMeta("ln_density", "LN 密度", "Density", "#6f87d8")]
        Density,

        [EzSkillMeta("ln_overlap", "LN 重叠", "Overlap", "#b06bc0")]
        Overlap,

        [EzSkillMeta("ln_release", "LN 放手", "Release", "#ad6b5d")]
        Release,

        [EzSkillMeta("ln_chord", "LN 合键", "Chord", "#ec6a9c")]
        Chord,

        [EzSkillMeta("ln_hold_p90", "LN 时长 P90", "Hold P90", "#c59a5c")]
        HoldP90,

        [EzSkillMeta("ln_general", "LN 综合", "General", "#6f87d8", InRadar = false)]
        General,

        [EzSkillMeta("ln_inverse", "LN 倒手", "Inverse", "#b06bc0", InRadar = false)]
        Inverse,

        [EzSkillMeta("ln_release_pattern", "LN 放手型", "Release", "#ad6b5d", InRadar = false)]
        ReleaseSubtype,

        [EzSkillMeta("ln_tech", "LN 技", "Tech", "#83cf6b", InRadar = false)]
        Tech,
    }

    public static class EzManiaLNAxisExtensions
    {
        public static EzManiaLNAxis[] All => EzEnumMetaCache<EzManiaLNAxis>.ALL;

        /// <summary>Measures that are drawn as their own radar spoke.</summary>
        public static EzManiaLNAxis[] StructureAxes { get; } = All.Where(static a => a.Meta().InRadar).ToArray();

        /// <summary>Pattern subtypes that can replace a spoke value rather than being drawn themselves.</summary>
        public static EzManiaLNAxis[] Subtypes { get; } = All.Where(static a => !a.Meta().InRadar).ToArray();

        public static IReadOnlySet<string> StructureIds { get; }
            = new HashSet<string>(StructureAxes.Select(static a => a.ToId()), StringComparer.Ordinal);

        public static IReadOnlySet<string> SubtypeIds { get; }
            = new HashSet<string>(Subtypes.Select(static a => a.ToId()), StringComparer.Ordinal);

        public static EzSkillMetaAttribute Meta(this EzManiaLNAxis axis)
            => EzEnumMetaCache<EzManiaLNAxis>.Meta(axis);

        public static string ToId(this EzManiaLNAxis axis) => axis.Meta().Id;

        public static bool TryParse(string? axisId, out EzManiaLNAxis axis)
            => EzEnumMetaCache<EzManiaLNAxis>.TryParseIgnoreCase(bareId(axisId), out axis);

        /// <summary>
        /// Structure axis values measured on a playable, keyed by axis id. The hold ratio is not part of this table.
        /// </summary>
        public static IReadOnlyDictionary<string, double> FromFeatures(EzChartFeatureMetrics metrics)
        {
            ArgumentNullException.ThrowIfNull(metrics);

            return new Dictionary<string, double>(StringComparer.Ordinal)
            {
                [EzManiaLNAxis.Density.ToId()] = metrics.LnDensity,
                [EzManiaLNAxis.Overlap.ToId()] = metrics.LnOverlapPressure,
                [EzManiaLNAxis.Release.ToId()] = metrics.LnReleasePressure,
                [EzManiaLNAxis.Chord.ToId()] = metrics.LnChordPressure,
                [EzManiaLNAxis.HoldP90.ToId()] = metrics.LnHoldDurationP90,
            };
        }

        /// <summary>
        /// Structure axis values merged with the measured subtype scores into one id-keyed table.
        /// </summary>
        public static IReadOnlyDictionary<string, double> FromFeatures(EzChartFeatureMetrics metrics, IReadOnlyDictionary<string, double>? subtypeScores)
        {
            var axes = new Dictionary<string, double>(FromFeatures(metrics), StringComparer.Ordinal);

            if (subtypeScores == null)
                return axes;

            foreach (var (id, score) in subtypeScores)
                axes[id] = score;

            return axes;
        }

        private static string? bareId(string? axisId)
        {
            if (string.IsNullOrEmpty(axisId))
                return axisId;

            int dot = axisId.LastIndexOf('.');
            return dot >= 0 && dot < axisId.Length - 1 ? axisId[(dot + 1)..] : axisId;
        }
    }
}

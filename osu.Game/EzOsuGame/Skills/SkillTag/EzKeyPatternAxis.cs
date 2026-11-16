// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Analysis;

namespace osu.Game.EzOsuGame.Skills.SkillTag
{
    /// <summary>
    /// Key pattern radar axes. Wire labels are Title Case from ruleset analysis.
    /// Distinct from the Mina rating axes and from the player pattern rating axes.
    /// </summary>
    public enum EzKeyPatternAxis
    {
        [EzSkillMeta("Bracket", "衩", "Bracket")]
        Bracket,

        [EzSkillMeta("Chord", "合键", "Chord")]
        Chord,

        [EzSkillMeta("Chordstream", "大切", "Chordstream")]
        Chordstream,

        [EzSkillMeta("Jack", "叠", "Jack")]
        Jack,

        [EzSkillMeta("Delay", "延迟", "Delay")]
        Delay,

        [EzSkillMeta("Dump", "乱切", "Dump")]
        Dump,

        [EzSkillMeta("Burst", "爆发", "Burst")]
        Burst,

        [EzSkillMeta("Anchor", "锚点", "Anchor")]
        Anchor,

        [EzSkillMeta("LN Hold", "长条按住", "LN Hold")]
        LnHold,

        [EzSkillMeta("LN Release", "放手", "Release")]
        LnRelease,

        [EzSkillMeta("LN%", "长条%", "LN%")]
        LnPercent,

        [EzSkillMeta("Tech", "技", "Tech")]
        Tech,

        [EzSkillMeta("Stream", "切", "Stream")]
        Stream,
    }

    public static class EzKeyPatternAxisExtensions
    {
        public static EzKeyPatternAxis[] All => EzEnumMetaCache<EzKeyPatternAxis>.ALL;

        /// <summary>Axes the KeyPattern radar persist and Filter currently compute.</summary>
        public static EzKeyPatternAxis[] RadarAxes { get; } =
        {
            EzKeyPatternAxis.Bracket,
            EzKeyPatternAxis.Chord,
            EzKeyPatternAxis.LnPercent,
            EzKeyPatternAxis.Jack,
            EzKeyPatternAxis.Delay,
            EzKeyPatternAxis.Dump,
        };

        public static IReadOnlyList<string> RadarAxisIds { get; } = RadarAxes.Select(static a => a.ToMetaId()).ToArray();

        public static EzSkillMetaAttribute Meta(this EzKeyPatternAxis axis)
            => EzEnumMetaCache<EzKeyPatternAxis>.Meta(axis);

        public static string ToMetaId(this EzKeyPatternAxis axis) => axis.Meta().Id;

        public static string ToWireLabel(this EzKeyPatternAxis axis) => axis.ToMetaId();

        public static LocalisableString DisplayName(this EzKeyPatternAxis axis) => axis.Meta().DisplayName;

        public static bool TryParse(string? wire, out EzKeyPatternAxis axis)
            => EzEnumMetaCache<EzKeyPatternAxis>.TryParse(wire, out axis);

        public static bool HasCompleteRadarSkills(IReadOnlyDictionary<string, double> skills)
        {
            if (skills.Count == 0)
                return false;

            foreach (string axisId in RadarAxisIds)
            {
                if (!skills.TryGetValue(axisId, out double value) || !double.IsFinite(value))
                    return false;
            }

            return true;
        }
    }

    /// <summary>Builds the KeyPattern radar from persisted Pattern rows. Axis keys are wire ids.</summary>
    public static class EzKeyPatternRadar
    {
        public static EzRadarChartData<string>? TryCreateFromStored(IReadOnlyDictionary<string, double> skills)
        {
            if (!EzKeyPatternAxisExtensions.HasCompleteRadarSkills(skills))
                return null;

            var axes = new EzRadarAxisValue<string>[EzKeyPatternAxisExtensions.RadarAxes.Length];

            for (int i = 0; i < axes.Length; i++)
            {
                var axis = EzKeyPatternAxisExtensions.RadarAxes[i];
                string id = axis.ToMetaId();
                axes[i] = new EzRadarAxisValue<string>(id, skills[id], "0.00");
            }

            return EzRadarChartData<string>.Create(axes);
        }
    }
}

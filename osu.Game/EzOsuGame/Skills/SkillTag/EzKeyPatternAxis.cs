// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
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

        /// <summary>Axes EKP persists and Filter reads. LN% is not stored.</summary>
        public static EzKeyPatternAxis[] RadarAxes { get; } =
        {
            EzKeyPatternAxis.Bracket,
            EzKeyPatternAxis.Chord,
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

    /// <summary>Builds the EKP radar: five persisted quantified axes plus LN% from hold ratio.</summary>
    public static class EzKeyPatternRadar
    {
        public static EzRadarChartData<string>? TryCreateFromStored(IReadOnlyDictionary<string, double> skills, double holdRatio)
        {
            if (!EzKeyPatternAxisExtensions.HasCompleteRadarSkills(skills))
                return null;

            double lnPercent = double.IsFinite(holdRatio)
                ? Math.Round(Math.Clamp(holdRatio, 0, 1) * 100, 2, MidpointRounding.AwayFromZero)
                : 0;

            return EzRadarChartData<string>.Create(
                axis(EzKeyPatternAxis.Bracket, skills),
                axis(EzKeyPatternAxis.Chord, skills),
                new EzRadarAxisValue<string>(EzKeyPatternAxis.LnPercent.ToMetaId(), lnPercent, "0.00"),
                axis(EzKeyPatternAxis.Jack, skills),
                axis(EzKeyPatternAxis.Delay, skills),
                axis(EzKeyPatternAxis.Dump, skills));
        }

        public static EzRadarChartData<string> WithHoldRatio(in EzRadarChartData<string> quantified, double holdRatio)
        {
            double lnPercent = double.IsFinite(holdRatio)
                ? Math.Round(Math.Clamp(holdRatio, 0, 1) * 100, 2, MidpointRounding.AwayFromZero)
                : 0;

            return EzRadarChartData<string>.Create(
                valueOf(quantified, EzKeyPatternAxis.Bracket),
                valueOf(quantified, EzKeyPatternAxis.Chord),
                new EzRadarAxisValue<string>(EzKeyPatternAxis.LnPercent.ToMetaId(), lnPercent, "0.00"),
                valueOf(quantified, EzKeyPatternAxis.Jack),
                valueOf(quantified, EzKeyPatternAxis.Delay),
                valueOf(quantified, EzKeyPatternAxis.Dump));
        }

        private static EzRadarAxisValue<string> axis(EzKeyPatternAxis patternAxis, IReadOnlyDictionary<string, double> skills)
        {
            string id = patternAxis.ToMetaId();
            return new EzRadarAxisValue<string>(id, skills[id], "0.00");
        }

        private static EzRadarAxisValue<string> valueOf(in EzRadarChartData<string> radar, EzKeyPatternAxis patternAxis)
        {
            string id = patternAxis.ToMetaId();

            for (int i = 0; i < radar.Count; i++)
            {
                if (radar[i].Axis == id)
                    return radar[i];
            }

            return new EzRadarAxisValue<string>(id, 0, "0.00");
        }
    }
}

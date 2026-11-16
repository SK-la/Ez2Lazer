// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Skills.SSR.SR4Pattern;

namespace osu.Game.EzOsuGame.Skills.SSR
{
    /// <summary>
    /// Chart Sunny SR4 skill axes persisted under <see cref="Analysis.EzAnalysisSystem.SSR"/> in <see cref="EzBeatmapSkillValue"/>.
    /// </summary>
    public enum EzSunnySrSkillAxis
    {
        [EzSkillMeta("bracket", "Bracket", "Bracket", "#8f6bd8")]
        Bracket,

        [EzSkillMeta("jack", "Jack", "Jack", "#ec6a9c")]
        Jack,

        [EzSkillMeta("burst", "Burst", "Burst", "#6f87d8")]
        Burst,

        [EzSkillMeta("ln_hold", "LN Hold", "LN Hold", "#b06bc0")]
        LnHold,

        [EzSkillMeta("ln_release", "LN Release", "LN Release", "#ad6b5d")]
        LnRelease,

        [EzSkillMeta("anchor", "Anchor", "Anchor", "#83cf6b")]
        Anchor,
    }

    public static class EzSunnySrSkillAxisExtensions
    {
        public static EzSunnySrSkillAxis[] ChartAxes { get; } = Enum.GetValues<EzSunnySrSkillAxis>();

        public static IReadOnlyList<string> ChartAxisIds { get; } = ChartAxes.Select(static a => a.ToMetaId()).ToArray();

        public static EzSkillMetaAttribute Meta(this EzSunnySrSkillAxis axis) => EzEnumMetaCache<EzSunnySrSkillAxis>.Meta(axis);

        public static string ToMetaId(this EzSunnySrSkillAxis axis) => axis.Meta().Id;

        public static bool TryParse(string? axisId, out EzSunnySrSkillAxis axis)
            => EzEnumMetaCache<EzSunnySrSkillAxis>.TryParseIgnoreCase(axisId, out axis);

        public static bool HasCompleteChartSkills(IReadOnlyDictionary<string, double> skills)
        {
            if (skills.Count == 0)
                return false;

            foreach (string axisId in ChartAxisIds)
            {
                if (!skills.TryGetValue(axisId, out double value) || !double.IsFinite(value))
                    return false;
            }

            return true;
        }
    }

    /// <summary>Builds the Sunny SR skill radar from persisted chart rows.</summary>
    public static class EzSunnySrSkillRadar
    {
        public static EzRadarChartData<string>? TryCreateFromStored(IReadOnlyDictionary<string, double> skills)
        {
            if (!EzSunnySrSkillAxisExtensions.HasCompleteChartSkills(skills))
                return null;

            var axes = new EzRadarAxisValue<string>[EzSunnySrSkillAxisExtensions.ChartAxes.Length];

            for (int i = 0; i < axes.Length; i++)
            {
                var axis = EzSunnySrSkillAxisExtensions.ChartAxes[i];
                string label = axis.Meta().DisplayName.ToString();
                axes[i] = new EzRadarAxisValue<string>(label, skills[axis.ToMetaId()], "0.00");
            }

            return EzRadarChartData<string>.Create(axes);
        }
    }

    /// <summary>Maps one SR4 pattern snapshot to chart skill axis values (wire ids from <see cref="EzSunnySrSkillAxis"/>).</summary>
    internal static class EzSunnySrSkillValues
    {
        public static IReadOnlyDictionary<string, double> FromSnapshot(EzManiaSR4PatternSnapshot snapshot)
        {
            var values = new Dictionary<string, double>(EzSunnySrSkillAxisExtensions.ChartAxes.Length, StringComparer.Ordinal);

            foreach (var axis in EzSunnySrSkillAxisExtensions.ChartAxes)
                values[axis.ToMetaId()] = compute(axis, snapshot);

            return values;
        }

        private static double compute(EzSunnySrSkillAxis axis, EzManiaSR4PatternSnapshot snapshot)
            => axis switch
            {
                EzSunnySrSkillAxis.Bracket => EzManiaSR4PatternBracket.Compute(snapshot),
                EzSunnySrSkillAxis.Jack => EzManiaSR4PatternJack.Compute(snapshot),
                EzSunnySrSkillAxis.Burst => EzManiaSR4PatternBurst.Compute(snapshot),
                EzSunnySrSkillAxis.LnHold => EzManiaSR4PatternLongNoteUsage.Compute(snapshot),
                EzSunnySrSkillAxis.LnRelease => EzManiaSR4PatternLongNoteRelease.Compute(snapshot),
                EzSunnySrSkillAxis.Anchor => EzManiaSR4PatternAnchor.Compute(snapshot),
                _ => 0,
            };
    }
}
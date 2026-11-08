// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.LocalProfile;

namespace osu.Game.EzOsuGame.Skills.PPP
{
    /// <summary>
    /// Skill metric. <see cref="MetricId"/> is the last segment of a skill id
    /// (<c>beatmap_ppp.jump_aim</c> → <c>jump_aim</c>) so a future <c>Skill-{metric}</c> filter
    /// can group chart and player rows without hard-coding a system prefix.
    /// </summary>
    public readonly record struct EzSkillSpec(string MetricId, LocalisableString DisplayName, string AccentHex, bool InRadar)
    {
        public string ChartSkillId => EzPPPSkillCatalog.ChartSkillId(MetricId);

        public string PlayerSkillId => EzPPPSkillCatalog.PlayerSkillId(MetricId);

        public static EzSkillSpec Pad(int index) => new EzSkillSpec($"_pad{index}", string.Empty, "#000000", false);
    }

    /// <summary>
    /// PPP skill ids for osu, taiko, catch, and mania. System ids stay
    /// <see cref="EzSkillSystems.BEATMAP_PPP"/> / <see cref="EzSkillSystems.PLAYER_PPP"/>.
    /// </summary>
    public static class EzPPPSkillCatalog
    {
        public const string ACCURACY_DIFFICULTY = "accuracy_difficulty";
        public const string STAR_RATING = "star_rating";
        public const string RHYTHM = "rhythm";
        public const string COLOUR = "colour";
        public const string MOVEMENT = "movement";
        public const string STRAIN = "strain";
        public const string STAMINA = "stamina";

        private static LocalisableString pppName(string zh, string en) => new EzLocalizationManager.EzLocalisableString(zh, en);

        private static readonly EzSkillSpec[] taiko_metrics =
        {
            new EzSkillSpec(RHYTHM, pppName("节奏", "Rhythm"), "#8f6bd8", true),
            new EzSkillSpec(COLOUR, pppName("颜色", "Colour"), "#6f87d8", true),
            new EzSkillSpec(STAMINA, pppName("耐力", "Stamina"), "#ad6b5d", true),
            new EzSkillSpec(STAR_RATING, pppName("星级", "Stars"), "#c9cfdd", false),
        };

        private static readonly EzSkillSpec[] catch_metrics =
        {
            new EzSkillSpec(MOVEMENT, pppName("移动", "Movement"), "#6f87d8", true),
            new EzSkillSpec(STAR_RATING, pppName("星级", "Stars"), "#c9cfdd", false),
        };

        private static readonly EzSkillSpec[] mania_metrics =
        {
            new EzSkillSpec(STRAIN, pppName("密度", "Strain"), "#ec6a9c", true),
            new EzSkillSpec(STAR_RATING, pppName("星级", "Stars"), "#c9cfdd", false),
        };

        private static readonly EzSkillSpec[] osu_metrics = buildOsuMetrics();

        public static bool SupportsRuleset(int rulesetOnlineId) => rulesetOnlineId == EzLocalProfileConstants.OSU_RULESET_ID
                                                                   || rulesetOnlineId == EzLocalProfileConstants.TAIKO_RULESET_ID
                                                                   || rulesetOnlineId == EzLocalProfileConstants.CATCH_RULESET_ID
                                                                   || rulesetOnlineId == EzLocalProfileConstants.MANIA_RULESET_ID;

        public static IReadOnlyList<EzSkillSpec> MetricsFor(int rulesetOnlineId)
        {
            if (rulesetOnlineId == EzLocalProfileConstants.OSU_RULESET_ID)
                return osu_metrics;

            if (rulesetOnlineId == EzLocalProfileConstants.TAIKO_RULESET_ID)
                return taiko_metrics;

            if (rulesetOnlineId == EzLocalProfileConstants.CATCH_RULESET_ID)
                return catch_metrics;

            if (rulesetOnlineId == EzLocalProfileConstants.MANIA_RULESET_ID)
                return mania_metrics;

            return Array.Empty<EzSkillSpec>();
        }

        /// <summary>
        /// Realm slice key. Mania uses key count; other PPP rulesets share <see cref="EzSkillSystems.UNSLICED_KEY"/>.
        /// </summary>
        public static int PlayerSliceKey(int rulesetOnlineId, int keyCount)
        {
            if (rulesetOnlineId == EzLocalProfileConstants.MANIA_RULESET_ID)
                return Math.Max(1, keyCount);

            return EzSkillSystems.UNSLICED_KEY;
        }

        public static string ChartSkillId(string metricId) => $"{EzSkillSystems.BEATMAP_PPP}.{metricId}";

        public static string PlayerSkillId(string metricId) => $"{EzSkillSystems.PLAYER_PPP}.{metricId}";

        public static IReadOnlyDictionary<string, double> ChartZeros(int rulesetOnlineId)
        {
            var result = new Dictionary<string, double>(StringComparer.Ordinal);

            foreach (var spec in MetricsFor(rulesetOnlineId))
                result[spec.ChartSkillId] = 0;

            return result;
        }

        /// <summary>Radar axes for a ruleset. Fewer than three axes are padded with star rating, then blank slots.</summary>
        public static IReadOnlyList<EzSkillSpec> RadarAxes(int rulesetOnlineId)
        {
            var metrics = MetricsFor(rulesetOnlineId);
            var selected = new List<EzSkillSpec>(metrics.Count);

            foreach (var spec in metrics)
            {
                if (spec.InRadar)
                    selected.Add(spec);
            }

            if (selected.Count < 3)
            {
                foreach (var spec in metrics)
                {
                    if (!spec.InRadar && spec.MetricId == STAR_RATING && selected.TrueForAll(s => s.MetricId != STAR_RATING))
                        selected.Add(spec);
                }
            }

            while (selected.Count < 3)
                selected.Add(EzSkillSpec.Pad(selected.Count));

            return selected;
        }

        private static EzSkillSpec[] buildOsuMetrics()
        {
            var axes = EzOsuSkillAxisExtensions.All;
            var specs = new EzSkillSpec[axes.Length + 1];

            for (int i = 0; i < axes.Length; i++)
            {
                var meta = axes[i].Meta();
                specs[i] = new EzSkillSpec(meta.Id, meta.DisplayName, meta.AccentHex, meta.InRadar);
            }

            specs[^1] = new EzSkillSpec(ACCURACY_DIFFICULTY, pppName("节奏准度", "Accuracy difficulty"), "#c59a5c", false);
            return specs;
        }
    }
}

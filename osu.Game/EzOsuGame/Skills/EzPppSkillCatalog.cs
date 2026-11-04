// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.LocalProfile;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// One PPP metric. <see cref="MetricId"/> is the last segment of a skill id
    /// (<c>beatmap_ppplus.jump_aim</c> → <c>jump_aim</c>) so a future <c>Skill-{metric}</c> filter
    /// can group chart and player rows without hard-coding a system prefix.
    /// </summary>
    public readonly record struct EzPppSkillSpec(
        string MetricId,
        string NameZh,
        string NameEn,
        string AccentHex,
        bool InRadar)
    {
        public LocalisableString DisplayName => new EzLocalizationManager.EzLocalisableString(NameZh, NameEn);

        public string ChartSkillId => EzPppSkillCatalog.ChartSkillId(MetricId);

        public string PlayerSkillId => EzPppSkillCatalog.PlayerSkillId(MetricId);
    }

    /// <summary>
    /// PPP skill ids for osu, taiko, catch, and mania. System ids stay
    /// <see cref="EzSkillSystems.BEATMAP_PPPLUS"/> / <see cref="EzSkillSystems.PLAYER_PPPLUS"/>.
    /// </summary>
    public static class EzPppSkillCatalog
    {
        public const int TAIKO_RULESET_ID = 1;
        public const int CATCH_RULESET_ID = 2;

        public const string ACCURACY_DIFFICULTY = "accuracy_difficulty";
        public const string STAR_RATING = "star_rating";
        public const string RHYTHM = "rhythm";
        public const string COLOUR = "colour";
        public const string MOVEMENT = "movement";
        public const string STRAIN = "strain";

        private static readonly EzPppSkillSpec[] taiko_metrics =
        {
            new EzPppSkillSpec(RHYTHM, "节奏", "Rhythm", "#8f6bd8", true),
            new EzPppSkillSpec(COLOUR, "颜色", "Colour", "#6f87d8", true),
            new EzPppSkillSpec("stamina", "耐力", "Stamina", "#ad6b5d", true),
            new EzPppSkillSpec(STAR_RATING, "星级", "Stars", "#c9cfdd", false),
        };

        private static readonly EzPppSkillSpec[] catch_metrics =
        {
            new EzPppSkillSpec(MOVEMENT, "移动", "Movement", "#6f87d8", true),
            new EzPppSkillSpec(STAR_RATING, "星级", "Stars", "#c9cfdd", false),
        };

        private static readonly EzPppSkillSpec[] mania_metrics =
        {
            new EzPppSkillSpec(STRAIN, "密度", "Strain", "#ec6a9c", true),
            new EzPppSkillSpec(STAR_RATING, "星级", "Stars", "#c9cfdd", false),
        };

        public static bool SupportsRuleset(int rulesetOnlineId)
            => rulesetOnlineId == EzLocalProfileConstants.OSU_RULESET_ID
               || rulesetOnlineId == TAIKO_RULESET_ID
               || rulesetOnlineId == CATCH_RULESET_ID
               || rulesetOnlineId == EzLocalProfileConstants.MANIA_RULESET_ID;

        public static IReadOnlyList<EzPppSkillSpec> MetricsFor(int rulesetOnlineId)
        {
            if (rulesetOnlineId == EzLocalProfileConstants.OSU_RULESET_ID)
                return osuMetrics();

            if (rulesetOnlineId == TAIKO_RULESET_ID)
                return taiko_metrics;

            if (rulesetOnlineId == CATCH_RULESET_ID)
                return catch_metrics;

            if (rulesetOnlineId == EzLocalProfileConstants.MANIA_RULESET_ID)
                return mania_metrics;

            return Array.Empty<EzPppSkillSpec>();
        }

        public static IReadOnlyList<string> RequiredChartSkillIds(int rulesetOnlineId)
        {
            var metrics = MetricsFor(rulesetOnlineId);
            var ids = new string[metrics.Count];

            for (int i = 0; i < metrics.Count; i++)
                ids[i] = metrics[i].ChartSkillId;

            return ids;
        }

        public static bool IsCompleteChart(int rulesetOnlineId, IReadOnlyDictionary<string, double>? skills)
        {
            if (skills == null || skills.Count == 0 || !SupportsRuleset(rulesetOnlineId))
                return false;

            foreach (string id in RequiredChartSkillIds(rulesetOnlineId))
            {
                if (!skills.ContainsKey(id))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Realm slice key. Mania uses key count; other PPP rulesets share <see cref="EzSkillSystems.OSU_SLICE_KEY"/>.
        /// </summary>
        public static int PlayerSliceKey(int rulesetOnlineId, int keyCount)
        {
            if (rulesetOnlineId == EzLocalProfileConstants.MANIA_RULESET_ID)
                return Math.Max(1, keyCount);

            return EzSkillSystems.OSU_SLICE_KEY;
        }

        public static string ChartSkillId(string metricId)
            => $"{EzSkillSystems.BEATMAP_PPPLUS}.{metricId}";

        public static string PlayerSkillId(string metricId)
            => $"{EzSkillSystems.PLAYER_PPPLUS}.{metricId}";

        public static bool TrySplit(string? skillId, out bool player, out string metricId)
        {
            player = false;
            metricId = string.Empty;

            if (string.IsNullOrEmpty(skillId))
                return false;

            int dot = skillId.LastIndexOf('.');

            if (dot <= 0 || dot >= skillId.Length - 1)
                return false;

            string system = skillId[..dot];
            metricId = skillId[(dot + 1)..];

            if (system == EzSkillSystems.BEATMAP_PPPLUS)
                return true;

            if (system == EzSkillSystems.PLAYER_PPPLUS)
            {
                player = true;
                return true;
            }

            metricId = string.Empty;
            return false;
        }

        public static IReadOnlyDictionary<string, double> ChartZeros(int rulesetOnlineId)
        {
            var result = new Dictionary<string, double>(StringComparer.Ordinal);

            foreach (var spec in MetricsFor(rulesetOnlineId))
                result[spec.ChartSkillId] = 0;

            return result;
        }

        /// <summary>Unique chart and player definitions across every PPP ruleset.</summary>
        public static IReadOnlyList<EzSkillDefinition> CreateDefinitions(EzSkillScope scope)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var list = new List<EzSkillDefinition>();

            foreach (int ruleset in new[] { EzLocalProfileConstants.OSU_RULESET_ID, TAIKO_RULESET_ID, CATCH_RULESET_ID, EzLocalProfileConstants.MANIA_RULESET_ID })
            {
                foreach (var spec in MetricsFor(ruleset))
                {
                    string skillId = scope == EzSkillScope.Player ? spec.PlayerSkillId : spec.ChartSkillId;

                    if (!seen.Add(skillId))
                        continue;

                    list.Add(new EzSkillDefinition(scope == EzSkillScope.Player ? EzSkillSystems.PLAYER_PPPLUS : EzSkillSystems.BEATMAP_PPPLUS, skillId, spec.DisplayName, scope, spec.AccentHex));
                }
            }

            return list;
        }

        /// <summary>Radar axes for a ruleset. Fewer than three axes are padded with star rating, then blank slots.</summary>
        public static IReadOnlyList<EzPppSkillSpec> RadarAxes(int rulesetOnlineId)
        {
            var metrics = MetricsFor(rulesetOnlineId);
            var selected = new List<EzPppSkillSpec>(metrics.Count);

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
                selected.Add(new EzPppSkillSpec($"_pad{selected.Count}", string.Empty, string.Empty, "#000000", false));

            return selected;
        }

        private static EzPppSkillSpec[] osuMetrics()
        {
            var axes = EzOsuSkillAxisExtensions.All;
            var specs = new EzPppSkillSpec[axes.Length + 1];

            for (int i = 0; i < axes.Length; i++)
            {
                var meta = axes[i].Meta();
                specs[i] = new EzPppSkillSpec(meta.Id, meta.NameZh, meta.NameEn, meta.AccentHex, meta.InRadar);
            }

            specs[^1] = new EzPppSkillSpec(ACCURACY_DIFFICULTY, "节奏准度", "Accuracy difficulty", "#c59a5c", false);
            return specs;
        }
    }
}

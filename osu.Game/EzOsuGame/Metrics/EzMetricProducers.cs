// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.EzOsuGame.LocalProfile;
using osu.Game.EzOsuGame.Skills;
using osu.Game.EzOsuGame.Skills.Dan;
using osu.Game.EzOsuGame.Skills.Mina;
using osu.Game.EzOsuGame.Skills.PPP;
using osu.Game.EzOsuGame.Skills.SkillTag;

namespace osu.Game.EzOsuGame.Metrics
{
    /// <summary>
    /// Declares every metric producer Ez knows about, and how they depend on one another.
    /// </summary>
    /// <remarks>
    /// Only a producer that reads another producer's stored rows declares a dependency. Sharing an engine
    /// or an axis vocabulary is not a dependency, so a chart-side bump never retires a player value.
    /// </remarks>
    public static class EzMetricProducers
    {
        public const string OFFICIAL = "official";
        public const string OFFICIAL_PP = "official_pp";
        public const string SSR = "ssr";
        public const string SSR_PP = "ssr_pp";
        public const string PPP = "ppp";
        public const string PPP_PP = "ppp_pp";
        public const string MINA = "mina";
        public const string CSI = "csi";
        public const string DAN = "dan";

        /// <summary>Axis family shared by the chart-side and player-side Mina vectors.</summary>
        public const string MINA_FAMILY = "mina";

        /// <summary>Axis family shared by the chart-side and player-side PP+ vectors.</summary>
        public const string PPP_FAMILY = "ppp";

        public const string SUNNY_FAMILY = "sunny";

        public const string OFFICIAL_FAMILY = "official";

        /// <summary>Axes produced by an engine shared between the chart and player sides.</summary>
        public const string ENGINE_GROUP = "engine";

        /// <summary>Structural features that describe the long-note layout, excluding the hold ratio.</summary>
        public const string LN_STRUCTURE_GROUP = "ln_structure";

        /// <summary>Long-note pattern subtype scores.</summary>
        public const string LN_SUBTYPE_GROUP = "ln_subtype";

        /// <summary>Structural features behind the RC pattern layer.</summary>
        public const string RC_PATTERN_GROUP = "rc_pattern";

        /// <summary>
        /// Axis id of a star-rating axis. Every star-tier producer stores its rating under this same id, so a
        /// reader can compare them without knowing which system produced the value.
        /// </summary>
        public const string STAR_RATING = "star_rating";

        /// <summary>Axis id of a performance-points axis, shared by the performance-tier producers.</summary>
        public const string PERFORMANCE_POINTS = "performance_points";

        public static readonly EzMetricProducerId OFFICIAL_CHART = new EzMetricProducerId(OFFICIAL, EzSkillScope.Beatmap);
        public static readonly EzMetricProducerId OFFICIAL_PERFORMANCE = new EzMetricProducerId(OFFICIAL_PP, EzSkillScope.Beatmap);
        public static readonly EzMetricProducerId SSR_CHART = new EzMetricProducerId(SSR, EzSkillScope.Beatmap);
        public static readonly EzMetricProducerId SSR_PERFORMANCE = new EzMetricProducerId(SSR_PP, EzSkillScope.Beatmap);
        public static readonly EzMetricProducerId PPP_CHART = new EzMetricProducerId(PPP, EzSkillScope.Beatmap);
        public static readonly EzMetricProducerId PPP_PERFORMANCE = new EzMetricProducerId(PPP_PP, EzSkillScope.Beatmap);
        public static readonly EzMetricProducerId PPP_PLAYER = new EzMetricProducerId(PPP, EzSkillScope.Player);
        public static readonly EzMetricProducerId MINA_CHART = new EzMetricProducerId(MINA, EzSkillScope.Beatmap);
        public static readonly EzMetricProducerId MINA_PLAYER = new EzMetricProducerId(MINA, EzSkillScope.Player);
        public static readonly EzMetricProducerId CSI_CHART = new EzMetricProducerId(CSI, EzSkillScope.Beatmap);
        public static readonly EzMetricProducerId DAN_CHART = new EzMetricProducerId(DAN, EzSkillScope.Beatmap);
        public static readonly EzMetricProducerId DAN_PLAYER = new EzMetricProducerId(DAN, EzSkillScope.Player);

        /// <summary>
        /// The full declaration. <paramref name="upstream"/> supplies the two revisions Ez does not own.
        /// </summary>
        public static EzMetricProducerRegistry CreateDefault(IEzMetricUpstreamRevisions upstream)
        {
            ArgumentNullException.ThrowIfNull(upstream);

            return new EzMetricProducerRegistry(new[]
            {
                createOfficialBeatmap(upstream),
                createOfficialPerformance(),
                createSsrBeatmap(upstream),
                createSsrPerformance(),
                createPppBeatmap(),
                createPppPerformance(),
                createPppPlayer(),
                createMinaBeatmap(),
                createMinaPlayer(),
                createCsiBeatmap(),
                createDanBeatmap(),
                createDanPlayer(),
            });
        }

        private static EzMetricProducer createOfficialBeatmap(IEzMetricUpstreamRevisions upstream)
            => new EzMetricProducer(OFFICIAL_CHART, EzMetricTier.Star, EzMetricUnit.Star, static _ => true)
            {
                AxisFamilyId = OFFICIAL_FAMILY,
                RevisionProbe = upstream.OfficialDifficulty,
            };

        private static EzMetricProducer createOfficialPerformance()
            => new EzMetricProducer(OFFICIAL_PERFORMANCE, EzMetricTier.Performance, EzMetricUnit.PerformancePoints, static _ => true)
            {
                AxisFamilyId = OFFICIAL_FAMILY,
                // Computed from the same difficulty attributes, so it goes stale with them.
                DependsOn = new[] { OFFICIAL_CHART },
            };

        private static EzMetricProducer createSsrBeatmap(IEzMetricUpstreamRevisions upstream)
            => new EzMetricProducer(SSR_CHART, EzMetricTier.Star, EzMetricUnit.Star, maniaOnly)
            {
                AxisFamilyId = SUNNY_FAMILY,
                RevisionProbe = upstream.Ssr,
            };

        private static EzMetricProducer createSsrPerformance()
            => new EzMetricProducer(SSR_PERFORMANCE, EzMetricTier.Performance, EzMetricUnit.PerformancePoints, maniaOnly)
            {
                AxisFamilyId = SUNNY_FAMILY,
                DependsOn = new[] { SSR_CHART },
            };

        private static EzMetricProducer createPppBeatmap()
        {
            var groups = new List<EzMetricAxisGroup>();

            foreach (int ruleset in all_rulesets)
            {
                var axes = pppAxesFor(ruleset);

                if (axes.Count == 0)
                    continue;

                int captured = ruleset;
                groups.Add(new EzMetricAxisGroup(ENGINE_GROUP, axes, appliesToRuleset: r => r == captured));
            }

            return new EzMetricProducer(PPP_CHART, EzMetricTier.Skill, EzMetricUnit.SkillPoint, EzPPPSkillCatalog.SupportsRuleset)
            {
                SystemId = EzSkillSystems.BEATMAP_PPP,
                AxisFamilyId = PPP_FAMILY,
                AxisGroups = groups,
                RevisionProbe = EzPPPSkillVersions.ForRuleset,
                SupportsLive = true,
                // Mania keeps its rating engine as the default radar.
                IsDefaultRadarFor = notMania,
            };
        }

        private static EzMetricProducer createPppPerformance()
            => new EzMetricProducer(PPP_PERFORMANCE, EzMetricTier.Performance, EzMetricUnit.PerformancePoints, EzPPPSkillCatalog.SupportsRuleset)
            {
                AxisFamilyId = PPP_FAMILY,
                DependsOn = new[] { PPP_CHART },
            };

        private static EzMetricProducer createPppPlayer()
        {
            var groups = new List<EzMetricAxisGroup>();

            foreach (int ruleset in all_rulesets)
            {
                var axes = pppAxesFor(ruleset);

                if (axes.Count == 0)
                    continue;

                int captured = ruleset;
                groups.Add(new EzMetricAxisGroup(ENGINE_GROUP, axes, appliesToRuleset: r => r == captured));
            }

            return new EzMetricProducer(PPP_PLAYER, EzMetricTier.Skill, EzMetricUnit.SkillPoint, EzPPPSkillCatalog.SupportsRuleset)
            {
                SystemId = EzSkillSystems.PLAYER_PPP,
                Sliced = true,
                AxisFamilyId = PPP_FAMILY,
                AxisGroups = groups,
                // Reads the chart vector before scaling it per play.
                DependsOn = new[] { PPP_CHART },
                RevisionProbe = static _ => EzPPPSkillVersions.FACET_REVISION,
                IsDefaultRadarFor = notMania,
            };
        }

        private static EzMetricProducer createMinaBeatmap()
            => new EzMetricProducer(MINA_CHART, EzMetricTier.Skill, EzMetricUnit.SkillPoint, maniaOnly)
            {
                SystemId = EzSkillSystems.BEATMAP_MINA,
                Sliced = true,
                AxisFamilyId = MINA_FAMILY,
                AxisGroups = new[] { new EzMetricAxisGroup(ENGINE_GROUP, minaAxes()) },
                RevisionProbe = static _ => EzMinaSkillAlgorithm.VERSION,
                SupportsLive = true,
                IsDefaultRadarFor = static _ => true,
            };

        private static EzMetricProducer createMinaPlayer()
            => new EzMetricProducer(MINA_PLAYER, EzMetricTier.Skill, EzMetricUnit.SkillPoint, maniaOnly)
            {
                SystemId = EzSkillSystems.PLAYER_MINA,
                Sliced = true,
                AxisFamilyId = MINA_FAMILY,
                AxisGroups = new[] { new EzMetricAxisGroup(ENGINE_GROUP, minaAxes()) },
                // Re-measures every play with the same engine instead of reading stored chart rows, so it
                // belongs to the same axis family but has no version edge to the chart side.
                RevisionProbe = static _ => EzMinaSkillAlgorithm.VERSION,
                IsDefaultRadarFor = static _ => true,
            };

        private static EzMetricProducer createCsiBeatmap()
        {
            var groups = new List<EzMetricAxisGroup>
            {
                new EzMetricAxisGroup(LN_STRUCTURE_GROUP, lnStructureAxes()),
                new EzMetricAxisGroup(LN_SUBTYPE_GROUP, lnSubtypeAxes()),
                new EzMetricAxisGroup(RC_PATTERN_GROUP, rcPatternAxes()),
            };

            return new EzMetricProducer(CSI_CHART, EzMetricTier.Tags, EzMetricUnit.Count, maniaOnly)
            {
                // Pure chart structure: reads no stored axes, so it is not gated on the Mina stage.
                AxisGroups = groups,
                RevisionProbe = static _ => EzChartSkillInfo.VERSION,
                SupportsLive = true,
            };
        }

        /// <summary>
        /// Axes whose value is drawn as a spoke of the long-note radar.
        /// </summary>
        private static IReadOnlyList<EzMetricAxis> lnStructureAxes()
        {
            var values = EzManiaLNAxisExtensions.StructureAxes;
            var result = new EzMetricAxis[values.Length];

            for (int i = 0; i < values.Length; i++)
                result[i] = toAxis(values[i].Meta(), i);

            return result;
        }

        /// <summary>
        /// Subtype scores that can replace a spoke value instead of being drawn.
        /// </summary>
        private static IReadOnlyList<EzMetricAxis> lnSubtypeAxes()
        {
            var values = EzManiaLNAxisExtensions.Subtypes;
            var result = new EzMetricAxis[values.Length];

            for (int i = 0; i < values.Length; i++)
                result[i] = toAxis(values[i].Meta(), i);

            return result;
        }

        /// <summary>
        /// Axes drawn on the pattern radar; the long-note tags have their own layer.
        /// </summary>
        private static IReadOnlyList<EzMetricAxis> rcPatternAxes()
        {
            var values = EzManiaRCAxisExtensions.PLAYER_RADAR_AXES;
            var result = new EzMetricAxis[values.Length];

            for (int i = 0; i < values.Length; i++)
                result[i] = toAxis(values[i].Meta(), i);

            return result;
        }

        private static EzMetricAxis toAxis(EzSkillMetaAttribute meta, int order)
            => new EzMetricAxis(meta.Id, meta.DisplayName, meta.AccentHex, meta.InRadar, order);

        private static EzMetricProducer createDanBeatmap()
            => new EzMetricProducer(DAN_CHART, EzMetricTier.Dan, EzMetricUnit.DanRank, maniaOnly)
            {
                // Rating axes come from Mina, structure tags from CSI; both are read as stored rows.
                DependsOn = new[] { MINA_CHART, CSI_CHART },
                RevisionProbe = static _ => EzDanAlgorithm.VERSION,
                SupportsLive = true,
            };

        private static EzMetricProducer createDanPlayer()
            => new EzMetricProducer(DAN_PLAYER, EzMetricTier.Dan, EzMetricUnit.DanRank, maniaOnly)
            {
                Sliced = true,
                DependsOn = new[] { DAN_CHART },
                RevisionProbe = static _ => EzDanAlgorithm.VERSION,
            };

        private static readonly int[] all_rulesets =
        {
            EzLocalProfileConstants.OSU_RULESET_ID,
            EzLocalProfileConstants.TAIKO_RULESET_ID,
            EzLocalProfileConstants.CATCH_RULESET_ID,
            EzLocalProfileConstants.MANIA_RULESET_ID,
        };

        private static bool maniaOnly(int rulesetOnlineId)
            => rulesetOnlineId == EzLocalProfileConstants.MANIA_RULESET_ID;

        private static bool notMania(int rulesetOnlineId)
            => rulesetOnlineId != EzLocalProfileConstants.MANIA_RULESET_ID;

        private static IReadOnlyList<EzMetricAxis> pppAxesFor(int rulesetOnlineId)
        {
            var specs = EzPPPSkillCatalog.MetricsFor(rulesetOnlineId);
            var axes = new List<EzMetricAxis>(specs.Count);

            for (int i = 0; i < specs.Count; i++)
            {
                var spec = specs[i];
                axes.Add(new EzMetricAxis(spec.MetricId, spec.DisplayName, spec.AccentHex, spec.InRadar, i));
            }

            return axes;
        }

        private static IReadOnlyList<EzMetricAxis> minaAxes()
        {
            var all = EzMinaSkillAxisExtensions.All;
            var axes = new List<EzMetricAxis>(all.Length);

            for (int i = 0; i < all.Length; i++)
            {
                var meta = all[i].Meta();
                axes.Add(new EzMetricAxis(meta.Id, meta.DisplayName, meta.AccentHex, meta.InRadar, i));
            }

            return axes;
        }
    }
}

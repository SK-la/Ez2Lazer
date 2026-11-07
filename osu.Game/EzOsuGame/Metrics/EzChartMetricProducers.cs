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
    /// Axis group labels. These are structural, not identities: they decide which write replaces which rows and
    /// how a flattened axis list is ordered.
    /// </summary>
    public static class EzMetricAxisGroups
    {
        /// <summary>Axes produced by an engine shared between the chart and player sides.</summary>
        public const string ENGINE = "engine";

        /// <summary>Structural features that describe the long-note layout, excluding the hold ratio.</summary>
        public const string LN_STRUCTURE = "ln_structure";

        /// <summary>Long-note pattern subtype scores.</summary>
        public const string LN_SUBTYPE = "ln_subtype";

        /// <summary>Structural features behind the RC pattern layer.</summary>
        public const string RC_PATTERN = "rc_pattern";
    }

    /// <summary>Ruleset applicability of a declared scope.</summary>
    public static class EzMetricRulesets
    {
        public static readonly int[] ALL =
        {
            EzLocalProfileConstants.OSU_RULESET_ID,
            EzLocalProfileConstants.TAIKO_RULESET_ID,
            EzLocalProfileConstants.CATCH_RULESET_ID,
            EzLocalProfileConstants.MANIA_RULESET_ID,
        };

        public static Func<int, bool> For(EzMetricRulesetScope scope)
        {
            switch (scope)
            {
                case EzMetricRulesetScope.ManiaOnly:
                    return static r => r == EzLocalProfileConstants.MANIA_RULESET_ID;

                case EzMetricRulesetScope.PppSupported:
                    return EzPPPSkillCatalog.SupportsRuleset;

                default:
                    return static _ => true;
            }
        }
    }

    /// <summary>Where each declared revision source reads its number from.</summary>
    public static class EzMetricRevisions
    {
        public static int Probe(EzMetricRevisionSource source, int rulesetOnlineId, IEzMetricUpstreamRevisions? upstream = null)
        {
            upstream ??= EzNullUpstreamRevisions.INSTANCE;

            switch (source)
            {
                case EzMetricRevisionSource.UpstreamOfficialDifficulty:
                    return upstream.OfficialDifficulty(rulesetOnlineId);

                case EzMetricRevisionSource.UpstreamSsr:
                    return upstream.Ssr(rulesetOnlineId);

                case EzMetricRevisionSource.PppBundle:
                    return EzPPPSkillVersions.ForRuleset(rulesetOnlineId);

                case EzMetricRevisionSource.PppFacet:
                    return EzPPPSkillVersions.FACET_REVISION;

                case EzMetricRevisionSource.MinaSkill:
                    return EzMinaSkillAlgorithm.VERSION;

                case EzMetricRevisionSource.ChartSkillInfo:
                    return EzChartSkillInfo.VERSION;

                case EzMetricRevisionSource.DanAlgorithm:
                    return EzDanAlgorithm.VERSION;

                default:
                    return 1;
            }
        }
    }

    /// <summary>
    /// Builds the axis groups of one declared axis source. Both catalogs read the same builder, so a chart vector
    /// and the player vector folded from it always describe the same spokes.
    /// </summary>
    internal static class EzMetricAxisSets
    {
        public static IReadOnlyList<EzMetricAxisGroup> Build(EzMetricAxisSource source)
        {
            switch (source)
            {
                case EzMetricAxisSource.PppCatalog:
                    return pppGroups();

                case EzMetricAxisSource.MinaEngine:
                    return new[] { new EzMetricAxisGroup(EzMetricAxisGroups.ENGINE, minaAxes()) };

                case EzMetricAxisSource.ChartStructure:
                    return new[]
                    {
                        new EzMetricAxisGroup(EzMetricAxisGroups.LN_STRUCTURE, toAxes(EzManiaLNAxisExtensions.StructureAxes)),
                        new EzMetricAxisGroup(EzMetricAxisGroups.LN_SUBTYPE, toAxes(EzManiaLNAxisExtensions.Subtypes)),
                        new EzMetricAxisGroup(EzMetricAxisGroups.RC_PATTERN, toAxes(EzManiaRCAxisExtensions.PlayerRadarAxes)),
                    };

                default:
                    return Array.Empty<EzMetricAxisGroup>();
            }
        }

        private static IReadOnlyList<EzMetricAxisGroup> pppGroups()
        {
            var groups = new List<EzMetricAxisGroup>();

            foreach (int ruleset in EzMetricRulesets.ALL)
            {
                var specs = EzPPPSkillCatalog.MetricsFor(ruleset);

                if (specs.Count == 0)
                    continue;

                var axes = new List<EzMetricAxis>(specs.Count);

                for (int i = 0; i < specs.Count; i++)
                {
                    var spec = specs[i];
                    axes.Add(new EzMetricAxis(spec.MetricId, spec.DisplayName, spec.AccentHex, spec.InRadar, i));
                }

                int captured = ruleset;
                groups.Add(new EzMetricAxisGroup(EzMetricAxisGroups.ENGINE, axes, appliesToRuleset: r => r == captured));
            }

            return groups;
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

        private static IReadOnlyList<EzMetricAxis> toAxes<TEnum>(TEnum[] values)
            where TEnum : struct, Enum
        {
            var result = new EzMetricAxis[values.Length];

            for (int i = 0; i < values.Length; i++)
            {
                var meta = EzEnumMetaCache<TEnum>.Meta(values[i]);
                result[i] = new EzMetricAxis(meta.Id, meta.DisplayName, meta.AccentHex, meta.InRadar, i);
            }

            return result;
        }
    }

    /// <summary>
    /// The chart-side producer catalog: identity, applicability and wiring for everything measured from a chart.
    /// </summary>
    public static class EzChartMetricProducers
    {
        public static EzChartMetricProducer[] All => EzMetricProducerMetaCache<EzChartMetricProducer>.ALL;

        public static EzMetricProducerId Id(this EzChartMetricProducer producer) => new EzMetricProducerId(EzMetricProducerNames.ToId(producer.ToString()));

        public static EzMetricProducerMetaAttribute Meta(this EzChartMetricProducer producer)
            => EzMetricProducerMetaCache<EzChartMetricProducer>.Meta(producer);

        public static EzMetricProducerRegistry CreateRegistry(IEzMetricUpstreamRevisions? upstream = null)
        {
            var producers = new List<EzMetricProducer>(All.Length);

            foreach (var value in All)
                producers.Add(EzMetricProducerFactory.Build(value.Id(), value.Meta(), upstream, All));

            return new EzMetricProducerRegistry(producers);
        }
    }

    /// <summary>
    /// The player-side producer catalog. A player vector folds the score archive, so nothing here is measured on
    /// demand; the rows exist only after the manual score analysis.
    /// </summary>
    public static class EzPlayerMetricProducers
    {
        public static EzPlayerMetricProducer[] All => EzMetricProducerMetaCache<EzPlayerMetricProducer>.ALL;

        public static EzMetricProducerId Id(this EzPlayerMetricProducer producer) => new EzMetricProducerId(EzMetricProducerNames.ToId(producer.ToString()) + EzMetricProducerNames.PLAYER_SUFFIX);

        public static EzMetricProducerMetaAttribute Meta(this EzPlayerMetricProducer producer)
            => EzMetricProducerMetaCache<EzPlayerMetricProducer>.Meta(producer);

        public static EzMetricProducerRegistry CreateRegistry()
        {
            var producers = new List<EzMetricProducer>(All.Length);

            foreach (var value in All)
                producers.Add(EzMetricProducerFactory.Build(value.Id(), value.Meta(), null, All));

            return new EzMetricProducerRegistry(producers);
        }
    }

    /// <summary>
    /// Turns one declaration into a producer. Shared by both catalogs so tier, family and axis wiring are applied
    /// the same way on either side.
    /// </summary>
    internal static class EzMetricProducerFactory
    {
        public static EzMetricProducer Build<TEnum>(EzMetricProducerId id, EzMetricProducerMetaAttribute meta,
                                                    IEzMetricUpstreamRevisions? upstream, TEnum[] siblings)
            where TEnum : struct, Enum
        {
            return new EzMetricProducer(id, meta.Tier, meta.Unit, EzMetricRulesets.For(meta.Rulesets))
            {
                SystemId = meta.SystemId,
                Family = meta.Family,
                Sliced = meta.Sliced,
                AxisGroups = EzMetricAxisSets.Build(meta.Axes),
                DependsOn = dependenciesOf(meta),
                RevisionProbe = ruleset => EzMetricRevisions.Probe(meta.Revision, ruleset, upstream),
                IsDefaultRadarFor = ruleset => meta.DefaultRadar
                                               && EzMetricRulesets.For(meta.Rulesets)(ruleset)
                                               && isHighestPriority(meta, ruleset, siblings),
                RatingSystem = meta.RatingSystem,
                RatingField = meta.RatingField,
            };
        }

        /// <summary>
        /// Resolves competing default-radar declarations by priority instead of a per-ruleset table: mania prefers
        /// its own rating engine, every other ruleset takes the PP+ vector.
        /// </summary>
        private static bool isHighestPriority<TEnum>(EzMetricProducerMetaAttribute meta, int rulesetOnlineId, TEnum[] siblings)
            where TEnum : struct, Enum
        {
            if (!meta.DefaultRadar)
                return false;

            foreach (var sibling in siblings)
            {
                var other = metaOf(sibling);

                if (!other.DefaultRadar || other.RadarPriority <= meta.RadarPriority)
                    continue;

                if (EzMetricRulesets.For(other.Rulesets)(rulesetOnlineId))
                    return false;
            }

            return true;
        }

        private static EzMetricProducerMetaAttribute metaOf<TEnum>(TEnum value)
            where TEnum : struct, Enum
            => EzMetricProducerMetaCache<TEnum>.Meta(value);

        private static IReadOnlyList<EzMetricProducerId> dependenciesOf(EzMetricProducerMetaAttribute meta)
        {
            int count = meta.ChartDependsOn.Length + meta.PlayerDependsOn.Length;

            if (count == 0)
                return Array.Empty<EzMetricProducerId>();

            var result = new List<EzMetricProducerId>(count);

            foreach (var dependency in meta.ChartDependsOn)
                result.Add(dependency.Id());

            foreach (var dependency in meta.PlayerDependsOn)
                result.Add(dependency.Id());

            return result;
        }
    }
}

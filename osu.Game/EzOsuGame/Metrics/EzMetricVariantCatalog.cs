// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.LocalProfile;

namespace osu.Game.EzOsuGame.Metrics
{
    /// <summary>
    /// The variants a ruleset can be read out under, in display order.
    /// </summary>
    /// <remarks>
    /// A ruleset only offers the variants every one of whose producers applies to it, so the same table
    /// covers mania (three) and the other modes (two) without a per-mode branch here.
    /// </remarks>
    public sealed class EzMetricVariantCatalog
    {
        public const string OFFICIAL = "official";
        public const string SUNNY = "sunny";
        public const string PPP = "ppp";

        private readonly EzMetricRevisionResolver resolver;

        public EzMetricVariantCatalog(EzMetricRevisionResolver resolver)
        {
            ArgumentNullException.ThrowIfNull(resolver);
            this.resolver = resolver;
        }

        /// <summary>
        /// Official stars and pp, drawn with the skill vector the ruleset already uses as its radar. Outside
        /// mania that is PP+, so the family has to be PP+ — the star axis is a scalar and does not join it.
        /// </summary>
        private static readonly EzMetricVariant official = new EzMetricVariant(
            OFFICIAL,
            EzSettingsStrings.PANEL_VARIANT_OFFICIAL,
            EzMetricFamily.Ppp,
            EzChartMetricProducer.Official.Id(),
            EzChartMetricProducer.OfficialPerformance.Id(),
            EzChartMetricProducer.Ppp.Id(),
            EzPlayerMetricProducer.Ppp.Id());

        private static readonly EzMetricVariant sunny = new EzMetricVariant(
            SUNNY,
            EzSettingsStrings.PANEL_VARIANT_SUNNY,
            EzMetricFamily.Sunny,
            EzChartMetricProducer.Ssr.Id(),
            EzChartMetricProducer.SsrPerformance.Id(),
            EzChartMetricProducer.Ssr.Id());

        private static readonly EzMetricVariant ppp = new EzMetricVariant(
            PPP,
            EzSettingsStrings.PANEL_VARIANT_PPP,
            EzMetricFamily.Ppp,
            EzChartMetricProducer.Ppp.Id(),
            EzChartMetricProducer.PppPerformance.Id(),
            EzChartMetricProducer.Ppp.Id(),
            EzPlayerMetricProducer.Ppp.Id());

        /// <summary>
        /// Mania reads its skill layer from its own rating engine, so the official variant pairs that chart vector
        /// with the player vector folded from it instead of PP+ (which mania also has, but not as its radar).
        /// </summary>
        private static readonly EzMetricVariant official_mania = new EzMetricVariant(
            OFFICIAL,
            EzSettingsStrings.PANEL_VARIANT_MINA,
            EzMetricFamily.Mina,
            EzChartMetricProducer.Official.Id(),
            EzChartMetricProducer.OfficialPerformance.Id(),
            EzChartMetricProducer.Mina.Id(),
            EzPlayerMetricProducer.Mina.Id());

        public IReadOnlyList<EzMetricVariant> For(int rulesetOnlineId)
        {
            var result = new List<EzMetricVariant>(3);

            foreach (var variant in all(rulesetOnlineId))
            {
                if (applies(variant, rulesetOnlineId))
                    result.Add(variant);
            }

            return result;
        }

        public EzMetricVariant? Get(string id, int rulesetOnlineId)
        {
            foreach (var variant in all(rulesetOnlineId))
            {
                if (string.Equals(variant.Id, id, StringComparison.Ordinal) && applies(variant, rulesetOnlineId))
                    return variant;
            }

            return null;
        }

        /// <summary>First variant the ruleset supports; null when it has no analysis at all.</summary>
        public EzMetricVariant? DefaultFor(int rulesetOnlineId)
        {
            foreach (var variant in all(rulesetOnlineId))
            {
                if (applies(variant, rulesetOnlineId))
                    return variant;
            }

            return null;
        }

        private static IEnumerable<EzMetricVariant> all(int rulesetOnlineId)
        {
            if (rulesetOnlineId == EzLocalProfileConstants.MANIA_RULESET_ID)
            {
                yield return official_mania;
                yield return sunny;
                yield return ppp;

                yield break;
            }

            yield return official;
            yield return ppp;
        }

        private bool applies(EzMetricVariant variant, int rulesetOnlineId)
        {
            if (!applies(variant.StarProducer, rulesetOnlineId))
                return false;

            if (variant.PerformanceProducer is { } performance && !applies(performance, rulesetOnlineId))
                return false;

            if (variant.ChartSkillProducer is { } chart && !applies(chart, rulesetOnlineId))
                return false;

            if (variant.PlayerSkillProducer is { } player && !applies(player, rulesetOnlineId))
                return false;

            return true;
        }

        private bool applies(EzMetricProducerId id, int rulesetOnlineId)
        {
            var producer = resolver.ChartRegistry.Get(id) ?? resolver.PlayerRegistry.Get(id);
            return producer != null && producer.AppliesToRuleset(rulesetOnlineId);
        }

        /// <summary>
        /// Whether the variant's skill vectors can be drawn on one radar: both sides must belong to the
        /// declared family, otherwise the two colours measure unrelated quantities.
        /// </summary>
        public bool IsPairable(EzMetricVariant variant)
        {
            if (variant.ChartSkillProducer == null || variant.PlayerSkillProducer == null)
                return false;

            return producer(variant.ChartSkillProducer.Value)?.Family == variant.Family
                   && producer(variant.PlayerSkillProducer.Value)?.Family == variant.Family;
        }

        private EzMetricProducer? producer(EzMetricProducerId id)
            => resolver.ChartRegistry.Get(id) ?? resolver.PlayerRegistry.Get(id);
    }
}

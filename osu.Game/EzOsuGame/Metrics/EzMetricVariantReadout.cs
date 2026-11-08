// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Analysis;

namespace osu.Game.EzOsuGame.Metrics
{
    /// <summary>
    /// A variant's star rating together with its performance points.
    /// </summary>
    /// <remarks>
    /// The two are always read and applied as one value. A star without its pp (or the reverse) describes two
    /// different systems on the same surface, which is the mismatch this type exists to make impossible.
    /// Either half may be null on its own: the tiers degrade independently (see the read order in
    /// <see cref="EzMetricVariantReadout.Read"/>).
    /// </remarks>
    public readonly record struct EzMetricVariantValues(double? Star, double? PerformancePoints)
    {
        public static readonly EzMetricVariantValues Empty = new EzMetricVariantValues(null, null);

        public bool IsEmpty => Star == null && PerformancePoints == null;

        /// <summary>A live pair, or <see cref="Empty"/> when the source has neither half.</summary>
        /// <remarks>
        /// An unresolved <see cref="StarDifficulty"/> carries <c>Stars == 0</c> and no performance attributes, which
        /// must read as "absent" so the caller falls back to the stored baseline instead of showing a zero.
        /// </remarks>
        public static EzMetricVariantValues Live(in StarDifficulty star) => new EzMetricVariantValues(star.Stars > 0 ? Finite(star.Stars) : null, Finite(star.PerformanceAttributes?.Total));

        internal static double? Finite(double? value) => value is { } actual && double.IsFinite(actual) && actual >= 0 ? actual : null;
    }

    /// <summary>
    /// Reads a variant's scalar tiers as a pair out of the <see cref="BeatmapInfo"/> baseline columns.
    /// </summary>
    /// <remarks>
    /// The columns are the L1 authority: one star and one pp column per system, written by the background backfill.
    /// Selecting a different variant therefore needs no new storage and no new consumer code, only a mapping here.
    /// Values the backfill has not reached yet are <c>-1</c> and read as null.
    /// <para>
    /// A caller that already holds a higher-tier pair (the live official difficulty, or the live Sunny rating)
    /// passes it in and the baseline is used only for the half the live pair does not have. Reading one half live
    /// and the other from a different system is not expressible through this API.
    /// </para>
    /// </remarks>
    public static class EzMetricVariantReadout
    {
        private static EzMetricProducerRegistry? catalogRegistry;
        private static EzMetricVariantCatalog? catalogInstance;

        /// <summary>
        /// The catalog over the registry in force. Cached per registry instance so sorting and grouping can ask per
        /// beatmap without allocating, and so a reconfigured registry cannot leave a stale catalog behind.
        /// </summary>
        public static EzMetricVariantCatalog Catalog
        {
            get
            {
                var registry = EzAnalysisRevision.Registry;

                if (!ReferenceEquals(registry, catalogRegistry))
                {
                    catalogRegistry = registry;
                    catalogInstance = new EzMetricVariantCatalog(registry);
                }

                return catalogInstance!;
            }
        }

        /// <summary>The system <paramref name="rulesetOnlineId"/> currently reads out under, or null when it has none.</summary>
        public static EzMetricVariant? Selected(int rulesetOnlineId) => EzMetricSelection.Primary(rulesetOnlineId, Catalog);

        /// <summary>
        /// The selected system's pair for <paramref name="beatmap"/>, chosen by the beatmap's own ruleset.
        /// </summary>
        public static EzMetricVariantValues SelectedValues(BeatmapInfo beatmap)
        {
            ArgumentNullException.ThrowIfNull(beatmap);
            return Read(beatmap, Selected(beatmap.Ruleset.OnlineID));
        }

        public static EzMetricVariantValues Read(BeatmapInfo beatmap, EzMetricVariant? variant) => Read(beatmap, variant, EzMetricVariantValues.Empty);

        public static EzMetricVariantValues Read(BeatmapInfo beatmap, EzMetricVariant? variant, in EzMetricVariantValues live)
        {
            ArgumentNullException.ThrowIfNull(beatmap);

            if (variant == null)
                return EzMetricVariantValues.Empty;

            double? star = live.Star ?? baseline(beatmap, variant.StarProducer);
            double? performance = variant.PerformanceProducer is { } producer
                ? live.PerformancePoints ?? baseline(beatmap, producer)
                : null;

            return new EzMetricVariantValues(star, performance);
        }

        /// <summary>Whether the variant's star is the official rating, which the official pill already shows.</summary>
        public static bool IsOfficial(EzMetricVariant variant)
        {
            ArgumentNullException.ThrowIfNull(variant);
            return variant.StarProducer == EzMetricProducers.OFFICIAL_CHART;
        }

        private static double? baseline(BeatmapInfo beatmap, EzMetricProducerId producer)
        {
            double value = (producer.Id, producer.Scope) switch
            {
                (EzMetricProducers.OFFICIAL, Skills.EzSkillScope.Beatmap) => beatmap.StarRating,
                (EzMetricProducers.OFFICIAL_PP, Skills.EzSkillScope.Beatmap) => beatmap.PerformancePoints,
                (EzMetricProducers.SSR, Skills.EzSkillScope.Beatmap) => beatmap.SunnyStarRating,
                (EzMetricProducers.SSR_PP, Skills.EzSkillScope.Beatmap) => beatmap.SunnyPerformancePoints,
                (EzMetricProducers.PPP, Skills.EzSkillScope.Beatmap) => beatmap.PPPStarRating,
                (EzMetricProducers.PPP_PP, Skills.EzSkillScope.Beatmap) => beatmap.PPPPerformancePoints,
                (EzMetricProducers.MINA, Skills.EzSkillScope.Beatmap) => beatmap.MinaRating,
                _ => -1,
            };

            return EzMetricVariantValues.Finite(value);
        }
    }
}

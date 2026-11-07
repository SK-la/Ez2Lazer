// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Skills.Mina;

namespace osu.Game.EzOsuGame.Metrics
{
    /// <summary>
    /// A variant's star rating together with its performance points.
    /// </summary>
    /// <remarks>
    /// The two are always read and applied as one value. A star without its pp (or the reverse) describes two
    /// different systems on the same surface, which is the mismatch this type exists to make impossible.
    /// Either half may be null on its own: the tiers degrade independently (see the read order in
    /// </remarks>
    public readonly record struct EzMetricVariantValues(double? Star, double? PerformancePoints, double? SkillStar = null)
    {
        public static readonly EzMetricVariantValues Empty = new EzMetricVariantValues(null, null);

        public bool IsEmpty => Star == null && PerformancePoints == null && SkillStar == null;

        /// <summary>A live pair, or <see cref="Empty"/> when the source has neither half.</summary>
        /// <remarks>
        /// An unresolved <see cref="StarDifficulty"/> carries <c>Stars == 0</c> and no performance attributes, which
        /// must read as "absent" so the caller falls back to the stored baseline instead of showing a zero.
        /// </remarks>
        public static EzMetricVariantValues Live(in StarDifficulty star)
            => new EzMetricVariantValues(star.Stars > 0 ? Finite(star.Stars) : null, Finite(star.PerformanceAttributes?.Total));

        internal static double? Finite(double? value)
            => value is { } actual && double.IsFinite(actual) && actual >= 0 ? actual : null;

        /// <summary>
        /// A rating is only a value when it is above zero. The per-system columns are pre-populated by schema
        /// defaults, so a zero there means "never computed", not "rated zero".
        /// </summary>
        internal static double? Positive(double? value)
            => value is { } actual && double.IsFinite(actual) && actual > 0 ? actual : null;
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
        private static EzMetricRevisionResolver? catalogResolver;
        private static EzMetricVariantCatalog? catalogInstance;

        /// <summary>
        /// The catalog over the registries in force. Cached per resolver instance so sorting and grouping can ask per
        /// beatmap without allocating, and so a reconfigured registry cannot leave a stale catalog behind.
        /// </summary>
        public static EzMetricVariantCatalog Catalog
        {
            get
            {
                var resolver = EzMetricRegistries.Current;

                if (!ReferenceEquals(resolver, catalogResolver))
                {
                    catalogResolver = resolver;
                    catalogInstance = new EzMetricVariantCatalog(resolver);
                }

                return catalogInstance!;
            }
        }

        /// <summary>The system <paramref name="rulesetOnlineId"/> currently reads out under, or null when it has none.</summary>
        public static EzMetricVariant? Selected(int rulesetOnlineId)
            => EzMetricSelection.Primary(rulesetOnlineId, Catalog);

        /// <summary>
        /// The selected system's pair for <paramref name="beatmap"/>, chosen by the beatmap's own ruleset.
        /// </summary>
        public static EzMetricVariantValues SelectedValues(BeatmapInfo beatmap)
        {
            ArgumentNullException.ThrowIfNull(beatmap);
            return Read(beatmap, Selected(beatmap.Ruleset.OnlineID));
        }

        public static EzMetricVariantValues Read(BeatmapInfo beatmap, EzMetricVariant? variant)
            => Read(beatmap, variant, EzMetricVariantValues.Empty);

        public static EzMetricVariantValues Read(BeatmapInfo beatmap, EzMetricVariant? variant, in EzMetricVariantValues live)
        {
            ArgumentNullException.ThrowIfNull(beatmap);

            if (variant == null)
                return EzMetricVariantValues.Empty;

            double? star = live.Star ?? baseline(beatmap, variant.StarProducer);
            double? performance = variant.PerformanceProducer is { } producer
                ? live.PerformancePoints ?? baseline(beatmap, producer)
                : null;
            double? skillStar = variant.ChartSkillProducer is { } chartProducer
                ? live.SkillStar ?? baseline(beatmap, chartProducer)
                : null;

            return new EzMetricVariantValues(star, performance, skillStar);
        }

        /// <summary>
        /// The chart-skill system's star for this chart: the live axis when it was measured, else the column.
        /// </summary>
        /// <remarks>
        /// This is the star the Ez pill shows beside the official one, which is not always the variant's
        /// <see cref="EzMetricVariant.StarProducer"/> (mania's official variant reads its skill star from Mina).
        /// </remarks>
        public static double? SkillStar(BeatmapInfo beatmap, EzAnalysisResult? analysis)
        {
            ArgumentNullException.ThrowIfNull(beatmap);

            if (Selected(beatmap.Ruleset.OnlineID)?.ChartSkillProducer is not { } producer)
                return null;

            if (analysis is { } live
                && live.AxesOf(producer).TryGetValue(RatingAxisOf(producer), out double measured)
                && double.IsFinite(measured) && measured > 0)
                return measured;

            return baseline(beatmap, producer);
        }

        /// <summary>
        /// The axis a system's headline rating is stored under. Every system writes the shared star id except the
        /// Mina engine, whose vector's headline is its Overall axis.
        /// </summary>
        public static string RatingAxisOf(EzMetricProducerId producer)
            => producer == EzChartMetricProducer.Mina.Id() ? EzMinaSkillAxis.Overall.ToId() : EzMetricAxisIds.STAR_RATING;

        /// <summary>Whether the variant's star is the official rating, which the official pill already shows.</summary>
        public static bool IsOfficial(EzMetricVariant variant)
        {
            ArgumentNullException.ThrowIfNull(variant);
            return variant.StarProducer == EzChartMetricProducer.Official.Id();
        }

        /// <summary>
        /// Reads a system's baseline from the flattened column it owns. Which producer a value belongs to is the
        /// only thing that decides the column, so the mapping is a plain producer switch.
        /// </summary>
        private static double? baseline(BeatmapInfo beatmap, EzMetricProducerId producer)
            => EzMetricVariantValues.Positive(ColumnValue(beatmap, producer));

        /// <summary>
        /// Whether a producer's baseline is persisted as a flattened column on <see cref="BeatmapInfo"/>.
        /// </summary>
        public static bool HasColumn(EzMetricProducerId producer)
            => producer == EzChartMetricProducer.Official.Id()
               || producer == EzChartMetricProducer.OfficialPerformance.Id()
               || producer == EzChartMetricProducer.Ssr.Id()
               || producer == EzChartMetricProducer.SsrPerformance.Id()
               || producer == EzChartMetricProducer.Ppp.Id()
               || producer == EzChartMetricProducer.PppPerformance.Id()
               || producer == EzChartMetricProducer.Mina.Id();

        /// <summary>
        /// The raw column a producer's baseline would be read from, or <c>-1</c> when it owns no column.
        /// </summary>
        /// <remarks>
        /// Exposed so diagnostics can answer "has anything been written here" without knowing which column belongs
        /// to which system; the read path and the coverage readout must not disagree about that mapping.
        /// </remarks>
        public static double ColumnValue(BeatmapInfo beatmap, EzMetricProducerId producer)
        {
            ArgumentNullException.ThrowIfNull(beatmap);

            return producer == EzChartMetricProducer.Official.Id() ? beatmap.StarRating
                : producer == EzChartMetricProducer.OfficialPerformance.Id() ? beatmap.PerformancePoints
                : producer == EzChartMetricProducer.Ssr.Id() ? beatmap.SunnyStarRating
                : producer == EzChartMetricProducer.SsrPerformance.Id() ? beatmap.SunnyPerformancePoints
                : producer == EzChartMetricProducer.Ppp.Id() ? beatmap.PPPStarRating
                : producer == EzChartMetricProducer.PppPerformance.Id() ? beatmap.PPPPerformancePoints
                : producer == EzChartMetricProducer.Mina.Id() ? beatmap.MinaRating
                : -1;
        }
    }
}

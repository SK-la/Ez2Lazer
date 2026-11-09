// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.LocalProfile;
using osu.Game.EzOsuGame.Skills.Mina;

namespace osu.Game.EzOsuGame.Analysis
{
    /// <summary>
    /// A star rating and a performance number as one system reports them for one chart.
    /// </summary>
    public readonly record struct EzReadoutValues(double? Star, double? PerformancePoints, double? SkillStar = null)
    {
        public static readonly EzReadoutValues Empty = new EzReadoutValues(null, null);

        public bool IsEmpty => Star == null && PerformancePoints == null && SkillStar == null;

        /// <summary>A live pair, or <see cref="Empty"/> when the source has neither half.</summary>
        public static EzReadoutValues Live(in StarDifficulty star)
            => new EzReadoutValues(star.Stars > 0 ? Finite(star.Stars) : null, Finite(star.PerformanceAttributes?.Total));

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
    /// One selectable readout for a ruleset: which system's rating it renders, and which system holds the skill
    /// star shown beside the official one.
    /// </summary>
    /// <remarks>
    /// A readout carries no axis vocabulary, no applicability predicate and no revision. It only names two
    /// <see cref="EzAnalysisSystem"/> values; which of them exist for a ruleset is decided by ruleset identity in
    /// <see cref="EzRulesetReadout.All"/>.
    /// </remarks>
    public readonly record struct EzReadout(string Id, LocalisableString Name, EzAnalysisSystem Rating, EzAnalysisSystem SkillRating);

    /// <summary>
    /// The readouts a ruleset offers, and the stored-value reads behind them.
    /// </summary>
    public static class EzRulesetReadout
    {
        public const string OFFICIAL = "official";
        public const string SUNNY = "sunny";
        public const string PPP = "ppp";

        private static readonly EzReadout sunny = new EzReadout(
            SUNNY, EzSettingsStrings.PANEL_VARIANT_SUNNY, EzAnalysisSystem.SSR, EzAnalysisSystem.SSR);

        private static readonly EzReadout ppp = new EzReadout(
            PPP, EzSettingsStrings.PANEL_VARIANT_PPP, EzAnalysisSystem.PPP, EzAnalysisSystem.PPP);

        private static readonly EzReadout official_msd = new EzReadout(
            OFFICIAL, EzSettingsStrings.PANEL_VARIANT_MINA, EzAnalysisSystem.Official, EzAnalysisSystem.Mina);

        private static readonly EzReadout official_ppp = new EzReadout(
            OFFICIAL, EzSettingsStrings.PANEL_VARIANT_OFFICIAL, EzAnalysisSystem.Official, EzAnalysisSystem.PPP);

        private static readonly EzReadout[] mania_readouts = [official_msd, sunny, ppp];

        private static readonly EzReadout[] other_readouts = [official_ppp, ppp];

        /// <summary>Readouts a ruleset offers, in display order. The first one is its default.</summary>
        public static IReadOnlyList<EzReadout> All(int rulesetOnlineId)
            => rulesetOnlineId == EzLocalProfileConstants.MANIA_RULESET_ID ? mania_readouts : other_readouts;

        public static EzReadout? Find(int rulesetOnlineId, string? id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            foreach (var readout in All(rulesetOnlineId))
            {
                if (string.Equals(readout.Id, id, StringComparison.Ordinal))
                    return readout;
            }

            return null;
        }

        /// <summary>The readout this ruleset currently shows, falling back to its first one.</summary>
        public static EzReadout? Selected(int rulesetOnlineId)
        {
            string? stored = EzReadoutSelection.StoredId(rulesetOnlineId);

            return Find(rulesetOnlineId, stored) ?? (All(rulesetOnlineId).Count > 0 ? All(rulesetOnlineId)[0] : null);
        }

        /// <summary>Moves the selection by <paramref name="direction"/> and returns the new readout.</summary>
        public static EzReadout? Cycle(int rulesetOnlineId, int direction = 1)
        {
            var readouts = All(rulesetOnlineId);

            if (readouts.Count == 0)
                return null;

            var current = Selected(rulesetOnlineId);
            int index = 0;

            if (current is { } selected)
            {
                for (int i = 0; i < readouts.Count; i++)
                {
                    if (string.Equals(readouts[i].Id, selected.Id, StringComparison.Ordinal))
                    {
                        index = i;
                        break;
                    }
                }
            }

            index = (((index + direction) % readouts.Count) + readouts.Count) % readouts.Count;
            var next = readouts[index];
            EzReadoutSelection.Store(rulesetOnlineId, next.Id);
            return next;
        }

        /// <summary>The selected readout's values for <paramref name="beatmap"/>.</summary>
        public static EzReadoutValues SelectedValues(BeatmapInfo beatmap)
        {
            ArgumentNullException.ThrowIfNull(beatmap);

            return Selected(beatmap.Ruleset.OnlineID) is { } readout
                ? Read(beatmap, readout)
                : EzReadoutValues.Empty;
        }

        public static EzReadoutValues Read(BeatmapInfo beatmap, EzReadout readout)
            => Read(beatmap, readout, EzReadoutValues.Empty);

        public static EzReadoutValues Read(BeatmapInfo beatmap, EzReadout readout, in EzReadoutValues live)
        {
            ArgumentNullException.ThrowIfNull(beatmap);

            double? star = live.Star ?? EzReadoutValues.Positive(StoredStar(beatmap, readout.Rating));
            double? performance = live.PerformancePoints ?? EzReadoutValues.Positive(StoredPerformance(beatmap, readout.Rating));
            double? skillStar = live.SkillStar is double liveStar
                ? liveSkillStar(readout.SkillRating, liveStar)
                : EzReadoutValues.Positive(StoredStar(beatmap, readout.SkillRating));

            return new EzReadoutValues(star, performance, skillStar);
        }

        /// <summary>
        /// The chart-skill star for this chart: the live axis when it was measured, else the stored column.
        /// </summary>
        public static double? SkillStar(BeatmapInfo beatmap, EzAnalysisResult? analysis)
        {
            ArgumentNullException.ThrowIfNull(beatmap);

            if (Selected(beatmap.Ruleset.OnlineID) is not { } readout || readout.SkillRating == EzAnalysisSystem.None)
                return null;

            if (analysis is { } live && live.RatingOf(readout.SkillRating).StarRating is double measured
                                     && double.IsFinite(measured) && measured > 0)
                return liveSkillStar(readout.SkillRating, measured);

            return EzReadoutValues.Positive(StoredStar(beatmap, readout.SkillRating));
        }

        /// <summary>
        /// The axis the Mina engine's headline sits on. Only Mina has one: every other system's star is a scalar
        /// read through <see cref="EzAnalysisResult.RatingOf"/>.
        /// </summary>
        public static string RatingAxisOf(EzAnalysisSystem system) => EzMinaSkillAxis.Overall.ToId();

        /// <summary>Whether the readout's star is the official rating, which the official pill already shows.</summary>
        public static bool IsOfficial(EzReadout readout) => readout.Rating == EzAnalysisSystem.Official;

        /// <summary>
        /// Reads a system's stored star rating. The official pair lives on the beatmap's own columns, which remain
        /// the authority for upstream code; every other system is stored in <see cref="BeatmapInfo.EzRatingData"/>.
        /// </summary>
        public static double StoredStar(BeatmapInfo beatmap, EzAnalysisSystem system)
        {
            ArgumentNullException.ThrowIfNull(beatmap);

            if (system == EzAnalysisSystem.None)
                return -1;

            if (system == EzAnalysisSystem.Official)
                return beatmap.StarRating;

            return beatmap.EzRatingData.GetStar(system);
        }

        /// <summary>Reads a system's stored perfect-play pp, from the same two places as <see cref="StoredStar"/>.</summary>
        public static double StoredPerformance(BeatmapInfo beatmap, EzAnalysisSystem system)
        {
            ArgumentNullException.ThrowIfNull(beatmap);

            if (system == EzAnalysisSystem.None)
                return -1;

            if (system == EzAnalysisSystem.Official)
                return beatmap.PerformancePoints;

            return beatmap.EzRatingData.GetPerformance(system);
        }

        /// <summary>The live chart-skill axis as the pill prints it: Mina measures in Etterna units.</summary>
        private static double liveSkillStar(EzAnalysisSystem system, double value)
            => system == EzAnalysisSystem.Mina ? EzMinaRatingScale.ToStarRating(value) : value;
    }

    /// <summary>
    /// Which readout each ruleset was last set to, persisted as <c>ruleset=id</c> pairs.
    /// </summary>
    public static class EzReadoutSelection
    {
        private const char pair_separator = ';';
        private const char key_separator = '=';

        public static string? StoredId(int rulesetOnlineId)
        {
            var config = GlobalConfigStore.EzConfig;

            if (config == null)
                return null;

            string raw = config.Get<string>(Ez2Setting.AnalysisSystemSelection);

            if (string.IsNullOrEmpty(raw))
                return null;

            foreach (string pair in raw.Split(pair_separator, StringSplitOptions.RemoveEmptyEntries))
            {
                int separator = pair.IndexOf(key_separator);

                if (separator <= 0)
                    continue;

                if (!int.TryParse(pair.AsSpan(0, separator), NumberStyles.Integer, CultureInfo.InvariantCulture, out int ruleset))
                    continue;

                if (ruleset != rulesetOnlineId)
                    continue;

                string value = pair[(separator + 1)..];
                return string.IsNullOrEmpty(value) ? null : value;
            }

            return null;
        }

        public static void Store(int rulesetOnlineId, string id)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(id);

            var config = GlobalConfigStore.EzConfig;

            if (config == null)
                return;

            config.SetValue(Ez2Setting.AnalysisSystemSelection, write(rulesetOnlineId, id, config.Get<string>(Ez2Setting.AnalysisSystemSelection)));
        }

        private static string write(int rulesetOnlineId, string id, string? existing)
        {
            string key = rulesetOnlineId.ToString(CultureInfo.InvariantCulture);
            var builder = new System.Text.StringBuilder();

            if (!string.IsNullOrEmpty(existing))
            {
                foreach (string pair in existing.Split(pair_separator, StringSplitOptions.RemoveEmptyEntries))
                {
                    int separator = pair.IndexOf(key_separator);

                    if (separator <= 0)
                        continue;

                    if (int.TryParse(pair.AsSpan(0, separator), NumberStyles.Integer, CultureInfo.InvariantCulture, out int ruleset)
                        && ruleset == rulesetOnlineId)
                    {
                        continue;
                    }

                    if (builder.Length > 0)
                        builder.Append(pair_separator);

                    builder.Append(pair);
                }
            }

            if (builder.Length > 0)
                builder.Append(pair_separator);

            builder.Append(key).Append(key_separator).Append(id);

            return builder.ToString();
        }
    }
}

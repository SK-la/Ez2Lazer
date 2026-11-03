// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Localisation;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// One hub <c>PlayerSkillPatternRating</c>: Overall-SSR aggregate on charts tagged with <see cref="Id"/>.
    /// </summary>
    public readonly record struct EzPatternRating(string Id, double Rating, int Plays);

    /// <summary>
    /// One Skills-card axis entry after hub <c>skillModeEntries</c> selection.
    /// </summary>
    public readonly record struct EzSkillModeEntry(
        string SkillId,
        LocalisableString DisplayName,
        double Value,
        string AccentHex);

    /// <summary>Per-axis green/yellow pair on 6/7/8K Skill radar (same algorithm both sides).</summary>
    public readonly record struct EzRadarAxisPair(double Player, double Chart);

    /// <summary>
    /// Hub <c>aggregateModePatternRatings</c> + <c>skillModeEntries</c>
    /// (mania-hub <c>skill-axes.ts</c> / <c>player-skills.ts</c>).
    /// Axis catalog: <see cref="EzPlayerPatternAxis"/>.
    /// </summary>
    public static class EzPatternRatings
    {
        /// <summary>Hub <c>PATTERN_RATING_MIN_PLAYS</c>.</summary>
        public const int MIN_PLAYS = 3;

        /// <summary>Hub display floor in <c>skillModeEntries</c> (<c>value &gt;= 1</c>).</summary>
        public const double DISPLAY_MIN = 1;

        /// <summary>Hub <c>PATTERN_TAG_MIN_SCORE</c> — chart counts toward a pattern only when meaningfully tagged.</summary>
        public const double PATTERN_TAG_MIN_SCORE = 0.5;

        /// <summary>Hub <c>PATTERN_RATING_META</c> order (from <see cref="EzPlayerPatternAxis"/>).</summary>
        public static IReadOnlyList<EzPlayerPatternAxis> Meta { get; } = EzPlayerPatternAxisExtensions.All;

        /// <summary>
        /// Skill radar axes for 6/7/8K: Meta minus <see cref="EzPlayerPatternAxis.Ln"/> (LN is LN-skill radar / DualPanel).
        /// Dual source (4+2): pattern Overall buckets + Mina SSR↔MSD — each axis compares same algorithm.
        /// </summary>
        public static IReadOnlyList<EzPlayerPatternAxis> RadarAxes { get; } =
            Meta.Where(static a => a != EzPlayerPatternAxis.Ln).ToArray();

        public static string ToSkillId(string patternId)
            => $"{EzSkillSystems.PLAYER_PATTERN}.{patternId}";

        public static string ToSkillId(EzPlayerPatternAxis axis) => axis.ToSkillId();

        public static bool TryParseSkillId(string? skillId, out string patternId)
        {
            patternId = string.Empty;

            if (string.IsNullOrEmpty(skillId))
                return false;

            string prefix = EzSkillSystems.PLAYER_PATTERN + ".";
            if (!skillId.StartsWith(prefix, StringComparison.Ordinal))
                return false;

            patternId = skillId[prefix.Length..];
            return patternId.Length > 0;
        }

        /// <summary>
        /// Pattern-Overall axes (player_pattern ↔ chart Overall MSD when tagged):
        /// chordstream / bracket / delay / stream.
        /// Mina axes (SSR ↔ MSD): jack / tech.
        /// </summary>
        public static bool UsesMinaRadarSource(EzPlayerPatternAxis axis)
            => axis is EzPlayerPatternAxis.Jack or EzPlayerPatternAxis.Tech;

        public static EzMinaSkillAxis? ToMinaRadarAxis(EzPlayerPatternAxis axis)
            => axis switch
            {
                EzPlayerPatternAxis.Jack => EzMinaSkillAxis.JackSpeed,
                EzPlayerPatternAxis.Tech => EzMinaSkillAxis.Technical,
                _ => null,
            };

        /// <summary>Hub <c>aggregateModePatternRatings</c>.</summary>
        public static IReadOnlyList<EzPatternRating> AggregateModePatternRatings(
            IEnumerable<(double Overall, IReadOnlyList<string> Patterns)> plays)
        {
            var playsByPattern = new Dictionary<string, List<double>>(StringComparer.Ordinal);

            foreach ((double overall, IReadOnlyList<string> patterns) in plays)
            {
                if (patterns.Count == 0)
                    continue;

                foreach (string pattern in patterns)
                {
                    if (string.IsNullOrWhiteSpace(pattern))
                        continue;

                    if (!playsByPattern.TryGetValue(pattern, out var list))
                        playsByPattern[pattern] = list = new List<double>();

                    list.Add(overall);
                }
            }

            return playsByPattern
                   .Where(static kv => kv.Value.Count >= MIN_PLAYS)
                   .Select(static kv => new EzPatternRating(
                       kv.Key,
                       EzSsrAggregator.Aggregate(kv.Value),
                       kv.Value.Count))
                   .Where(static entry => entry.Rating > 0)
                   .OrderByDescending(static entry => entry.Rating)
                   .ToList();
        }

        /// <summary>
        /// Fixed 6-axis Skill radar labels (no LN): Meta order without re-sort; values unused when Mina-sourced.
        /// </summary>
        public static IReadOnlyList<EzSkillModeEntry> FixedPatternRadarEntries(
            IReadOnlyList<EzPatternRating> patterns)
        {
            var byId = new Dictionary<string, EzPatternRating>(StringComparer.Ordinal);

            foreach (var entry in patterns)
                byId[entry.Id] = entry;

            var result = new List<EzSkillModeEntry>(RadarAxes.Count);

            foreach (var axis in RadarAxes)
            {
                var meta = axis.Meta();
                double value = byId.TryGetValue(meta.Id, out var rating) ? rating.Rating : 0;
                result.Add(new EzSkillModeEntry(
                    axis.ToSkillId(),
                    meta.DisplayName,
                    value,
                    meta.AccentHex));
            }

            return result;
        }

        /// <summary>
        /// Same-algorithm green/yellow for one radar axis.
        /// Pattern axes: player pattern rating ↔ Overall MSD if chart tagged.
        /// Mina axes: player SSR ↔ chart MSD.
        /// </summary>
        public static EzRadarAxisPair ResolveRadarAxisPair(
            EzPlayerPatternAxis axis,
            IReadOnlyDictionary<string, double> msd,
            IReadOnlyDictionary<string, double> ssr,
            IReadOnlyList<EzPatternRating> patterns,
            IReadOnlyDictionary<string, double>? rcPatternScores = null,
            IReadOnlyList<string>? chartPatternTags = null)
        {
            if (UsesMinaRadarSource(axis) && ToMinaRadarAxis(axis) is EzMinaSkillAxis mina)
            {
                double chart = msd.GetValueOrDefault(mina.ToMsdSkillId(), 0);
                double player = ssr.GetValueOrDefault(mina.ToSsrSkillId(), 0);
                if (player < DISPLAY_MIN)
                    player = 0;
                return new EzRadarAxisPair(player, chart);
            }

            string id = axis.ToId();
            double overall = msd.GetValueOrDefault(EzMinaSkillAxis.Overall.ToMsdSkillId(), 0);
            double playerPattern = 0;

            foreach (var rating in patterns)
            {
                if (!string.Equals(rating.Id, id, StringComparison.Ordinal))
                    continue;

                playerPattern = rating.Rating;
                break;
            }

            if (playerPattern < DISPLAY_MIN)
                playerPattern = 0;

            double chartPattern = chartHasPatternTag(id, rcPatternScores, chartPatternTags) && overall > 0
                ? overall
                : 0;

            return new EzRadarAxisPair(playerPattern, chartPattern);
        }

        private static bool chartHasPatternTag(
            string patternId,
            IReadOnlyDictionary<string, double>? rcPatternScores,
            IReadOnlyList<string>? chartPatternTags)
        {
            if (rcPatternScores != null
                && rcPatternScores.TryGetValue(patternId, out double score)
                && double.IsFinite(score)
                && score >= PATTERN_TAG_MIN_SCORE)
            {
                return true;
            }

            if (chartPatternTags == null)
                return false;

            for (int i = 0; i < chartPatternTags.Count; i++)
            {
                if (string.Equals(chartPatternTags[i], patternId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        /// <summary>Hub <c>skillModeEntries</c>.</summary>
        public static IReadOnlyList<EzSkillModeEntry> SkillModeEntries(
            int keyCount,
            IReadOnlyDictionary<string, double> ssrValues,
            IReadOnlyList<EzPatternRating> patterns)
        {
            if (EzDanSkillsetFiling.UsesPatternSkillAxes(keyCount))
            {
                var byId = new Dictionary<string, EzPatternRating>(StringComparer.Ordinal);

                foreach (var entry in patterns)
                    byId[entry.Id] = entry;

                var patternEntries = new List<EzSkillModeEntry>(Meta.Count);

                foreach (var axis in Meta)
                {
                    var meta = axis.Meta();
                    double value = byId.TryGetValue(meta.Id, out var rating) ? rating.Rating : 0;
                    if (value < DISPLAY_MIN)
                        continue;

                    patternEntries.Add(new EzSkillModeEntry(
                        axis.ToSkillId(),
                        meta.DisplayName,
                        value,
                        meta.AccentHex));
                }

                if (patternEntries.Count > 0)
                    return patternEntries.OrderByDescending(static e => e.Value).ToList();
            }

            var entries = new List<EzSkillModeEntry>();

            foreach (var axis in EzMinaSkillAxisExtensions.RadarAxes)
            {
                double value = ssrValues.GetValueOrDefault(axis.ToSsrSkillId(), 0);
                if (value < DISPLAY_MIN)
                    continue;

                var chip = axis.Chip();
                entries.Add(new EzSkillModeEntry(
                    axis.ToSsrSkillId(),
                    chip.Name,
                    value,
                    chip.AccentHex));
            }

            // Hub: graft LN pattern axis onto non-pattern keymodes (same Overall-SSR scale).
            var ln = patterns.FirstOrDefault(static p => p.Id == EzPlayerPatternAxis.Ln.ToId());

            if (ln.Rating >= DISPLAY_MIN)
            {
                var lnMeta = EzPlayerPatternAxis.Ln.Meta();
                entries.Add(new EzSkillModeEntry(
                    EzPlayerPatternAxis.Ln.ToSkillId(),
                    lnMeta.DisplayName,
                    ln.Rating,
                    lnMeta.AccentHex));
            }

            return entries.OrderByDescending(static e => e.Value).ToList();
        }
    }
}

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

        /// <summary>Hub <c>PATTERN_RATING_META</c> order (from <see cref="EzPlayerPatternAxis"/>).</summary>
        public static IReadOnlyList<EzPlayerPatternAxis> Meta { get; } = EzPlayerPatternAxisExtensions.All;

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
        /// Fixed pattern-axis set for song-select / HUD Skill radar (6/7/8K):
        /// full <see cref="Meta"/> order, including zeros — does not filter or re-sort by player.
        /// </summary>
        public static IReadOnlyList<EzSkillModeEntry> FixedPatternRadarEntries(
            IReadOnlyList<EzPatternRating> patterns)
        {
            var byId = new Dictionary<string, EzPatternRating>(StringComparer.Ordinal);

            foreach (var entry in patterns)
                byId[entry.Id] = entry;

            var result = new List<EzSkillModeEntry>(Meta.Count);

            foreach (var axis in Meta)
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

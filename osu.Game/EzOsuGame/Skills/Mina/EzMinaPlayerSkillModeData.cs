// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Skills.Dan;
using osu.Game.EzOsuGame.Skills.SkillTag;

namespace osu.Game.EzOsuGame.Skills.Mina
{
    /// <summary>
    /// One player SkillTag bucket: per-play Overall aggregate on charts tagged with <see cref="SkillId"/>.
    /// </summary>
    public readonly record struct EzMinaPlayerSkillTagPlayData(string SkillId, double SkillRating, int PlayCount);

    /// <summary>
    /// One Skills-card axis entry after hub <c>skillModeEntries</c> selection.
    /// </summary>
    public readonly record struct EzMinaPlayerSkillModeEntry(string SkillId, LocalisableString DisplayName, double Value, string AccentHex);

    /// <summary>Per-axis green/yellow pair on 6/7/8K Skill radar (same algorithm both sides).</summary>
    public readonly record struct EzMinaPlayerChartAxisPair(double Player, double Chart);

    /// <summary>
    /// Hub <c>aggregateModeSkillRatings</c> + <c>skillModeEntries</c>
    /// (mania-hub <c>skill-axes.ts</c> / <c>player-skills.ts</c>).
    /// Axis catalog: <see cref="EzManiaRCAxis"/>.
    /// </summary>
    public static class EzMinaPlayerSkillModeData
    {
        /// <summary>
        /// Revision stamped on the player pattern rows this aggregation produces.
        /// </summary>
        /// <remarks>
        /// Bump when the play-to-pattern fold changes: the minimum-play gate, the aggregation, or the tag filter.
        /// </remarks>
        public const int VERSION = 20240501;

        /// <summary>Hub <c>PATTERN_RATING_MIN_PLAYS</c>.</summary>
        public const int MIN_PLAYS = 3;

        /// <summary>Hub display floor in <c>skillModeEntries</c> (<c>value &gt;= 1</c>).</summary>
        public const double DISPLAY_MIN = 1;

        /// <summary>Hub <c>PATTERN_TAG_MIN_SCORE</c> — chart counts toward a pattern only when meaningfully tagged.</summary>
        public const double SKILL_TAG_MIN_SCORE = 0.5;

        /// <summary>
        /// Hub <c>PATTERN_RATING_META</c> order for 6/7/8K Track and pattern skill radar.
        /// Dual source (4+2): pattern Overall buckets + player rating↔chart Mina — each axis compares same algorithm family.
        /// LN chart select uses <see cref="EzLnSkillRadar"/>; <see cref="EzManiaRCAxis.Ln"/> stays a Tag only.
        /// </summary>
        public static IReadOnlyList<EzManiaRCAxis> PlayerRcAxes { get; } = EzManiaRCAxisExtensions.PLAYER_RC_AXES;

        public static string ToSkillId(string skillId) => skillId;

        public static string ToSkillId(EzManiaRCAxis axis) => axis.ToId();

        public static bool TryParseSkillId(string? skillId, out string skillID)
        {
            skillID = string.Empty;

            if (string.IsNullOrEmpty(skillId))
                return false;

            skillID = skillId;
            return skillID.Length > 0;
        }

        /// <summary>
        /// Pattern-Overall axes (player_pattern ↔ chart Overall Mina when tagged):
        /// chordstream / bracket / delay / stream.
        /// Mina axes (player rating ↔ chart Mina): jack / tech.
        /// </summary>
        public static bool UsesMinaRadarSource(EzManiaRCAxis axis)
            => axis is EzManiaRCAxis.Jack or EzManiaRCAxis.Tech;

        public static EzMinaSkillAxis? ToMinaRadarAxis(EzManiaRCAxis axis)
            => axis switch
            {
                EzManiaRCAxis.Jack => EzMinaSkillAxis.JackSpeed,
                EzManiaRCAxis.Tech => EzMinaSkillAxis.Technical,
                _ => null,
            };

        /// <summary>Hub <c>aggregateModeSkillRatings</c>.</summary>
        public static IReadOnlyList<EzMinaPlayerSkillTagPlayData> AggregateModeSkillRatings(
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
                   .Select(static kv => new EzMinaPlayerSkillTagPlayData(
                       kv.Key,
                       EzMinaPlayerSkillMath.Aggregate(kv.Value),
                       kv.Value.Count))
                   .Where(static entry => entry.SkillRating > 0)
                   .OrderByDescending(static entry => entry.SkillRating)
                   .ToList();
        }

        /// <summary>
        /// Fixed RC skill radar labels: <see cref="PlayerRcAxes"/> order without re-sort; values unused when Mina-sourced.
        /// </summary>
        public static IReadOnlyList<EzMinaPlayerSkillModeEntry> FixedPatternRadarEntries(
            IReadOnlyList<EzMinaPlayerSkillTagPlayData> patterns)
        {
            var byId = new Dictionary<string, EzMinaPlayerSkillTagPlayData>(StringComparer.Ordinal);

            foreach (var entry in patterns)
                byId[entry.SkillId] = entry;

            var result = new List<EzMinaPlayerSkillModeEntry>(PlayerRcAxes.Count);

            foreach (var axis in PlayerRcAxes)
            {
                var meta = axis.Meta();
                double value = byId.TryGetValue(meta.Id, out var rating) ? rating.SkillRating : 0;
                result.Add(new EzMinaPlayerSkillModeEntry(
                    axis.ToId(),
                    meta.DisplayName,
                    value,
                    meta.AccentHex));
            }

            return result;
        }

        /// <summary>
        /// Same-algorithm green/yellow for one radar axis.
        /// Pattern axes: player pattern rating ↔ Overall Mina if chart tagged.
        /// Mina axes: player rating ↔ chart Mina.
        /// </summary>
        public static EzMinaPlayerChartAxisPair ResolveRadarAxisPair(
            EzManiaRCAxis axis,
            IReadOnlyDictionary<string, double> chartSkillAxis,
            IReadOnlyDictionary<string, double> playerSkillAxis,
            IReadOnlyList<EzMinaPlayerSkillTagPlayData> patterns,
            IReadOnlyDictionary<string, double>? rcPatternScores = null,
            IReadOnlyList<string>? chartPatternTags = null)
        {
            if (UsesMinaRadarSource(axis) && ToMinaRadarAxis(axis) is EzMinaSkillAxis mina)
            {
                double chart = chartSkillAxis.GetValueOrDefault(mina.ToMetaId(), 0);
                double player = playerSkillAxis.GetValueOrDefault(mina.ToMetaId(), 0);
                if (player < DISPLAY_MIN)
                    player = 0;
                return new EzMinaPlayerChartAxisPair(player, chart);
            }

            string id = axis.ToId();
            double overall = chartSkillAxis.GetValueOrDefault(EzMinaSkillAxis.Overall.ToMetaId(), 0);
            double playerPattern = 0;

            foreach (var rating in patterns)
            {
                if (!string.Equals(rating.SkillId, id, StringComparison.Ordinal))
                    continue;

                playerPattern = rating.SkillRating;
                break;
            }

            if (playerPattern < DISPLAY_MIN)
                playerPattern = 0;

            double chartPattern = chartHasPatternTag(id, rcPatternScores, chartPatternTags) && overall > 0
                ? overall
                : 0;

            return new EzMinaPlayerChartAxisPair(playerPattern, chartPattern);
        }

        private static bool chartHasPatternTag(
            string patternId,
            IReadOnlyDictionary<string, double>? rcPatternScores,
            IReadOnlyList<string>? chartPatternTags)
        {
            if (rcPatternScores != null
                && rcPatternScores.TryGetValue(patternId, out double score)
                && double.IsFinite(score)
                && score >= SKILL_TAG_MIN_SCORE)
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
        public static IReadOnlyList<EzMinaPlayerSkillModeEntry> SkillModeEntries(
            int keyCount,
            IReadOnlyDictionary<string, double> ssrValues,
            IReadOnlyList<EzMinaPlayerSkillTagPlayData> patterns)
        {
            if (EzDanSkillsetFiling.UsesPatternSkillAxes(keyCount))
            {
                var byId = new Dictionary<string, EzMinaPlayerSkillTagPlayData>(StringComparer.Ordinal);

                foreach (var entry in patterns)
                    byId[entry.SkillId] = entry;

                var patternEntries = new List<EzMinaPlayerSkillModeEntry>(PlayerRcAxes.Count);

                foreach (var axis in PlayerRcAxes)
                {
                    var meta = axis.Meta();
                    double value = byId.TryGetValue(meta.Id, out var rating) ? rating.SkillRating : 0;
                    if (value < DISPLAY_MIN)
                        continue;

                    patternEntries.Add(new EzMinaPlayerSkillModeEntry(
                        axis.ToId(),
                        meta.DisplayName,
                        value,
                        meta.AccentHex));
                }

                if (patternEntries.Count > 0)
                    return patternEntries.OrderByDescending(static e => e.Value).ToList();
            }

            var entries = new List<EzMinaPlayerSkillModeEntry>();

            foreach (var axis in EzMinaSkillAxisExtensions.RadarAxes)
            {
                double value = ssrValues.GetValueOrDefault(axis.ToMetaId(), 0);
                if (value < DISPLAY_MIN)
                    continue;

                var chip = axis.Chip();
                entries.Add(new EzMinaPlayerSkillModeEntry(
                    axis.ToMetaId(),
                    chip.Name,
                    value,
                    chip.AccentHex));
            }

            // Hub: graft LN pattern axis onto non-pattern keymodes (same per-play rating Overall scale).
            var ln = patterns.FirstOrDefault(static p => p.SkillId == EzManiaRCAxis.Ln.ToId());

            if (ln.SkillRating >= DISPLAY_MIN)
            {
                var lnMeta = EzManiaRCAxis.Ln.Meta();
                entries.Add(new EzMinaPlayerSkillModeEntry(
                    EzManiaRCAxis.Ln.ToId(),
                    lnMeta.DisplayName,
                    ln.SkillRating,
                    lnMeta.AccentHex));
            }

            return entries.OrderByDescending(static e => e.Value).ToList();
        }
    }
}
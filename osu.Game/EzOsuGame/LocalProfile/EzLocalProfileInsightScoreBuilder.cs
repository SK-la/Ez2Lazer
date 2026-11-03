// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using osu.Game.Beatmaps;
using osu.Game.Online.API;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;

namespace osu.Game.EzOsuGame.LocalProfile
{
    /// <summary>
    /// Builds Insight plays from archived drill rows + optional live beatmap metadata (BPM / CS / convert).
    /// Prefers persisted drill insight columns when present to avoid Realm full-table scans.
    /// </summary>
    public static class EzLocalProfileInsightScoreBuilder
    {
        public static IReadOnlyList<EzLocalProfileInsightPlay> Build(
            IEnumerable<EzLocalProfileDrillScoreRow> drillScores,
            BeatmapManager beatmapManager,
            RulesetStore rulesets)
        {
            var rows = drillScores
                       .Where(r => r.RulesetId == EzLocalProfileConstants.MANIA_RULESET_ID && r.PpResolved > 0)
                       .OrderByDescending(r => r.PpResolved)
                       .ThenByDescending(r => r.Date)
                       .ToList();

            var needsLookup = rows.Where(static r => !r.HasInsightMeta).ToList();
            var beatmapsByHash = loadBeatmapsByHash(needsLookup, beatmapManager);
            var rulesetCache = new Dictionary<int, Ruleset?>();

            var plays = new List<EzLocalProfileInsightPlay>(rows.Count);

            foreach (var row in rows)
            {
                BeatmapInfo? beatmap = null;
                if (!row.HasInsightMeta && !string.IsNullOrEmpty(row.BeatmapHash))
                    beatmapsByHash.TryGetValue(row.BeatmapHash, out beatmap);

                var ruleset = getRuleset(row.RulesetId, rulesets, rulesetCache);
                var mods = row.HasInsightMeta ? Array.Empty<Mod>() : EzLocalProfileDrillMods.Resolve(row, ruleset);
                var acronyms = row.HasInsightMeta ? row.ReadModAcronyms() : resolveModAcronyms(row, mods);
                double rate = row.HasInsightMeta
                    ? (row.Rate > 0 && double.IsFinite(row.Rate) ? row.Rate : 1)
                    : resolveRate(mods);
                int keyCount = row.KeyCount > 0
                    ? row.KeyCount
                    : resolveKeyCount(row, beatmap, mods, ruleset);
                bool isConvert = row.HasInsightMeta
                    ? row.IsConvert
                    : beatmap != null && beatmap.Ruleset.OnlineID != EzLocalProfileConstants.MANIA_RULESET_ID;
                double bpm = row.Bpm > 0
                    ? row.Bpm
                    : beatmap is { BPM: > 0 } ? beatmap.BPM : 0;
                int onlineId = beatmap is { OnlineID: > 0 } ? beatmap.OnlineID : 0;

                plays.Add(new EzLocalProfileInsightPlay
                {
                    Row = row,
                    Pp = row.PpResolved,
                    KeyCount = keyCount,
                    BeatmapOnlineId = onlineId,
                    IsConvert = isConvert,
                    Bpm = bpm,
                    Rate = rate,
                    ModAcronyms = acronyms,
                    Date = row.Date,
                    Title = row.Title,
                    Artist = row.Artist,
                    DifficultyName = row.DifficultyName,
                    StarRating = row.StarRating,
                    Rank = row.Rank.ToString(),
                });
            }

            return plays;
        }

        private static Dictionary<string, BeatmapInfo> loadBeatmapsByHash(
            IReadOnlyList<EzLocalProfileDrillScoreRow> rows,
            BeatmapManager beatmapManager)
        {
            var hashes = rows
                         .Select(r => r.BeatmapHash)
                         .Where(h => !string.IsNullOrEmpty(h))
                         .Distinct(StringComparer.Ordinal)
                         .ToList();

            var result = new Dictionary<string, BeatmapInfo>(hashes.Count, StringComparer.Ordinal);

            foreach (string hash in hashes)
            {
                // Indexed lookup — never scan All<BeatmapInfo>().
                var beatmap = beatmapManager.QueryBeatmap(b => b.Hash == hash);
                if (beatmap != null)
                    result[hash] = beatmap;
            }

            return result;
        }

        private static Ruleset? getRuleset(int rulesetId, RulesetStore rulesets, Dictionary<int, Ruleset?> cache)
        {
            if (cache.TryGetValue(rulesetId, out var cached))
                return cached;

            var instance = rulesets.GetRuleset(rulesetId)?.CreateInstance();
            cache[rulesetId] = instance;
            return instance;
        }

        private static IReadOnlyList<string> resolveModAcronyms(EzLocalProfileDrillScoreRow row, Mod[] mods)
        {
            if (mods.Length > 0)
                return mods.Select(m => m.Acronym).Where(a => !string.IsNullOrEmpty(a)).Distinct(StringComparer.Ordinal).ToList();

            if (string.IsNullOrEmpty(row.ModsJson))
                return Array.Empty<string>();

            try
            {
                var apiMods = JsonConvert.DeserializeObject<APIMod[]>(row.ModsJson) ?? Array.Empty<APIMod>();
                return apiMods
                       .Select(m => m.Acronym)
                       .Where(a => !string.IsNullOrEmpty(a))
                       .Distinct(StringComparer.Ordinal)
                       .ToList();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        private static double resolveRate(IEnumerable<Mod> mods)
        {
            double rate = 1;

            foreach (var mod in mods)
            {
                if (mod is ModRateAdjust rateAdjust)
                    rate *= rateAdjust.SpeedChange.Value;
            }

            if (!double.IsFinite(rate) || rate <= 0)
                return 1;

            return Math.Clamp(rate, 0.5, 2.0);
        }

        private static int resolveKeyCount(
            EzLocalProfileDrillScoreRow row,
            BeatmapInfo? beatmap,
            Mod[] mods,
            Ruleset? ruleset)
        {
            var maniaSummary = row.ReadManiaSummary();
            if (maniaSummary.ColumnCounts.Count > 0)
                return maniaSummary.ColumnCounts.Keys.Max() + 1;

            if (beatmap != null)
            {
                try
                {
                    if (ruleset != null)
                    {
                        int fromMods = ruleset.GetVariantForBeatmap(beatmap, mods);
                        if (fromMods > 0)
                            return fromMods;
                    }
                }
                catch
                {
                    // fall through
                }

                int fromCs = (int)Math.Round(beatmap.Difficulty.CircleSize);
                if (fromCs > 0)
                    return fromCs;
            }

            return 0;
        }
    }
}

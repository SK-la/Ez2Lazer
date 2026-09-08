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
    /// Builds Insight plays from archived drill rows + live beatmap metadata (BPM / CS / convert).
    /// </summary>
    public static class EzLocalProfileInsightScoreBuilder
    {
        public static IReadOnlyList<EzLocalProfileInsightPlay> Build(
            IEnumerable<EzLocalProfileDrillScoreRow> drillScores,
            BeatmapManager beatmapManager,
            RulesetStore rulesets)
        {
            var plays = new List<EzLocalProfileInsightPlay>();

            foreach (var row in drillScores
                                .Where(r => r.RulesetId == EzLocalProfileConstants.MANIA_RULESET_ID && r.PpResolved > 0)
                                .OrderByDescending(r => r.PpResolved)
                                .ThenByDescending(r => r.Date))
            {
                var beatmap = !string.IsNullOrEmpty(row.BeatmapHash)
                    ? beatmapManager.QueryBeatmap(b => b.Hash == row.BeatmapHash)
                    : null;

                var mods = EzLocalProfileDrillMods.Resolve(row, rulesets);
                var acronyms = resolveModAcronyms(row, mods);
                double rate = resolveRate(mods);
                int keyCount = resolveKeyCount(row, beatmap, mods, rulesets);
                bool isConvert = beatmap != null && beatmap.Ruleset.OnlineID != EzLocalProfileConstants.MANIA_RULESET_ID;
                double bpm = beatmap is { BPM: > 0 } ? beatmap.BPM : 0;
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
            RulesetStore rulesets)
        {
            var maniaSummary = row.ReadManiaSummary();
            if (maniaSummary.ColumnCounts.Count > 0)
                return maniaSummary.ColumnCounts.Keys.Max() + 1;

            if (beatmap != null)
            {
                try
                {
                    var ruleset = rulesets.GetRuleset(row.RulesetId)?.CreateInstance();

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

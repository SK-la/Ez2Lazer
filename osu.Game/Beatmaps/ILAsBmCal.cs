// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Objects.Types;

namespace osu.Game.Beatmaps
{
    public class ILAsBmCal
    {
        // [Resolved]
        // private IBindable<RulesetInfo> ruleset { get; set; } = null!;
        //
        // [Resolved]
        // private IBindable<IReadOnlyList<Mod>> mods { get; set; } = null!;

        // private readonly BeatmapInfo beatmapInfo;
        // private IBeatmap playableBeatmap = null!;

        // public void CalculateAll(WorkingBeatmap workingBeatmap)
        // {
        //     ILegacyRuleset legacyRuleset = (ILegacyRuleset)ruleset.Value.CreateInstance();
        //
        //     playableBeatmap = workingBeatmap.GetPlayableBeatmap(ruleset.Value, mods.Value);
        //
        //     int keyCount = legacyRuleset.GetKeyCount(beatmapInfo, mods.Value);
        //     // Calculate KPS
        //     var (averageKps, maxKps, kpsList) = GetKps(playableBeatmap);
        //     beatmapInfo.AverageKps = averageKps;
        //     beatmapInfo.MaxKps = maxKps;
        //     beatmapInfo.KpsList = kpsList;
        //
        //     // Calculate Column Note Counts
        //     var columnNoteCounts = GetColumnNoteCounts(playableBeatmap);
        //     beatmapInfo.ColumnNoteCounts = columnNoteCounts;
        //
        //     // Calculate Key Count Text
        //     string keyCountText = GetScratch(playableBeatmap, keyCount);
        //     beatmapInfo.KeyCountText = keyCountText;
        // }
        // public static void CalculateAndStoreKps(IBeatmap beatmap, BeatmapInfo beatmapInfo)
        // {
        //     var (averageKps, maxKps, kpsList) = GetKps(beatmap);
        //     beatmapInfo.AverageKps = averageKps;
        //     beatmapInfo.MaxKps = maxKps;
        //     beatmapInfo.KpsList = kpsList;
        // }
        //
        // public static void CalculateAndStoreColumnNoteCounts(IBeatmap beatmap, BeatmapInfo beatmapInfo)
        // {
        //     var columnNoteCounts = GetColumnNoteCounts(beatmap);
        //     beatmapInfo.ColumnNoteCounts = columnNoteCounts;
        // }
        //
        // public static void CalculateAndStoreKeyCountText(IBeatmap beatmap, int keyCount, BeatmapInfo beatmapInfo)
        // {
        //     string keyCountText = GetScratch(beatmap, keyCount);
        //     beatmapInfo.KeyCountText = keyCountText;
        // }
        public static (double averageKps, double maxKps, List<double> kpsList) GetKps(IBeatmap beatmap)
        {
            var hitObjects = beatmap.HitObjects;
            if (hitObjects.Count == 0)
                return (0, 0, new List<double>());

            double totalDuration = double.Abs(hitObjects.Last().StartTime - hitObjects.First().StartTime) / 1000.0;
            double totalHits = hitObjects.Count;

            double averageKps = totalHits / totalDuration;

            double interval = 60000.0 / (beatmap.BeatmapInfo.BPM);
            List<double> kpsList = new List<double>();

            const double song_start_time = 0;
            double songEndTime = hitObjects.Last().StartTime;

            for (double currentTime = song_start_time; currentTime < songEndTime; currentTime += interval)
            {
                double endTime = currentTime + interval;
                int hitsInInterval = hitObjects.Count(h => h.StartTime >= currentTime && h.StartTime < endTime);

                double kps = hitsInInterval / (interval / 1000.0);
                kpsList.Add(kps);
            }

            double maxKps = kpsList.Max();

            return (averageKps, maxKps, kpsList);
        }

        public static Dictionary<int, int> GetColumnNoteCounts(IBeatmap beatmap)
        {
            var columnNoteCounts = new Dictionary<int, int>();

            foreach (var hitObject in beatmap.HitObjects.OfType<IHasColumn>())
            {
                if (hitObject is IHasDuration)
                    continue;

                if (!columnNoteCounts.TryGetValue(hitObject.Column, out int value))
                {
                    value = 0;
                }

                columnNoteCounts[hitObject.Column] = ++value;
            }

            return columnNoteCounts;
        }

        public static string GetScratch(IBeatmap beatmap, int keyCount)
        {
            var columnNoteCounts = GetColumnNoteCounts(beatmap);
            if (columnNoteCounts.Count == 0)
                return $"[{keyCount}K]";

            var sortedColumns = columnNoteCounts.OrderBy(c => c.Key).ToList();
            int firstColumnNotes = sortedColumns.First().Value;
            int lastColumnNotes = sortedColumns.Last().Value;
            var remainingColumns = sortedColumns.Skip(1).Take(sortedColumns.Count - 2).ToList();

            double averageNotes = remainingColumns.Any() ? remainingColumns.Average(c => c.Value) : 0;
            int maxNotesInRemainingColumns = remainingColumns.Any() ? remainingColumns.Max(c => c.Value) : 0;

            bool isFirstColumnHighSpeed = checkHighSpeedNotes(beatmap, sortedColumns.First().Key);
            bool isLastColumnHighSpeed = checkHighSpeedNotes(beatmap, sortedColumns.Last().Key);

            bool isFirstColumnLow = firstColumnNotes < averageNotes * 0.3 || firstColumnNotes < maxNotesInRemainingColumns / 3;
            bool isLastColumnLow = lastColumnNotes < averageNotes * 0.3 || lastColumnNotes < maxNotesInRemainingColumns / 3;

            string result = $"[{keyCount}K]";

            if (keyCount == 6 || keyCount == 8)
            {
                if (isFirstColumnHighSpeed || isLastColumnHighSpeed)
                {
                    result = $"[{keyCount - 1}K1S]";
                }
                else if (isFirstColumnLow || isLastColumnLow)
                {
                    result = $"[{keyCount - 1}+1K]";
                }
            }
            else if (keyCount >= 7)
            {
                if (isFirstColumnHighSpeed || isLastColumnHighSpeed)
                {
                    result = $"[{keyCount - 2}K2S]";
                }
                else if (isFirstColumnLow || isLastColumnLow)
                {
                    result = $"[{keyCount - 2}+2K]";
                }
            }

            int emptyColumns = columnNoteCounts.Count(c => c.Value == 0);

            if (emptyColumns > 0)
            {
                result = $"[{keyCount - 1}K_{emptyColumns}Null]";
            }

            return result;
        }

        private static bool checkHighSpeedNotes(IBeatmap beatmap, int column)
        {
            var hitObjects = beatmap.HitObjects.OfType<IHasColumn>().Where(h => h.Column == column).ToList();
            if (hitObjects.Count == 0)
                return false;

            var (_, maxKps, kpsList) = GetKps(beatmap);

            double highSpeedThreshold = maxKps / 4;

            return kpsList.Any(kps => kps > highSpeedThreshold);
        }
    }
}

        //
        // public static Dictionary<int, int> GetColumnNoteCounts(WorkingBeatmap workingBeatmap)
        // {
        //     var beatmap = workingBeatmap.BeatmapAfterConverted;
        //     if (beatmap == null)
        //         return new Dictionary<int, int>();
        //
        //     var columnNoteCounts = new Dictionary<int, int>();
        //
        //     foreach (var hitObject in beatmap.HitObjects.OfType<IHasColumn>())
        //     {
        //         if (!columnNoteCounts.TryGetValue(hitObject.Column, out int value))
        //         {
        //             value = 0;
        //         }
        //
        //         columnNoteCounts[hitObject.Column] = ++value;
        //     }
        //
        //     return columnNoteCounts;
        // }
        //
        // public static string GetScratch(WorkingBeatmap beatmap, int keyCount)
        // {
        //     var columnNoteCounts = GetColumnNoteCounts(beatmap);
        //     if (columnNoteCounts.Count == 0)
        //         return $"[{keyCount}K]";
        //
        //     var sortedColumns = columnNoteCounts.OrderBy(kvp => kvp.Value).ToList();
        //     int minColumnNotes1 = sortedColumns[0].Value;
        //     int minColumnNotes2 = sortedColumns.Count > 1 ? sortedColumns[1].Value : int.MaxValue;
        //
        //     double averageNotes = columnNoteCounts.Values.Average();
        //
        //     string result = $"[{keyCount}K]";
        //     Console.WriteLine($"Min Column Notes 1: {minColumnNotes1}, Min Column Notes 2: {minColumnNotes2}, Average Notes: {averageNotes}");
        //
        //     if (minColumnNotes1 < averageNotes * 0.3 && minColumnNotes2 < averageNotes * 0.3)
        //     {
        //         result = $"[{keyCount - 2}K2S]";
        //     }
        //     else if (minColumnNotes1 < averageNotes * 0.3 || minColumnNotes2 < averageNotes * 0.3)
        //     {
        //         result = $"[{keyCount - 1}K1S]";
        //     }
        //
        //     int emptyColumns = columnNoteCounts.Count(kvp => kvp.Value == 0);
        //
        //     if (emptyColumns > 0)
        //     {
        //         result = $"[{keyCount - 1}K_{emptyColumns}Null]";
        //     }
        //
        //     return result;
        // }
        //
        // public static Dictionary<int, int> GetColumnNoteCounts(IBeatmap beatmap)
        // {
        //     // ArgumentNullException.ThrowIfNull(workingBeatmap);
        //     //
        //     // var beatmap = workingBeatmap.BeatmapAfterConverted;
        //     if (beatmap?.HitObjects == null)
        //         return new Dictionary<int, int>();
        //
        //     var columnNoteCounts = new Dictionary<int, int>();
        //
        //     foreach (var hitObject in beatmap.HitObjects.OfType<IHasXPosition>())
        //     {
        //         if (!columnNoteCounts.TryGetValue((int)hitObject.X, out int value))
        //         {
        //             value = 0;
        //         }
        //
        //         columnNoteCounts[(int)hitObject.X] = ++value;
        //     }
        //
        //     return columnNoteCounts;
        // }
        // private static bool checkHighSpeedNotes(IBeatmap beatmap, int column)
        // {
        //     var columnNoteInfos = GetColumnNoteCounts(beatmap);
        //     var columnInfo = columnNoteInfos.FirstOrDefault(c => c.ColumnNumber == column);
        //     if (columnInfo == null)
        //         return false;
        //
        //     var hitObjects = beatmap.HitObjects.OfType<IHasXPosition>()
        //                             .Where(h => h.X == columnInfo.XCoordinate)
        //                             .ToList();
        //     if (hitObjects.Count == 0)
        //         return false;
        //
        //     double totalDuration = hitObjects.Last().StartTime - hitObjects.First().StartTime;
        //     double highSpeedThreshold = totalDuration / 4;
        //
        //     foreach (var hitObject in hitObjects)
        //     {
        //         if (hitObject.StartTime < highSpeedThreshold)
        //             return true;
        //     }
        //
        //     return false;
        // }

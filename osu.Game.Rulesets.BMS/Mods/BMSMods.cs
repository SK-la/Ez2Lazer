// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.BMS.Objects;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Rulesets.BMS.Mods
{
    public class BMSModEasy : ModEasy
    {
        public override LocalisableString Description => "Wider hit windows for a more lenient experience.";
        public override double ScoreMultiplier => 0.5;
    }

    public class BMSModNoFail : ModNoFail
    {
        public override LocalisableString Description => "You can't fail, no matter what.";
        public override double ScoreMultiplier => 0.5;
    }

    public class BMSModHardRock : ModHardRock
    {
        public override LocalisableString Description => "Tighter hit windows for a more challenging experience.";
        public override double ScoreMultiplier => 1.06;
    }

    public class BMSModSuddenDeath : ModSuddenDeath
    {
        public override LocalisableString Description => "Miss and fail.";
    }

    public class BMSModAutoplay : ModAutoplay
    {
        public override LocalisableString Description => "Watch a perfect automated play through the song.";
    }

    public class BMSModRandom : ModRandom, IApplicableAfterBeatmapConversion
    {
        public override LocalisableString Description => "Randomize the lane positions of notes.";

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            Seed.Value ??= osu.Framework.Utils.RNG.Next();

            var rng = new Random((int)Seed.Value);

            foreach (int[] group in BMSStageModHelper.GetRegularLaneGroups(beatmap))
            {
                var shuffledColumns = group.OrderBy(_ => rng.Next()).ToArray();

                foreach (var hitObject in beatmap.HitObjects)
                    randomiseColumn(hitObject, group, shuffledColumns);
            }
        }

        private static void randomiseColumn(HitObject hitObject, int[] group, int[] shuffledColumns)
        {
            if (BMSStageModHelper.HasColumn(hitObject))
            {
                int columnIndex = Array.IndexOf(group, BMSStageModHelper.GetColumn(hitObject));

                if (columnIndex >= 0)
                    BMSStageModHelper.SetColumn(hitObject, shuffledColumns[columnIndex]);
            }

            foreach (var nested in hitObject.NestedHitObjects)
                randomiseColumn(nested, group, shuffledColumns);
        }
    }

    /// <summary>
    /// Horizontal mirror for BMS. Lane groups are split at scratch columns so non-scratch lanes can mirror without moving scratch.
    /// </summary>
    public class BMSModMirror : ModMirror, IApplicableAfterBeatmapConversion
    {
        public override LocalisableString Description => "Flip the playfield horizontally.";

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            foreach (int[] group in BMSStageModHelper.GetRegularLaneGroups(beatmap))
            {
                foreach (var hitObject in beatmap.HitObjects)
                    mirrorColumn(hitObject, group);
            }
        }

        private static void mirrorColumn(HitObject hitObject, int[] group)
        {
            if (BMSStageModHelper.HasColumn(hitObject))
            {
                int columnIndex = Array.IndexOf(group, BMSStageModHelper.GetColumn(hitObject));

                if (columnIndex >= 0)
                    BMSStageModHelper.SetColumn(hitObject, group[group.Length - 1 - columnIndex]);
            }

            foreach (var nested in hitObject.NestedHitObjects)
                mirrorColumn(nested, group);
        }
    }

    /// <summary>
    /// Move 1P scratch from the default left column (0) to the right (7) without mirroring key lanes.
    /// Applies only to native <see cref="BMSHitObject"/> maps: 8 columns, single scratch at column 0.
    /// </summary>
    public class BMSModScratchLaneRight : Mod, IApplicableAfterBeatmapConversion
    {
        public override string Name => "Scratch right";
        public override string Acronym => "SR";
        public override ModType Type => ModType.Conversion;
        public override double ScoreMultiplier => 1;

        public override LocalisableString Description =>
            "Places the scratch lane on the right (7K + 1S). Key order is unchanged; only the scratch column moves.";

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            var topLevel = beatmap.HitObjects.OfType<BMSHitObject>().ToList();
            if (topLevel.Count == 0)
                return;

            int totalColumns = topLevel.Max(h => h.Column) + 1;
            var scratchCols = topLevel.Where(h => h.IsScratch).Select(h => h.Column).Distinct().ToList();

            if (totalColumns != 8 || scratchCols.Count != 1 || scratchCols[0] != 0)
                return;

            foreach (var ho in beatmap.HitObjects)
                remapScratchRightRecursive(ho);
        }

        private static void remapScratchRightRecursive(HitObject hitObject)
        {
            if (hitObject is BMSHitObject bms)
            {
                if (bms.IsScratch)
                    bms.Column = 7;
                else if (bms.Column >= 1 && bms.Column <= 7)
                    bms.Column = bms.Column - 1;
            }

            foreach (var nested in hitObject.NestedHitObjects)
                remapScratchRightRecursive(nested);
        }
    }

    /// <summary>
    /// Splits columns into contiguous segments separated by scratch lanes for mirror/random.
    /// Supports native <see cref="BMSHitObject"/> and mania-converted <see cref="BmsManiaNote"/> charts.
    /// </summary>
    internal static class BMSStageModHelper
    {
        public static IEnumerable<int[]> GetRegularLaneGroups(IBeatmap beatmap)
        {
            var scratchColumns = collectScratchColumns(beatmap);

            int totalColumns = getTotalColumns(beatmap);

            if (scratchColumns.Count == 0)
            {
                if (totalColumns > 0)
                    yield return Enumerable.Range(0, totalColumns).ToArray();

                yield break;
            }

            int start = 0;

            foreach (int scratchColumn in scratchColumns)
            {
                if (scratchColumn > start)
                    yield return Enumerable.Range(start, scratchColumn - start).ToArray();

                start = scratchColumn + 1;
            }

            if (start < totalColumns)
                yield return Enumerable.Range(start, totalColumns - start).ToArray();
        }

        private static List<int> collectScratchColumns(IBeatmap beatmap)
        {
            var set = new HashSet<int>();

            foreach (var ho in beatmap.HitObjects)
                collectScratchColumnsRecursive(ho, set);

            return set.OrderBy(c => c).ToList();
        }

        private static void collectScratchColumnsRecursive(HitObject hitObject, HashSet<int> set)
        {
            if (isScratchObject(hitObject))
                set.Add(GetColumn(hitObject));

            foreach (var nested in hitObject.NestedHitObjects)
                collectScratchColumnsRecursive(nested, set);
        }

        private static int getTotalColumns(IBeatmap beatmap)
        {
            if (beatmap is ManiaBeatmap mb)
                return mb.TotalColumns;

            if (beatmap.HitObjects.Count == 0)
                return 0;

            int max = -1;

            foreach (var ho in beatmap.HitObjects)
                max = Math.Max(max, maxColumnRecursive(ho));

            return max + 1;
        }

        private static int maxColumnRecursive(HitObject hitObject)
        {
            int m = HasColumn(hitObject) ? GetColumn(hitObject) : -1;

            foreach (var nested in hitObject.NestedHitObjects)
                m = Math.Max(m, maxColumnRecursive(nested));

            return m;
        }

        private static bool isScratchObject(HitObject hitObject) =>
            hitObject switch
            {
                BMSHitObject b => b.IsScratch,
                BmsManiaNote n => n.IsScratch,
                BmsManiaHoldNote h => h.IsScratch,
                _ => false,
            };

        public static bool HasColumn(HitObject hitObject) =>
            hitObject is ManiaHitObject or BMSHitObject;

        public static int GetColumn(HitObject hitObject) =>
            hitObject switch
            {
                ManiaHitObject m => m.Column,
                BMSHitObject b => b.Column,
                _ => 0,
            };

        public static void SetColumn(HitObject hitObject, int column)
        {
            switch (hitObject)
            {
                case ManiaHitObject m:
                    m.Column = column;
                    break;

                case BMSHitObject b:
                    b.Column = column;
                    break;
            }
        }
    }
}

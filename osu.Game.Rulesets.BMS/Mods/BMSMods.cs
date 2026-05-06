// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.BMS.Objects;
using osu.Framework.Localisation;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;

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
            var maniaBeatmap = (ManiaBeatmap)beatmap;

            foreach (int[] group in BMSStageModHelper.GetRegularLaneGroups(maniaBeatmap))
            {
                var shuffledColumns = group.OrderBy(_ => rng.Next()).ToArray();

                foreach (ManiaHitObject hitObject in maniaBeatmap.HitObjects.OfType<ManiaHitObject>())
                {
                    int columnIndex = Array.IndexOf(group, hitObject.Column);

                    if (columnIndex >= 0)
                        hitObject.Column = shuffledColumns[columnIndex];
                }
            }
        }
    }

    /// <summary>
    /// Horizontal mirror for BMS. Lane groups are split at scratch columns so non-scratch lanes can mirror without moving scratch
    /// (BMS-specific: preserve scratch while mirroring the key body, or extend later with scratch-only placement modes).
    /// </summary>
    public class BMSModMirror : ModMirror, IApplicableAfterBeatmapConversion
    {
        public override LocalisableString Description => "Flip the playfield horizontally.";

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            var maniaBeatmap = (ManiaBeatmap)beatmap;

            foreach (int[] group in BMSStageModHelper.GetRegularLaneGroups(maniaBeatmap))
            {
                foreach (ManiaHitObject hitObject in maniaBeatmap.HitObjects.OfType<ManiaHitObject>())
                {
                    int columnIndex = Array.IndexOf(group, hitObject.Column);

                    if (columnIndex >= 0)
                        hitObject.Column = group[group.Length - 1 - columnIndex];
                }
            }
        }
    }

    /// <summary>
    /// Splits mania columns into contiguous segments separated by scratch columns (see <see cref="BmsManiaNote.IsScratch"/>).
    /// Used so mirror/random can affect only "本体" key lanes. Scratch column index comes from global BMS→mania mapping.
    /// Future: optional mode to relocate only scratch to far left/right without permuting key columns.
    /// </summary>
    internal static class BMSStageModHelper
    {
        public static IEnumerable<int[]> GetRegularLaneGroups(ManiaBeatmap beatmap)
        {
            List<int> scratchColumns = beatmap.HitObjects.OfType<ManiaHitObject>()
                                             .Where(isScratchObject)
                                             .Select(hitObject => hitObject.Column)
                                             .Distinct()
                                             .OrderBy(column => column)
                                             .ToList();

            if (scratchColumns.Count == 0)
            {
                yield return Enumerable.Range(0, beatmap.TotalColumns).ToArray();
                yield break;
            }

            int start = 0;

            foreach (int scratchColumn in scratchColumns)
            {
                if (scratchColumn > start)
                    yield return Enumerable.Range(start, scratchColumn - start).ToArray();

                start = scratchColumn + 1;
            }

            if (start < beatmap.TotalColumns)
                yield return Enumerable.Range(start, beatmap.TotalColumns - start).ToArray();
        }

        private static bool isScratchObject(ManiaHitObject hitObject) => hitObject switch
        {
            BmsManiaNote note => note.IsScratch,
            BmsManiaHoldNote holdNote => holdNote.IsScratch,
            _ => false,
        };
    }
}

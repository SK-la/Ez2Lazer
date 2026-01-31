// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.BMS.Objects;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.BMS
{
    public class BMSDifficultyCalculator : DifficultyCalculator
    {
        public BMSDifficultyCalculator(IRulesetInfo ruleset, IWorkingBeatmap beatmap)
            : base(ruleset, beatmap)
        {
        }

        protected override DifficultyAttributes CreateDifficultyAttributes(IBeatmap beatmap, Mod[] mods, Skill[] skills, double clockRate)
        {
            // Simple difficulty calculation based on note density
            int noteCount = beatmap.HitObjects.Count;
            double length = beatmap.HitObjects.LastOrDefault()?.StartTime ?? 0;
            double density = length > 0 ? noteCount / (length / 1000.0) : 0;

            return new DifficultyAttributes
            {
                StarRating = density * 0.5, // Very simplified
                MaxCombo = noteCount,
            };
        }

        protected override IEnumerable<DifficultyHitObject> CreateDifficultyHitObjects(IBeatmap beatmap, double clockRate)
        {
            var hitObjects = beatmap.HitObjects.OfType<BMSHitObject>().ToList();

            for (int i = 1; i < hitObjects.Count; i++)
            {
                yield return new DifficultyHitObject(hitObjects[i], hitObjects[i - 1], clockRate, new List<DifficultyHitObject>(), i - 1);
            }
        }

        protected override Skill[] CreateSkills(IBeatmap beatmap, Mod[] mods, double clockRate)
        {
            return System.Array.Empty<Skill>();
        }

        protected override Mod[] DifficultyAdjustmentMods => System.Array.Empty<Mod>();
    }
}

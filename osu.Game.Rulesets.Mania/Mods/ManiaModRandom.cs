// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;
using osu.Game.EzOsuGame.Mods;

namespace osu.Game.Rulesets.Mania.Mods
{
    public class ManiaModRandom : ModRandom, IApplicableToBeatmap
    {
        public override LocalisableString Description => @"Shuffle around the keys!";

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            int seed = EzModSeed.Resolve(Seed);
            var rng = new Random(seed);

            int availableColumns = ((ManiaBeatmap)beatmap).TotalColumns;
            var shuffledColumns = Enumerable.Range(0, availableColumns).OrderBy(_ => rng.Next()).ToList();

            beatmap.HitObjects.OfType<ManiaHitObject>().ForEach(h => h.Column = shuffledColumns[h.Column]);
        }
    }
}

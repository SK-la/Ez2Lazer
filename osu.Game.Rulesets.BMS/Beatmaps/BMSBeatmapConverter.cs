// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.BMS.Objects;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Rulesets.BMS.Beatmaps
{
    /// <summary>
    /// Converts beatmaps for BMS ruleset.
    /// </summary>
    public class BMSBeatmapConverter : BeatmapConverter<BMSHitObject>
    {
        public int TargetColumns { get; set; } = 8; // Default 7K + scratch

        public BMSBeatmapConverter(IBeatmap beatmap, Ruleset ruleset)
            : base(beatmap, ruleset)
        {
            // Determine columns from beatmap
            if (beatmap.HitObjects.OfType<BMSHitObject>().Any())
            {
                TargetColumns = beatmap.HitObjects.OfType<BMSHitObject>().Max(h => h.Column) + 1;
            }
        }

        public override bool CanConvert() => true;

        protected override Beatmap<BMSHitObject> CreateBeatmap()
        {
            var beatmap = new Beatmap<BMSHitObject>();

            return beatmap;
        }

        protected override Beatmap<BMSHitObject> ConvertBeatmap(IBeatmap original, CancellationToken cancellationToken)
        {
            var converted = base.ConvertBeatmap(original, cancellationToken);
            converted.HitObjects = converted.HitObjects.Select(cloneBmsHitObject).ToList();
            return converted;
        }

        protected override IEnumerable<BMSHitObject> ConvertHitObject(HitObject original, IBeatmap beatmap, CancellationToken cancellationToken)
        {
            if (original is BMSHitObject bmsObject)
                yield return cloneBmsHitObject(bmsObject);
        }

        private static BMSHitObject cloneBmsHitObject(BMSHitObject source)
        {
            BMSHitObject clone = source switch
            {
                BMSHoldNote hold => new BMSHoldNote
                {
                    Duration = hold.Duration
                },
                BMSHoldNoteHead => new BMSHoldNoteHead(),
                BMSHoldNoteTail => new BMSHoldNoteTail(),
                BMSHoldNoteBody body => new BMSHoldNoteBody
                {
                    Duration = body.Duration
                },
                BMSNote => new BMSNote(),
                _ => throw new System.ArgumentOutOfRangeException(nameof(source), source.GetType(), @"Unsupported BMS hitobject type"),
            };

            clone.StartTime = source.StartTime;
            clone.Column = source.Column;
            clone.IsScratch = source.IsScratch;
            clone.Samples = source.Samples.ToList();
            return clone;
        }
    }
}

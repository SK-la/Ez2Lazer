// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.LAsEZMania;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Mods.YuLiangSSSMods
{
    public class ManiaModDeleteColumn : Mod, IApplicableToBeatmapConverter, IApplicableAfterBeatmapConversion
    {
        public override string Name => "Delete Column";

        public override string Acronym => "DC";

        public override double ScoreMultiplier => 1;

        public override LocalisableString Description => EzManiaModStrings.DeleteSpace_Description;

        public override IconUsage? Icon => FontAwesome.Solid.Backspace;

        public override ModType Type => ModType.YuLiangSSS_Mod;

        public override bool Ranked => false;

        //[SettingSource("Column", "Select the column you delete.")]
        //public BindableInt Column { get; set; } = new BindableInt(4)
        //{
        //    Precision = 1,
        //    MinValue = 1,
        //    MaxValue = 7
        //};

        public static int TargetColumns = 7;

        public void ApplyToBeatmapConverter(IBeatmapConverter converter)
        {
            var mbc = (ManiaBeatmapConverter)converter;

            float keys = mbc.TotalColumns;

            if (keys != 7)
            {
                return;
            }

            mbc.TargetColumns = TargetColumns;
        }

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            var maniaBeatmap = (ManiaBeatmap)beatmap;

            int keys = (int)maniaBeatmap.Difficulty.CircleSize;

            if (keys != 7)
            {
                return;
            }

            var newObjects = new List<ManiaHitObject>();

            var newColumnObjects = new List<ManiaHitObject>();

            var locations = maniaBeatmap.HitObjects.OfType<Note>().Select(n => (
                                            startTime: n.StartTime,
                                            samples: n.Samples,
                                            column: n.Column,
                                            endTime: n.StartTime,
                                            duration: n.StartTime - n.StartTime
                                        ))
                                        .Concat(maniaBeatmap.HitObjects.OfType<HoldNote>().Select(h => (
                                            startTime: h.StartTime,
                                            samples: h.Samples,
                                            column: h.Column,
                                            endTime: h.EndTime,
                                            duration: h.EndTime - h.StartTime
                                        ))).OrderBy(h => h.startTime).ThenBy(n => n.column).ToList();

            foreach (var note in locations)
            {
                int column = note.column;

                if (column == 3)
                {
                    continue;
                }

                if (note.startTime != note.endTime)
                {
                    newColumnObjects.Add(new HoldNote
                    {
                        Column = column,
                        StartTime = note.startTime,
                        Duration = note.endTime - note.startTime,
                        NodeSamples = [note.samples, Array.Empty<HitSampleInfo>()]
                    });
                }
                else
                {
                    newColumnObjects.Add(new Note
                    {
                        Column = column,
                        StartTime = note.startTime,
                        Samples = note.samples
                    });
                }
            }

            newObjects.AddRange(newColumnObjects);

            maniaBeatmap.HitObjects = newObjects;
        }
    }
}

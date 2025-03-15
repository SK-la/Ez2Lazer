// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Framework.Utils;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Mods.YuLiangSSSMods
{
    public class ManiaModLNLongShortAddition : Mod, IApplicableAfterBeatmapConversion, IHasSeed
    {
        public override string Name => "LN Long & Short";

        public override string Acronym => "LS";

        public override double ScoreMultiplier => 1;

        public override LocalisableString Description => "LN Transformer additional version.";// "From YuLiangSSS' LN Transformer.";

        public override IconUsage? Icon => FontAwesome.Solid.YinYang;

        public override ModType Type => ModType.CustomMod;

        public override bool Ranked => false;

        public readonly int[] DivideNumber = [2, 4, 8, 3, 6, 9, 5, 7, 12, 16, 48, 35, 64];

        public override string SettingDescription => string.Join(", ", new[]
        {
            $"Divide 1/{Divide.Value}",
            $"Percentage {Percentage.Value}%",
            $"Long / Short {LongShort.Value}%",
            $"Original LN {OriginalLN.Value}",
            $"Column Num {SelectColumn.Value}",
            $"Gap {Gap.Value}",
            $"Seed {(Seed.Value == null ? "Null" : Seed.Value)}"
        });

        [SettingSource("Divide", "Use 1/?")]
        public BindableNumber<int> Divide { get; set; } = new BindableInt(4)
        {
            MinValue = 1,
            MaxValue = 16,
            Precision = 1,
        };

        [SettingSource("Percentage", "LN Content")]
        public BindableNumber<int> Percentage { get; set; } = new BindableInt(100)
        {
            MinValue = 0,
            MaxValue = 100,
            Precision = 5,
        };

        [SettingSource("Long / Short %", "The Shape")]
        public BindableNumber<int> LongShort { get; set; } = new BindableInt(40)
        {
            MinValue = 0,
            MaxValue = 100,
            Precision = 5,
        };

        [SettingSource("Original LN", "Original LN won't be converted.")]
        public BindableBool OriginalLN { get; } = new BindableBool(false);

        [SettingSource("Column Num", "Select the number of column to transform(Transform all columns if set to equal or greater than keys).")]
        public BindableInt SelectColumn { get; set; } = new BindableInt(20)
        {
            MinValue = 0,
            MaxValue = 20,
            Precision = 1,
        };

        [SettingSource("Gap", "For changing random columns after transforming the gap's number of notes(set to 0 then the selected columns for transforming will not move).")]
        public BindableInt Gap { get; set; } = new BindableInt(12)
        {
            MinValue = 0,
            MaxValue = 20,
            Precision = 1,
        };

        [SettingSource("Seed", "Use a custom seed instead of a random one.", SettingControlType = typeof(SettingsNumberBox))]
        public Bindable<int?> Seed { get; } = new Bindable<int?>();

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            if (SelectColumn.Value == 0)
            {
                return;
            }
            var maniaBeatmap = (ManiaBeatmap)beatmap;
            var newObjects = new List<ManiaHitObject>();
            var originalLNObjects = new List<ManiaHitObject>();

            Random? Rng;
            Seed.Value ??= RNG.Next();
            Rng = new Random((int)Seed.Value);

            foreach (var column in maniaBeatmap.HitObjects.GroupBy(h => h.Column))
            {
                var newColumnObjects = new List<ManiaHitObject>();
                var locations = column.OfType<Note>().Select(n => (startTime: n.StartTime, samples: n.Samples, endTime: n.StartTime))
                                      .Concat(column.OfType<HoldNote>().SelectMany(h => new[]
                                      {
                                          (startTime: h.StartTime, samples: h.GetNodeSamples(0), endTime: h.EndTime)
                                      }))
                                      .OrderBy(h => h.startTime).ToList();

                for (int i = 0; i < locations.Count - 1; i++)
                {
                    double fullDuration = locations[i + 1].startTime - locations[i].startTime;
                    double beatLength = beatmap.ControlPointInfo.TimingPointAt(locations[i + 1].startTime).BeatLength;
                    double beatBPM = beatmap.ControlPointInfo.TimingPointAt(locations[i + 1].startTime).BPM;
                    double timeDivide = beatLength / Divide.Value; //beatBPM / 60 * 100 / Divide.Value;
                    double duration = Rng.Next(100) < LongShort.Value ? fullDuration - timeDivide : timeDivide;
                    bool flag = true; // Can be transformed to LN

                    if (duration < timeDivide)
                    {
                        duration = timeDivide;
                    }

                    if (duration >= fullDuration - 2)
                    {
                        flag = false;
                    }

                    if (OriginalLN.Value && locations[i].startTime != locations[i].endTime)
                    {
                        newColumnObjects.Add(new HoldNote
                        {
                            Column = column.Key,
                            StartTime = locations[i].startTime,
                            EndTime = locations[i].endTime,
                            NodeSamples = [locations[i].samples, Array.Empty<HitSampleInfo>()]
                        });
                        originalLNObjects.AddNote(locations[i].samples, column.Key, locations[i].startTime, locations[i].endTime);
                    }
                    else if (Rng.Next(100) < Percentage.Value && flag)
                    {
                        newColumnObjects.Add(new HoldNote
                        {
                            Column = column.Key,
                            StartTime = locations[i].startTime,
                            Duration = duration,
                            NodeSamples = [locations[i].samples, Array.Empty<HitSampleInfo>()]
                        });
                    }
                    else
                    {
                        newColumnObjects.Add(new Note
                        {
                            Column = column.Key,
                            StartTime = locations[i].startTime,
                            Samples = locations[i].samples
                        });
                    }
                }

                if (Math.Abs(locations[locations.Count - 1].startTime - locations[locations.Count - 1].endTime) <= 2 || Rng.Next(100) >= Percentage.Value)
                {
                    newColumnObjects.Add(new Note
                    {
                        Column = column.Key,
                        StartTime = locations[locations.Count - 1].startTime,
                        Samples = locations[locations.Count - 1].samples
                    });
                }
                else
                {
                    newColumnObjects.Add(new HoldNote
                    {
                        Column = column.Key,
                        StartTime = locations[locations.Count - 1].startTime,
                        Duration = locations[locations.Count - 1].endTime - locations[locations.Count - 1].startTime,
                        NodeSamples = [locations[locations.Count - 1].samples, Array.Empty<HitSampleInfo>()]
                    });
                }

                newObjects.AddRange(newColumnObjects);
            }

            ManiaModHelper.AfterTransform(newObjects, originalLNObjects, maniaBeatmap, Rng, OriginalLN.Value, Gap.Value, SelectColumn.Value);
        }
    }
}

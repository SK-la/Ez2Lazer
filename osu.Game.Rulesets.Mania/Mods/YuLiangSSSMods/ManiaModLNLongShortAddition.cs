// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Framework.Utils;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Mods.YuLiangSSSMods
{
    public class ManiaModLNLongShortAddition : ManiaModLN, IApplicableAfterBeatmapConversion
    {
        public override string Name => "LN Long & Short";

        public override string Acronym => "LS";

        public override LocalisableString Description => "LN Transformer additional version.";// "From YuLiangSSS' LN Transformer.";

        public readonly int[] DivideNumber = [2, 4, 8, 3, 6, 9, 5, 7, 12, 16, 48, 35, 64];

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                yield return ("Long / Short %", $"{LongShort.Value}%");

                foreach (var (setting, value) in base.SettingDescription)
                    yield return (setting, value);
            }
        }

        [SettingSource("Long / Short %", "The Shape", 0)]
        public BindableNumber<int> LongShort { get; set; } = new BindableInt(40)
        {
            MinValue = 0,
            MaxValue = 100,
            Precision = 5,
        };

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
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

            ManiaModHelper.AfterTransform(newObjects, originalLNObjects, maniaBeatmap, Rng, OriginalLN.Value, Gap.Value, SelectColumn.Value, DurationLimit.Value, LineSpacing.Value, InvertLineSpacing.Value);

            maniaBeatmap.Breaks.Clear();
        }
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
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
using osu.Game.Rulesets.Mania.LAsEZMania;
using osu.Game.Rulesets.Mods;
// using osu.Framework.Logging;

namespace osu.Game.Rulesets.Mania.Mods.YuLiangSSSMods
{
    public class ManiaModLNTransformer : ManiaModLN, IApplicableAfterBeatmapConversion
    {
        public override string Name => "LN Transformer";

        public override string Acronym => "LT";

        public override LocalisableString Description => EzManiaModStrings.LNTransformer_Description;// "From YuLiangSSS' LN Transformer.";

        public readonly double ERROR = 2;

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                yield return ("Level", $"{Level.Value}");

                foreach (var (setting, value) in base.SettingDescription)
                    yield return (setting, value);
            }
        }

        [SettingSource("Level", "LN Transform Level  (-3: Hold Off   -2: Real RandomLN(Random ms)   -1: RandomLN(Random TimingPoint)   0: RegularLN   3: LightLN   5: MediumLN   8: HeavyLN   10: FullLN)", 0)]
        public BindableNumber<int> Level { get; set; } = new BindableInt(3)
        {
            MinValue = -3,
            MaxValue = 10,
            Precision = 1,
        };

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            var maniaBeatmap = (ManiaBeatmap)beatmap;
            int keys = maniaBeatmap.TotalColumns;

            Random? Rng;
            Seed.Value ??= RNG.Next();
            Rng = new Random((int)Seed.Value);

            var newObjects = new List<ManiaHitObject>();
            var oldObjects = maniaBeatmap.HitObjects.ToList();
            var originalLNObjects = new List<ManiaHitObject>();

            if (Level.Value == -3)
            {
                int transformColumnNum = SelectColumn.Value;

                if (transformColumnNum > keys)
                {
                    transformColumnNum = keys;
                }
                var randomColumnSet = ManiaModHelper.SelectRandom(Enumerable.Range(0, keys), Rng, transformColumnNum == 0 ? keys : transformColumnNum).ToHashSet();
                int gap = Gap.Value;

                foreach (var timeGroup in oldObjects.GroupBy(x => x.StartTime))
                {
                    foreach (var note in timeGroup)
                    {
                        if (randomColumnSet.Contains(note.Column) && Rng.Next(100) < Percentage.Value)
                        {
                            newObjects.Add(new Note
                            {
                                Column = note.Column,
                                StartTime = note.StartTime,
                                Samples = note.Samples
                            });
                        }
                        else
                        {
                            newObjects.Add(note);
                        }
                    }

                    gap--;
                    if (gap <= 0)
                    {
                        randomColumnSet = ManiaModHelper.SelectRandom(Enumerable.Range(0, keys), Rng, transformColumnNum).ToHashSet();
                        gap = Gap.Value;
                    }
                }

                maniaBeatmap.HitObjects = newObjects.OrderBy(h => h.StartTime).ToList();
                return;
            }

            foreach (var column in maniaBeatmap.HitObjects.GroupBy(h => h.Column))
            {
                switch (Level.Value)
                {
                    case -2:
                    {
                        TrueRandom(beatmap, newObjects, Rng, column);
                    }
                    break;
                    case -1:
                    {
                        double mu = -1;
                        originalLNObjects = ManiaModHelper.Transform(Rng, mu, 1, Divide.Value, Percentage.Value, ERROR, OriginalLN.Value, beatmap, newObjects, column);
                    }
                    break;
                    case 0:
                    {
                        double mu = 1;
                        originalLNObjects = ManiaModHelper.Transform(Rng, mu, 100, Divide.Value, Percentage.Value, ERROR, OriginalLN.Value, beatmap, newObjects, column);
                    }
                    break;
                    case 1:
                    {
                        double mu = 11; //LN duration μ
                        originalLNObjects = ManiaModHelper.Transform(Rng, mu, 0.85, Divide.Value, Percentage.Value, ERROR, OriginalLN.Value, beatmap, newObjects, column);
                    }
                    break;
                    case 2:
                    {
                        double mu = 22;
                        originalLNObjects = ManiaModHelper.Transform(Rng, mu, 0.85, Divide.Value, Percentage.Value, ERROR, OriginalLN.Value, beatmap, newObjects, column);
                    }
                    break;
                    case 3:
                    {
                        double mu = 33;
                        originalLNObjects = ManiaModHelper.Transform(Rng, mu, 0.85, Divide.Value, Percentage.Value, ERROR, OriginalLN.Value, beatmap, newObjects, column);
                    }
                    break;
                    case 4:
                    {
                        double mu = 44;
                        originalLNObjects = ManiaModHelper.Transform(Rng, mu, 0.85, Divide.Value, Percentage.Value, ERROR, OriginalLN.Value, beatmap, newObjects, column);
                    }
                    break;
                    case 5:
                    {
                        double mu = 55;
                        originalLNObjects = ManiaModHelper.Transform(Rng, mu, 0.85, Divide.Value, Percentage.Value, ERROR, OriginalLN.Value, beatmap, newObjects, column);
                    }
                    break;
                    case 6:
                    {
                        double mu = 66;
                        originalLNObjects = ManiaModHelper.Transform(Rng, mu, 0.85, Divide.Value, Percentage.Value, ERROR, OriginalLN.Value, beatmap, newObjects, column);
                    }
                    break;
                    case 7:
                    {
                        double mu = 77;
                        originalLNObjects = ManiaModHelper.Transform(Rng, mu, 0.85, Divide.Value, Percentage.Value, ERROR, OriginalLN.Value, beatmap, newObjects, column);
                    }
                    break;
                    case 8:
                    {
                        double mu = 88;
                        originalLNObjects = ManiaModHelper.Transform(Rng, mu, 0.9, Divide.Value, Percentage.Value, ERROR, OriginalLN.Value, beatmap, newObjects, column);
                    }
                    break;
                    case 9:
                    {
                        double mu = 99;
                        originalLNObjects = ManiaModHelper.Transform(Rng, mu, 1, Divide.Value, Percentage.Value, ERROR, OriginalLN.Value, beatmap, newObjects, column);
                    }
                    break;
                    case 10:
                    {
                        originalLNObjects = Invert(beatmap, newObjects, Rng, column);
                    }
                    break;
                }
            }

            ManiaModHelper.AfterTransform(newObjects, originalLNObjects, beatmap, Rng, OriginalLN.Value, Gap.Value, SelectColumn.Value, DurationLimit.Value, LineSpacing.Value, InvertLineSpacing.Value);
            maniaBeatmap.Breaks.Clear();
        }

        public List<ManiaHitObject> Invert(IBeatmap beatmap, List<ManiaHitObject> newObjects, Random Rng, IGrouping<int, ManiaHitObject> column)
        {
            var locations = column.OfType<Note>().Select(n => (column: n.Column, startTime: n.StartTime, samples: n.Samples, endTime: n.StartTime))
                                  .Concat(column.OfType<HoldNote>().SelectMany(h => new[]
                                  {
                                          (column: h.Column, startTime: h.StartTime, samples: h.GetNodeSamples(0), endTime: h.EndTime)
                                      //(startTime: h.EndTime, samples: h.GetNodeSamples(1)) Invert Mod Bug
                                  }))
                                  .OrderBy(h => h.startTime).ToList();

            var newColumnObjects = new List<ManiaHitObject>();
            var originalLNObjects = new List<ManiaHitObject>();

            for (int i = 0; i < locations.Count - 1; i++)
            {
                // Full duration of the hold note.
                double fullDuration = locations[i + 1].startTime - locations[i].startTime;
                // Beat length at the end of the hold note.
                double beatLength = beatmap.ControlPointInfo.TimingPointAt(locations[i + 1].startTime).BeatLength;
                bool flag = true;
                double duration = fullDuration - (beatLength / Divide.Value);

                if (duration < beatLength / Divide.Value)
                {
                    duration = beatLength / Divide.Value;
                }

                if (duration > fullDuration - 3)
                {
                    flag = false;
                }

                if (OriginalLN.Value && locations[i].startTime != locations[i].endTime)
                {
                    newColumnObjects.AddNote(locations[i].samples, column.Key, locations[i].startTime, locations[i].endTime);
                    originalLNObjects.AddNote(locations[i].samples, column.Key, locations[i].startTime, locations[i].endTime);
                }
                else if (Rng!.Next(100) < Percentage.Value && flag)
                {
                    newColumnObjects.AddLNByDuration(locations[i].samples, column.Key, locations[i].startTime, duration);
                }
                else
                {
                    newColumnObjects.AddNote(locations[i].samples, column.Key, locations[i].startTime);
                }
            }

            double lastStartTime = locations[locations.Count - 1].startTime;
            double lastEndTime = locations[locations.Count - 1].endTime;
            if (OriginalLN.Value && lastStartTime != lastEndTime)
            {
                newColumnObjects.Add(new HoldNote
                {
                    Column = column.Key,
                    StartTime = locations[locations.Count - 1].startTime,
                    EndTime = locations[locations.Count - 1].endTime,
                    NodeSamples = [locations[locations.Count - 1].samples, Array.Empty<HitSampleInfo>()]
                });
                originalLNObjects.AddNote(locations[locations.Count - 1].samples, column.Key, locations[locations.Count - 1].startTime, locations[locations.Count - 1].endTime);
            }
            else
            {
                newColumnObjects.Add(new Note
                {
                    Column = column.Key,
                    StartTime = locations[locations.Count - 1].startTime,
                    Samples = locations[locations.Count - 1].samples
                });
            }

            newObjects.AddRange(newColumnObjects);

            return originalLNObjects;
        }

        public List<ManiaHitObject> TrueRandom(IBeatmap beatmap, List<ManiaHitObject> newObjects, Random Rng, IGrouping<int, ManiaHitObject> column)
        {
            var locations = column.OfType<Note>().Select(n => (column: n.Column, startTime: n.StartTime, endTime: n.StartTime, samples: n.Samples))
                              .Concat(column.OfType<HoldNote>().SelectMany(h => new[]
                              {
                                          (column: h.Column, startTime: h.StartTime, endTime: h.EndTime, samples: h.GetNodeSamples(0))
                                  //(startTime: h.EndTime, samples: h.GetNodeSamples(1))
                              }))
                              .OrderBy(h => h.startTime).ToList();

            var newColumnObjects = new List<ManiaHitObject>();
            var originalLNObjects = new List<ManiaHitObject>();

            for (int i = 0; i < locations.Count - 1; i++)
            {
                // Full duration of the hold note.
                double fullDuration = locations[i + 1].startTime - locations[i].startTime;

                // Beat length at the end of the hold note.
                // double beatLength = beatmap.ControlPointInfo.TimingPointAt(locations[i + 1].startTime).BeatLength;

                double duration = Rng.Next((int)fullDuration) + Rng.NextDouble();
                while (duration > fullDuration)
                    duration--;
                if (OriginalLN.Value && locations[i].startTime != locations[i].endTime)
                {
                    newColumnObjects.AddNote(locations[i].samples, column.Key, locations[i].startTime, locations[i].endTime);
                    originalLNObjects.AddNote(locations[i].samples, column.Key, locations[i].startTime, locations[i].endTime);
                }
                else if (Rng.Next(100) < Percentage.Value)
                {
                    newColumnObjects.AddLNByDuration(locations[i].samples, column.Key, locations[i].startTime, duration);
                }
                else
                {
                    newColumnObjects.AddNote(locations[i].samples, column.Key, locations[i].startTime);
                }
            }

            newObjects.AddRange(newColumnObjects);

            return originalLNObjects;
        }

        [Obsolete]
        public void Invert(IBeatmap beatmap, List<ManiaHitObject> newObjects, Random Rng, List<(int column, double startTime, IList<HitSampleInfo> samples, double endTime)> locations, List<(double lastStartTime, double lastEndTime, bool lastLN, double thisStartTime, double thisEndTime, bool thisLN)> noteList, List<(IList<HitSampleInfo> lastSample, IList<HitSampleInfo> thisSample)> sampleList, int column)
        {
            double fullDuration = noteList[column].thisStartTime - noteList[column].lastStartTime;

            double beatLength = beatmap.ControlPointInfo.TimingPointAt(noteList[column].thisStartTime).BeatLength;

            bool flag = true;

            double duration = fullDuration - (beatLength / Divide.Value);

            if (duration < beatLength / Divide.Value)
            {
                duration = beatLength / Divide.Value;
            }

            if (duration > fullDuration - 3)
            {
                flag = false;
            }

            if (OriginalLN.Value && noteList[column].lastStartTime != noteList[column].lastEndTime)
            {
                newObjects.AddNote(sampleList[column].lastSample, column, noteList[column].lastStartTime, noteList[column].lastEndTime);
            }
            else if (Rng.Next(100) < Percentage.Value && flag)
            {
                newObjects.AddLNByDuration(sampleList[column].lastSample, column, noteList[column].lastStartTime, duration);
            }
            else
            {
                newObjects.AddNote(sampleList[column].lastSample, column, noteList[column].lastStartTime);
            }
        }

        [Obsolete]
        public void TrueRandomTransform(List<ManiaHitObject> newObjects, Random? Rng, List<(double lastTime, double lastEndTime, bool lastLN, double thisTime, double thisEndTime, bool thisLN)> noteList, List<(IList<HitSampleInfo> lastSample, IList<HitSampleInfo> thisSample)> sampleList, int column, double fullDuration)
        {
            double duration = Rng!.Next((int)fullDuration) + Rng.NextDouble();
            while (duration > fullDuration)
                duration--;

            if (OriginalLN.Value && noteList[column].lastTime != noteList[column].lastEndTime)
            {
                newObjects.AddNote(sampleList[column].lastSample, column, noteList[column].lastTime);
            }
            else if (Rng.Next(100) < Percentage.Value)
            {
                newObjects.AddNote(sampleList[column].lastSample, column, noteList[column].lastTime, noteList[column].lastTime + duration);
            }
            else
            {
                newObjects.AddNote(sampleList[column].lastSample, column, noteList[column].lastTime);
            }
        }
    }
}

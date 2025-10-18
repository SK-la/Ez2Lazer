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
    public class ManiaModNoteAdjust : Mod, IApplicableAfterBeatmapConversion, IHasSeed
    {
        public override string Name => "Note Adjust";

        public override string Acronym => "NA";

        public override LocalisableString Description => "To make more or less note.";

        public override ModType Type => ModType.CustomMod;

        public override IconUsage? Icon => FontAwesome.Solid.Brain;

        public override double ScoreMultiplier => 1;

        public override bool Ranked => false;

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                if (Style.Value != 6)
                {
                    yield return ("Style", $"{Style.Value}");
                    yield return ("Probability", $"{Probability.Value}%");
                    yield return ("Seed", $"{(Seed.Value == null ? "Null" : Seed.Value)}");
                }
                else
                {
                    yield return ("Style", $"{Style.Value}");
                    yield return ("Probability", $"{Probability.Value}%");
                    yield return ("Extremum", $"{Extremum.Value}");
                    yield return ("Comparison Style", $"{ComparisonStyle.Value}");
                    yield return ("Line", $"{Line.Value}");
                    yield return ("Step", $"{Step.Value}");
                    yield return ("Ignore Comparison", $"{IgnoreComparison}");
                    yield return ("Ignore Interval", $"{IgnoreInterval}");
                    yield return ("Seed", $"{(Seed.Value == null ? "Null" : Seed.Value)}");
                }
            }
        }

        [SettingSource("Style", "1: Applicable to Jack Pattern.  2&3: Applicable to Stream Pattern.  4&5: Applicable to Speed Pattern(No Jack).  6: DIY(Will use ↓↓↓ all options) (1~5 will only use ↓ seed option).")]
        public BindableInt Style { get; set; } = new BindableInt(1)
        {
            Precision = 1,
            MinValue = 1,
            MaxValue = 6
        };

        [SettingSource("Probability", "The Probability of increasing note.")]
        public BindableDouble Probability { get; set; } = new BindableDouble(100)
        {
            Precision = 2.5,
            MinValue = -100,
            MaxValue = 100,
        };

        [SettingSource("Extremum", "Depending on how many notes on one line you keep(Available maximum note or minimum note).")]
        public BindableInt Extremum { get; set; } = new BindableInt(10)
        {
            Precision = 1,
            MinValue = 1,
            MaxValue = 10
        };

        [SettingSource("Comparison Style", "1: Dispose a line when this line's note quantity >= Last&Next line. 2: Dispose a line when this line's note quantity <= Last&Next line.")]
        public BindableInt ComparisonStyle { get; set; } = new BindableInt(1)
        {
            Precision = 1,
            MinValue = 1,
            MaxValue = 2
        };

        [SettingSource("Line", "Depending on how heavy about this map(0 is recommended for Jack,  1 is recommended for (Jump/Hand/Etc.)Stream, 2 is recommended for Speed).")]
        public BindableInt Line { get; set; } = new BindableInt(1)
        {
            Precision = 1,
            MinValue = 0,
            MaxValue = 10
        };

        [SettingSource("Step", "Skip \"Step\" line when converting successfully on a line.")]
        public BindableInt Step { get; set; } = new BindableInt(-1)
        {
            Precision = 1,
            MinValue = -1,
            MaxValue = 10
        };

        [SettingSource("Ignore Comparison", "Ignore condition of Comparison.")]
        public BindableBool IgnoreComparison { get; set; } = new BindableBool(false);

        [SettingSource("Ignore Interval", "Ignore interval of note.")]
        public BindableBool IgnoreInterval { get; set; } = new BindableBool(false);

        [SettingSource("Seed", "Use a custom seed instead of a random one", SettingControlType = typeof(SettingsNumberBox))]
        public Bindable<int?> Seed { get; } = new Bindable<int?>();


        // Column Number: 0 to n - 1
        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            if (Probability.Value == 0)
            {
                return;
            }

            Random? Rng;
            Seed.Value ??= RNG.Next();
            Rng = new Random((int)Seed.Value);

            var maniaBeatmap = (ManiaBeatmap)beatmap;

            var newObjects = new List<ManiaHitObject>();

            var newColumnObjects = new List<ManiaHitObject>();

            int keys = maniaBeatmap.TotalColumns;

            switch (Style.Value)
            {
                case 1:
                {
                    if (Probability.Value > 0)
                    {
                        Transform(newColumnObjects, maniaBeatmap, Rng, Probability.Value, 0, keys, 1, keys, -1);
                    }
                    else if (Probability.Value < 0)
                    {
                        Transform(newColumnObjects, maniaBeatmap, Rng, Probability.Value, 0, keys, 1, 1, -1);
                    }
                }
                break;

                case 2:
                {
                    if (Probability.Value > 0)
                    {
                        Transform(newColumnObjects, maniaBeatmap, Rng, Probability.Value, 1, keys, 1, keys, -1);
                    }
                    else if (Probability.Value < 0)
                    {
                        Transform(newColumnObjects, maniaBeatmap, Rng, Probability.Value, 1, keys, 1, 1, -1);
                    }
                }
                break;

                case 3:
                {
                    if (Probability.Value > 0)
                    {
                        Transform(newColumnObjects, maniaBeatmap, Rng, Probability.Value, 1, keys, 2, keys, -1);
                    }
                    else if (Probability.Value < 0)
                    {
                        Transform(newColumnObjects, maniaBeatmap, Rng, Probability.Value, 1, keys, 2, 1, -1);
                    }
                }
                break;

                case 4:
                {
                    if (Probability.Value > 0)
                    {
                        Transform(newColumnObjects, maniaBeatmap, Rng, Probability.Value, 2, keys, 1, keys, -1);
                    }
                    else if (Probability.Value < 0)
                    {
                        Transform(newColumnObjects, maniaBeatmap, Rng, Probability.Value, 2, keys, 1, 1, -1);
                    }
                }
                break;

                case 5:
                {
                    if (Probability.Value > 0)
                    {
                        Transform(newColumnObjects, maniaBeatmap, Rng, Probability.Value, 2, keys, 2, keys, -1);
                    }
                    else if (Probability.Value < 0)
                    {
                        Transform(newColumnObjects, maniaBeatmap, Rng, Probability.Value, 2, keys, 2, 1, -1);
                    }
                }
                break;

                case 6:
                {
                    Transform(newColumnObjects, maniaBeatmap, Rng, Probability.Value, Line.Value, keys, ComparisonStyle.Value, Extremum.Value, Step.Value, IgnoreComparison.Value, IgnoreInterval.Value);
                }
                break;
            }
            newObjects.AddRange(newColumnObjects);

            maniaBeatmap.HitObjects = [.. newObjects.OrderBy(h => h.StartTime)];
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="beatmap"></param>
        /// <param name="Rng"></param>
        /// <param name="probability"></param>
        /// <param name="interval"></param>
        /// <param name="keys"></param>
        /// <param name="compare">If is 1, will convert the line: note greater or equal than Next and Last Line <br/> If is 2, will convert the line: note less or equal than Next and Last Line</param>
        /// <param name="extremum">Maximum or minimum note on a line.</param>
        /// <param name="skipStep">Skip line when converting successfully.</param>
        /// <param name="ignoreComparison"></param>
        /// <param name="ignoreInterval"></param>
        public void Transform(List<ManiaHitObject> obj, ManiaBeatmap beatmap, Random Rng, double probability, int interval, int keys, int compare, int extremum, int skipStep, bool ignoreComparison = false, bool ignoreInterval = false)
        {
            List<int> columnWithNoNote = new List<int>(Enumerable.Range(0, keys));
            List<int> columnWithNote = new List<int>();

            if (interval == 0)
            {
                foreach (var timingPoint in beatmap.HitObjects.GroupBy(h => h.StartTime))
                {
                    var locations = timingPoint.OfType<Note>().Select(n => (column: n.Column, startTime: n.StartTime, endTime: n.StartTime, samples: n.Samples))
                              .Concat(timingPoint.OfType<HoldNote>().SelectMany(h => new[]
                              {(
                                        column: h.Column,
                                        startTime: h.StartTime,
                                        endTime: h.EndTime,
                                        samples: h.GetNodeSamples(0)
                                  )}))
                              .OrderBy(h => h.startTime).ToList();

                    int quantity = timingPoint.Count();

                    foreach (var note in locations)
                    {
                        obj.AddNote(note.samples, note.column, note.startTime, note.endTime);
                        columnWithNoNote.Remove(note.column);
                        columnWithNote.Add(note.column);
                    }

                    columnWithNoNote = columnWithNoNote.ShuffleIndex(Rng).ToList();
                    columnWithNote = columnWithNote.ShuffleIndex(Rng).ToList();

                    if (probability > 0)
                    {
                        foreach (int column in columnWithNoNote)
                        {
                            if (quantity < Extremum.Value && Rng.Next(100) < probability)
                            {
                                if (!InLN(obj, column, timingPoint.Key))
                                {
                                    obj.AddNote(locations[0].samples, column, timingPoint.Key);
                                    quantity++;
                                }
                            }
                        }
                    }
                    else if (probability < 0)
                    {
                        foreach (int column in columnWithNote)
                        {
                            if (quantity > Extremum.Value && Rng.Next(100) < probability)
                            {
                                obj.RemoveNote(column, timingPoint.Key);
                                quantity--;
                            }
                        }
                    }
                    columnWithNoNote = new List<int>(Enumerable.Range(0, keys));
                    columnWithNote = new List<int>();
                }
                return;
            }



            List<(int column, double startTime, double endTime, IList<HitSampleInfo> samples)> line = new List<(int column, double startTime, double endTime, IList<HitSampleInfo> samples)>();
            List<List<(int column, double startTime, double endTime, IList<HitSampleInfo> samples)>> manyLine = new List<List<(int column, double startTime, double endTime, IList<HitSampleInfo> samples)>>();

            var middleLine = new List<(int column, double startTime, double endTime, IList<HitSampleInfo> samples)>();
            var lastLine = new List<(int column, double startTime, double endTime, IList<HitSampleInfo> samples)>();
            var nextLine = new List<(int column, double startTime, double endTime, IList<HitSampleInfo> samples)>();

            double? middleTime = null;
            IList<HitSampleInfo>? samples = null;
            int lastQuantity = 0, middleQuantity = 0, nextQuantity = 0;
            int i = 0, skip = 0;

            foreach (var timingPoint in beatmap.HitObjects.GroupBy(h => h.StartTime))
            {
                var locations = timingPoint.OfType<Note>().Select(n => (column: n.Column, startTime: n.StartTime, endTime: n.StartTime, samples: n.Samples))
                          .Concat(timingPoint.OfType<HoldNote>().SelectMany(h => new[]
                          {(
                                        column: h.Column,
                                        startTime: h.StartTime,
                                        endTime: h.EndTime,
                                        samples: h.GetNodeSamples(0)
                                  )}))
                          .OrderBy(h => h.startTime).ToList();

                if (i < 1 + interval * 2)
                {
                    foreach (var note in locations)
                    {
                        line.Add((note.column, note.startTime, note.endTime, note.samples));
                        obj.AddNote(note.samples, note.column, note.startTime, note.endTime);
                    }

                    manyLine.Add(line);

                    if (i >= interval)
                    {
                        foreach (var inLine in manyLine)
                        {
                            foreach (var note in inLine)
                            {
                                columnWithNoNote.Remove(note.column);
                            }
                        }
                        middleLine = manyLine[i - interval];
                        nextLine = manyLine[i - interval + 1];
                        foreach (var note in middleLine)
                        {
                            columnWithNote.Add(note.column);
                        }
                        middleQuantity = columnWithNote.Count;
                        nextQuantity = nextLine.Count;
                        middleTime = middleLine[^1].startTime;
                        samples = middleLine[^1].samples;

                        if (ignoreInterval)
                        {
                            columnWithNoNote = new List<int>(Enumerable.Range(0, keys));
                            foreach (var note in middleLine)
                            {
                                columnWithNoNote.Remove(note.column);
                            }
                        }

                        columnWithNoNote = columnWithNoNote.ShuffleIndex(Rng).ToList();
                        columnWithNote = columnWithNote.ShuffleIndex(Rng).ToList();

                        if (compare == 1)
                        {
                            if (probability > 0 && ((middleQuantity >= nextQuantity && middleQuantity >= lastQuantity) || ignoreComparison))
                            {
                                foreach (int column in columnWithNoNote)
                                {
                                    if (middleQuantity < extremum && Rng.Next(100) < probability && middleTime is not null && samples is not null)
                                    {
                                        if (!InLN(obj, column, (double)middleTime))
                                        {
                                            middleLine.Add((column, (double)middleTime, (double)middleTime, samples));
                                            middleQuantity++;
                                            obj.AddNote(samples, column, (double)middleTime);
                                            skip = skipStep + 1;
                                        }
                                    }
                                }
                            }
                            else if (probability < 0 && ((middleQuantity >= nextQuantity && middleQuantity >= lastQuantity) || ignoreComparison))
                            {
                                foreach (int column in columnWithNote)
                                {
                                    if (middleQuantity > extremum && Rng.Next(100) < -probability && middleTime is not null)
                                    {
                                        middleLine = middleLine.Where(s => s.column != column).ToList();
                                        middleQuantity--;
                                        obj.RemoveNote(column, (double)middleTime);
                                        skip = skipStep + 1;
                                    }
                                }
                            }
                        }
                        else if (compare == 2)
                        {
                            if (probability > 0 && ((middleQuantity <= nextQuantity && middleQuantity <= lastQuantity) || ignoreComparison))
                            {
                                foreach (int column in columnWithNoNote)
                                {
                                    if (middleQuantity < extremum && Rng.Next(100) < probability && middleTime is not null && samples is not null)
                                    {
                                        if (!InLN(obj, column, (double)middleTime))
                                        {
                                            middleLine.Add((column, (double)middleTime, (double)middleTime, samples));
                                            middleQuantity++;
                                            obj.AddNote(samples, column, (double)middleTime);
                                            skip = skipStep + 1;
                                        }
                                    }
                                }
                            }
                            else if (probability < 0 && ((middleQuantity <= nextQuantity && middleQuantity <= lastQuantity) || ignoreComparison))
                            {
                                foreach (int column in columnWithNote)
                                {
                                    if (middleQuantity > extremum && Rng.Next(100) < -probability && middleTime is not null)
                                    {
                                        middleLine = middleLine.Where(s => s.column != column).ToList();
                                        middleQuantity--;
                                        obj.RemoveNote(column, (double)middleTime);
                                        skip = skipStep + 1;
                                    }
                                }
                            }
                        }
                    }
                    line = new List<(int column, double startTime, double endTime, IList<HitSampleInfo> samples)>();
                    lastLine = middleLine;
                    middleLine = new List<(int column, double startTime, double endTime, IList<HitSampleInfo> samples)>();
                    nextLine = new List<(int column, double startTime, double endTime, IList<HitSampleInfo> samples)>();
                    lastQuantity = middleQuantity;
                    middleQuantity = 0;
                    nextQuantity = 0;
                    columnWithNoNote = new List<int>(Enumerable.Range(0, keys));
                    columnWithNote = new List<int>();
                    i++;
                    if (i == 1 + interval * 2)
                    {
                        manyLine.RemoveAt(0);
                    }
                    continue;
                }

                foreach (var note in locations)
                {
                    line.Add((note.column, note.startTime, note.endTime, note.samples));
                    obj.AddNote(note.samples, note.column, note.startTime, note.endTime);
                }
                manyLine.Add(line);

                foreach (var inLine in manyLine)
                {
                    foreach (var note in inLine)
                    {
                        columnWithNoNote.Remove(note.column);
                    }
                }
                middleLine = manyLine[interval];
                nextLine = manyLine[interval + 1];
                foreach (var note in middleLine)
                {
                    columnWithNote.Add(note.column);
                }
                middleQuantity = columnWithNote.Count;
                nextQuantity = nextLine.Count;
                middleTime = middleLine[^1].startTime;
                samples = middleLine[^1].samples;

                if (ignoreInterval)
                {
                    columnWithNoNote = new List<int>(Enumerable.Range(0, keys));
                    foreach (var note in middleLine)
                    {
                        columnWithNoNote.Remove(note.column);
                    }
                }

                if (skip > 0)
                {
                    goto skip;
                }

                columnWithNoNote = columnWithNoNote.ShuffleIndex(Rng).ToList();
                columnWithNote = columnWithNote.ShuffleIndex(Rng).ToList();

                if (compare == 1)
                {
                    if (probability > 0 && ((middleQuantity >= nextQuantity && middleQuantity >= lastQuantity) || ignoreComparison))
                    {
                        foreach (int column in columnWithNoNote)
                        {
                            if (middleQuantity < extremum && Rng.Next(100) < probability && middleTime is not null && samples is not null)
                            {
                                if (!InLN(obj, column, (double)middleTime))
                                {
                                    middleLine.Add((column, (double)middleTime, (double)middleTime, samples));
                                    middleQuantity++;
                                    obj.AddNote(samples, column, (double)middleTime);
                                    skip = skipStep + 1;
                                }
                            }
                        }
                    }
                    else if (probability < 0 && ((middleQuantity >= nextQuantity && middleQuantity >= lastQuantity) || ignoreComparison))
                    {
                        foreach (int column in columnWithNote)
                        {
                            if (middleQuantity > extremum && Rng.Next(100) < -probability && middleTime is not null)
                            {
                                middleLine = middleLine.Where(s => s.column != column).ToList();
                                middleQuantity--;
                                obj.RemoveNote(column, (double)middleTime);
                                skip = skipStep + 1;
                            }
                        }
                    }
                }
                else if (compare == 2)
                {
                    if (probability > 0 && ((middleQuantity <= nextQuantity && middleQuantity <= lastQuantity) || ignoreComparison))
                    {
                        foreach (int column in columnWithNoNote)
                        {
                            if (middleQuantity < extremum && Rng.Next(100) < probability && middleTime is not null && samples is not null)
                            {
                                if (!InLN(obj, column, (double)middleTime))
                                {
                                    middleLine.Add((column, (double)middleTime, (double)middleTime, samples));
                                    middleQuantity++;
                                    obj.AddNote(samples, column, (double)middleTime);
                                    skip = skipStep + 1;
                                }
                            }
                        }
                    }
                    else if (probability < 0 && ((middleQuantity <= nextQuantity && middleQuantity <= lastQuantity) || ignoreComparison))
                    {
                        foreach (int column in columnWithNote)
                        {
                            if (middleQuantity > extremum && Rng.Next(100) < -probability && middleTime is not null)
                            {
                                middleLine = middleLine.Where(s => s.column != column).ToList();
                                middleQuantity--;
                                obj.RemoveNote(column, (double)middleTime);
                                skip = skipStep + 1;
                            }
                        }
                    }
                }

            skip:
                if (skip > 0)
                {
                    skip--;
                }
                line = new List<(int column, double startTime, double endTime, IList<HitSampleInfo> samples)>();
                lastLine = middleLine;
                middleLine = new List<(int column, double startTime, double endTime, IList<HitSampleInfo> samples)>();
                nextLine = new List<(int column, double startTime, double endTime, IList<HitSampleInfo> samples)>();
                lastQuantity = middleQuantity;
                middleQuantity = 0;
                nextQuantity = 0;
                columnWithNoNote = new List<int>(Enumerable.Range(0, keys));
                columnWithNote = new List<int>();
                manyLine.RemoveAt(0);
            }

            // Dispose last n line.

            for (i = interval; i < manyLine.Count; i++)
            {
                foreach (var inLine in manyLine)
                {
                    foreach (var note in inLine)
                    {
                        columnWithNoNote.Remove(note.column);
                    }
                }
                middleLine = manyLine[i];
                if (i + 1 < manyLine.Count)
                {
                    nextLine = manyLine[i + 1];
                }
                foreach (var note in middleLine)
                {
                    columnWithNote.Add(note.column);
                }
                middleQuantity = columnWithNote.Count;
                nextQuantity = nextLine.Count;
                middleTime = middleLine[^1].startTime;
                samples = middleLine[^1].samples;

                if (ignoreInterval)
                {
                    columnWithNoNote = new List<int>(Enumerable.Range(0, keys));
                    foreach (var note in middleLine)
                    {
                        columnWithNoNote.Remove(note.column);
                    }
                }

                columnWithNoNote = columnWithNoNote.ShuffleIndex(Rng).ToList();
                columnWithNote = columnWithNote.ShuffleIndex(Rng).ToList();

                if (compare == 1)
                {
                    if (probability > 0 && ((middleQuantity >= nextQuantity && middleQuantity >= lastQuantity) || ignoreComparison))
                    {
                        foreach (int column in columnWithNoNote)
                        {
                            if (middleQuantity < extremum && Rng.Next(100) < probability && middleTime is not null && samples is not null)
                            {
                                if (!InLN(obj, column, (double)middleTime))
                                {
                                    middleLine.Add((column, (double)middleTime, (double)middleTime, samples));
                                    middleQuantity++;
                                    obj.AddNote(samples, column, (double)middleTime);
                                }
                            }
                        }
                    }
                    else if (probability < 0 && ((middleQuantity >= nextQuantity && middleQuantity >= lastQuantity) || ignoreComparison))
                    {
                        foreach (int column in columnWithNote)
                        {
                            if (middleQuantity > extremum && Rng.Next(100) < -probability && middleTime is not null)
                            {
                                middleLine = middleLine.Where(s => s.column != column).ToList();
                                middleQuantity--;
                                obj.RemoveNote(column, (double)middleTime);
                            }
                        }
                    }
                }
                else if (compare == 2)
                {
                    if (probability > 0 && ((middleQuantity <= nextQuantity && middleQuantity <= lastQuantity) || ignoreComparison))
                    {
                        foreach (int column in columnWithNoNote)
                        {
                            if (middleQuantity < extremum && Rng.Next(100) < probability && middleTime is not null && samples is not null)
                            {
                                if (!InLN(obj, column, (double)middleTime))
                                {
                                    middleLine.Add((column, (double)middleTime, (double)middleTime, samples));
                                    middleQuantity++;
                                    obj.AddNote(samples, column, (double)middleTime);
                                }
                            }
                        }
                    }
                    else if (probability < 0 && ((middleQuantity <= nextQuantity && middleQuantity <= lastQuantity) || ignoreComparison))
                    {
                        foreach (int column in columnWithNote)
                        {
                            if (middleQuantity > extremum && Rng.Next(100) < -probability && middleTime is not null)
                            {
                                middleLine = middleLine.Where(s => s.column != column).ToList();
                                middleQuantity--;
                                obj.RemoveNote(column, (double)middleTime);
                            }
                        }
                    }
                }

                line = new List<(int column, double startTime, double endTime, IList<HitSampleInfo> samples)>();
                lastLine = middleLine;
                middleLine = new List<(int column, double startTime, double endTime, IList<HitSampleInfo> samples)>();
                nextLine = new List<(int column, double startTime, double endTime, IList<HitSampleInfo> samples)>();
                lastQuantity = middleQuantity;
                middleQuantity = 0;
                nextQuantity = 0;
                columnWithNoNote = new List<int>(Enumerable.Range(0, keys));
                columnWithNote = new List<int>();
            }
        }

        public bool InLN(List<ManiaHitObject> obj, int column, double startTime)
        {
            var temp = obj.Where(x => x.Column == column);
            var times = temp.OfType<HoldNote>().Select(n => (column: n.Column, startTime: n.StartTime, endTime: n.EndTime));

            //var temp2 = newColumnObjects.GroupBy(x => x.Column == column);
            //var times2 = temp2.OfType<HoldNote>().Select(n => (column: n.Column, startTime: n.StartTime, endTime: n.EndTime));

            foreach (var time in times)
            {
                if (time.column == column && startTime >= time.startTime && startTime <= time.endTime)
                {
                    return true;
                }
            }

            return false;
        }
    }
}

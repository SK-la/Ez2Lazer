// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.LAsEzExtensions.Mods;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.LAsEZMania;
using osu.Game.Rulesets.Mania.LAsEzMania.Mods;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Rulesets.Mania.Mods.YuLiangSSSMods
{
    public class ManiaModJackAdjust : Mod, IApplicableAfterBeatmapConversion, IHasSeed
    {
        public const int MAX_KEY = 18;

        public override string Name => "Jack Adjust";

        public override string Acronym => "JA";

        public override LocalisableString Description => EzManiaModStrings.JackAdjust_Description;

        public override ModType Type => ModType.YuLiangSSS_Mod;

        public override IconUsage? Icon => FontAwesome.Solid.Bars;

        public override double ScoreMultiplier => 1;

        public override bool Ranked => false;
        public override bool ValidForMultiplayer => true;
        public override bool ValidForFreestyleAsRequiredMod => false;

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                yield return ("Probability", $"{Probability.Value}");
                yield return ("Line", $"{Line.Value}");
                yield return ("Alignment", Align.Value ? "First Line" : "Last Line");
                yield return ("Seed", $"{(Seed.Value == null ? "Null" : Seed.Value)}");
            }
        }

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.ToStream_Label), nameof(EzManiaModStrings.ToStream_Description))]
        public BindableBool Stream { get; set; } = new BindableBool(true);

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.Probability_Label), nameof(EzManiaModStrings.Probability_Description))]
        public BindableInt Probability { get; set; } = new BindableInt(100)
        {
            Precision = 1,
            MinValue = 0,
            MaxValue = 100
        };

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.Line_Label), nameof(EzManiaModStrings.Line_Description))]
        public BindableInt Line { get; set; } = new BindableInt(3)
        {
            Precision = 1,
            MinValue = 2,
            MaxValue = 16
        };

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.Alignment_Label), nameof(EzManiaModStrings.Alignment_Description))]
        public BindableBool Align { get; set; } = new BindableBool(true);

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.Seed_Label), nameof(EzManiaModStrings.Seed_Description), SettingControlType = typeof(SettingsNumberBox))]
        public Bindable<int?> Seed { get; } = new Bindable<int?>();

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            Seed.Value ??= RNG.Next();
            var rng = new Random((int)Seed.Value);
            var maniaBeatmap = (ManiaBeatmap)beatmap;
            var newObjects = new List<ManiaHitObject>();
            var areaObjects = new List<ManiaHitObject>();
            int keys = maniaBeatmap.TotalColumns;
            int line = Line.Value;
            var lastLine = new List<ManiaHitObject>();

            foreach (var timingPoint in maniaBeatmap.HitObjects.GroupBy(h => h.StartTime))
            {
                var thisLine = new List<ManiaHitObject>();
                thisLine.AddRange(timingPoint);

                if (!Stream.Value)
                {
                    if (line > 0)
                    {
                        areaObjects.AddRange(thisLine);
                        line--;
                    }
                    else
                    {
                        var processed = ProcessArea(rng, areaObjects, Line.Value, keys, Probability.Value, Align.Value);
                        newObjects.AddRange(processed);
                        line = Line.Value;
                        areaObjects.Clear();
                        areaObjects.AddRange(thisLine);
                        line--;
                    }
                }
                else
                {
                    var duplicateColumn = lastLine.Select(h => h.Column).ToList();
                    var notDuplicate = Enumerable.Range(0, keys).ToList();
                    notDuplicate = notDuplicate.Except(duplicateColumn).ToList();
                    int count = notDuplicate.Count;
                    thisLine = thisLine.ShuffleIndex(rng).ToList();
                    int selectError = 0;

                    for (int i = 0; i < thisLine.Count; i++)
                    {
                        if (count == 0) break;

                        if (duplicateColumn.Contains(thisLine[i].Column) && rng.Next(100) < Probability.Value)
                        {
                            bool jumpLoop = false;
                            int randColumn = notDuplicate.SelectRandomOne(rng);

                            while (thisLine.Any(c => c.Column == randColumn))
                            {
                                if (selectError > MAX_KEY)
                                {
                                    jumpLoop = true;
                                    selectError = 0;
                                    break;
                                }

                                randColumn = notDuplicate.SelectRandomOne(rng);
                                selectError++;
                            }

                            if (jumpLoop) continue;

                            duplicateColumn.Remove(thisLine[i].Column);
                            thisLine[i].Column = randColumn;
                            count--;
                        }
                    }

                    newObjects.AddRange(thisLine);
                }

                lastLine = thisLine.ToList();
            }

            if (!Stream.Value && areaObjects.Count != 0)
            {
                var processed = ProcessArea(rng, areaObjects, Line.Value, keys, Probability.Value, Align.Value);
                newObjects.AddRange(processed);
            }

            var cleanObjects = new List<ManiaHitObject>();

            foreach (var column in newObjects.GroupBy(c => c.Column))
            {
                var newColumnObjects = new List<ManiaHitObject>();

                var cleanLocations = column.OfType<Note>().Select(n => (startTime: n.StartTime, samples: n.Samples, endTime: n.StartTime))
                                           .Concat(column.OfType<HoldNote>().SelectMany(h => new[]
                                           {
                                               (startTime: h.StartTime, samples: h.GetNodeSamples(0), endTime: h.EndTime)
                                           }))
                                           .OrderBy(h => h.startTime).ToList();

                double lastStartTime = cleanLocations[0].startTime;
                double lastEndTime = cleanLocations[0].endTime;
                var lastSample = cleanLocations[0].samples;

                for (int i = 0; i < cleanLocations.Count; i++)
                {
                    if (i == 0)
                    {
                        lastStartTime = cleanLocations[0].startTime;
                        lastEndTime = cleanLocations[0].endTime;
                        lastSample = cleanLocations[0].samples;
                        continue;
                    }

                    if (cleanLocations[i].startTime >= lastStartTime && cleanLocations[i].startTime <= lastEndTime)
                    {
                        cleanLocations.RemoveAt(i);
                        i--;
                        continue;
                    } // if the note in a LN

                    newColumnObjects.AddNote(lastSample, column.Key, lastStartTime, lastEndTime);
                    lastStartTime = cleanLocations[i].startTime;
                    lastEndTime = cleanLocations[i].endTime;
                    lastSample = cleanLocations[i].samples;
                }

                newColumnObjects.AddNote(lastSample, column.Key, lastStartTime, lastEndTime);

                cleanObjects.AddRange(newColumnObjects);
            }

            maniaBeatmap.HitObjects = cleanObjects.OrderBy(h => h.StartTime).ToList();
        }

        public List<ManiaHitObject> ProcessArea(Random rng, List<ManiaHitObject> area, int line, int keys, int probability, bool align)
        {
            var resultObjects = new List<ManiaHitObject>();
            var jackLine = new List<ManiaHitObject>(); // first line
            var lastLine = new List<ManiaHitObject>();
            bool init = true;
            // int jackCount = 0;

            foreach (var group in area.GroupBy(h => h.StartTime))
            {
                var thisLine = group.ToList();

                if (init)
                {
                    jackLine = thisLine;
                    lastLine = thisLine;
                    resultObjects.AddRange(thisLine);
                    init = false;
                    // jackCount = jackLine.Count;
                    continue;
                }

                //if (init)
                //{
                //    var select = SelectNote(Rng, thisLine, probability);
                //    var remain = select.remain.ShuffleIndex(Rng).ToList();
                //    var result = select.result.ShuffleIndex(Rng).ToList();
                //    var duplicateColumn = remain.Select(c => c.Column).ShuffleIndex(Rng).ToList();
                //    var forAlign = Align.Value ? SelectNote(Rng, jackLine, probability, jackCount) : SelectNote(Rng, lastLine, probability, jackCount);
                //    var alignResult = forAlign.result.ShuffleIndex(Rng).ToList();
                //    for (int i = 0; i < jackCount; i++)
                //    {
                //        if (!duplicateColumn.Contains(alignResult[i].Column))
                //        {
                //            result[i].Column = alignResult[i].Column;
                //            duplicateColumn.Add(alignResult[i].Column);
                //        }
                //    }
                //    resultObjects.AddRange(remain);
                //    resultObjects.AddRange(result);
                //}

                //int count = Math.Min(jackCount, thisLine.Count);
                //var select = SelectNote(Rng, thisLine, probability, count);
                //count = select.result.Count;
                // var select = thisLine;
                var jackColumn = jackLine.Select(c => c.Column).ShuffleIndex(rng).ToList();
                if (!align) jackColumn = lastLine.Select(c => c.Column).ShuffleIndex(rng).ToList();
                thisLine = thisLine.ShuffleIndex(rng).ToList();

                for (int i = 0; i < thisLine.Count; i++)
                {
                    if (!jackColumn.Contains(thisLine[i].Column) && jackColumn.Count > 0 && thisLine[i].GetEndTime() == thisLine[i].StartTime)
                    {
                        int randColumn = jackColumn.SelectRandomOne(rng);
                        int opportunity = 0;
                        const int max = 20;

                        while (opportunity < max)
                        {
                            if (randColumn == thisLine[i].Column || thisLine.Except(Enumerable.Repeat(thisLine[i], 1)).Any(c => c.Column == randColumn))
                            {
                                randColumn = jackColumn.SelectRandomOne(rng);

                                if (randColumn != thisLine[i].Column && thisLine.Except(Enumerable.Repeat(thisLine[i], 1)).All(c => c.Column != randColumn))
                                {
                                    thisLine[i].Column = randColumn;
                                    jackColumn.Remove(randColumn);
                                    break;
                                }
                            }

                            opportunity++;
                        }
                    }
                }

                resultObjects.AddRange(thisLine);

                lastLine = thisLine;
            }

            return resultObjects.OrderBy(s => s.StartTime).ToList();
        }

        public static (List<ManiaHitObject> remain, List<ManiaHitObject> result) SelectNote(Random rng, List<ManiaHitObject> obj, int probability = 100, int num = 1)
        {
            if (num > obj.Count)
            {
                var nullList = new List<ManiaHitObject>();
                return (nullList, nullList);
            }

            var remainList = obj.ToList();
            var resultList = new List<ManiaHitObject>();

            for (int i = 0; i < num; i++)
            {
                if (rng.Next(100) < probability)
                {
                    int index = rng.Next(remainList.Count);
                    resultList.Add(remainList[index]);
                    remainList.RemoveAt(index);
                }
            }

            return (remainList, resultList);
        }
    }
}

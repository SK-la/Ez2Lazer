﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
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
    public class ManiaModNtoMAnother : Mod, IApplicableToBeatmapConverter, IApplicableAfterBeatmapConversion, IHasSeed
    {
        public const double INTERVAL = 50;

        public const double LN_INTERVAL = 10;

        public const double ERROR = 1.5;

        public override string Name => "Nk to Mk Converter Another";

        public override string Acronym => "NTMA";

        public override double ScoreMultiplier => 1;

        public override LocalisableString Description => "From krrcream's Tool (It has some bugs, please use Clean settings to clean it.)";

        public override IconUsage? Icon => FontAwesome.Solid.CloudRain;

        public override ModType Type => ModType.CustomMod;

        public override bool Ranked => false;

        [SettingSource("Key", "To Keys")]
        public BindableNumber<int> Key { get; set; } = new BindableInt(8)
        {
            MinValue = 2,
            MaxValue = 10,
            Precision = 1
        };

        [SettingSource("Blank Column", "Number of blank columns to add. (Notice: If the number of Key - CircleSize is less than the number of blank columns, it won't be added.)")]
        public BindableNumber<int> BlankColumn { get; set; } = new BindableInt(0)
        {
            MinValue = 0,
            MaxValue = 10,
            Precision = 1
        };

        [SettingSource("Gap", "Rearrange the notes in every area. (If Gap is bigger, the notes will be more spread out.)")]
        public BindableInt Gap { get; set; } = new BindableInt(10)
        {
            MinValue = 0,
            MaxValue = 20,
            Precision = 1
        };

        [SettingSource("Clean", "Try to clean some notes in the map.")]
        public BindableBool Clean { get; set; } = new BindableBool(true);

        [SettingSource("Clean Divide", "Choose the divide(0 For no Divide Clean, 4 is Recommended for Stream, 8 is Recommended for Jack) of cleaning. (If Clean is false, this setting won't be used.)")]
        public BindableInt CleanDivide { get; set; } = new BindableInt(4)
        {
            MinValue = 0,
            MaxValue = 16,
            Precision = 1
        };

        [SettingSource("Seed", "Use a custom seed instead of a random one.", SettingControlType = typeof(SettingsNumberBox))]
        public Bindable<int?> Seed { get; } = new Bindable<int?>();

        public void ApplyToBeatmapConverter(IBeatmapConverter converter)
        {
            var mbc = (ManiaBeatmapConverter)converter;

            float keys = mbc.TotalColumns;

            if (keys > 9 || Key.Value <= keys)
            {
                return;
            }

            mbc.TargetColumns = Key.Value;
        }

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            Random? Rng;
            Seed.Value ??= RNG.Next();
            Rng = new Random((int)Seed.Value);

            var maniaBeatmap = (ManiaBeatmap)beatmap;

            int keys = (int)maniaBeatmap.Difficulty.CircleSize;

            int blank = BlankColumn.Value;
            if (blank > Key.Value - keys)
            {
                blank = Key.Value - keys;
            }

            if (keys > 9 || Key.Value <= keys)
            {
                return;
            }

            var newObjects = new List<ManiaHitObject>();

            var locations = maniaBeatmap.HitObjects.OfType<Note>().Select(n =>
            (
                column: n.Column,
                startTime: n.StartTime,
                endTime: n.StartTime,
                samples: n.Samples
            ))
            .Concat(maniaBeatmap.HitObjects.OfType<HoldNote>().Select(h =>
            (
                column: h.Column,
                startTime: h.StartTime,
                endTime: h.EndTime,
                samples: h.Samples
            ))).OrderBy(h => h.startTime).ThenBy(n => n.column).ToList();

            var confirmNull = new List<bool>();
            var nullColumnList = new List<int>();

            for (int i = 0; i <= Key.Value; i++)
            {
                confirmNull.Add(false);
            }

            foreach (var column in maniaBeatmap.HitObjects.GroupBy(h => h.Column))
            {
                int count = column.Count();
                if (!confirmNull[column.Key] && count != 0)
                {
                    confirmNull[column.Key] = true;
                }
            }

            for (int i = 0; i < Key.Value; i++)
            {
                if (!confirmNull[i])
                {
                    nullColumnList.Add(i);
                }
            }

            for (int i = 0; i < locations.Count; i++)
            {
                int minusColumn = 0;
                foreach (int nul in nullColumnList)
                {
                    if (locations[i].column > nul)
                    {
                        minusColumn++;
                    }
                }
                var thisLocations = locations[i];
                thisLocations.column -= minusColumn;
                locations[i] = thisLocations;
            }

            List<(int column, double startTime, double endTime, IList<HitSampleInfo> samples)> area = new List<(int column, double startTime, double endTime, IList<HitSampleInfo> samples)>();
            List<ManiaHitObject> checkList = new List<ManiaHitObject>();

            var tempObjects = locations.OrderBy(h => h.startTime).ToList();

            double sumTime = 0;
            double lastTime = 0;

            foreach (var timingPoint in tempObjects.GroupBy(h => h.startTime))
            {
                var newLocations = timingPoint.OfType<(int column, double startTime, double endTime, IList<HitSampleInfo> samples)>().Select(n => (Column: n.column, StartTime: n.startTime, EndTime: n.endTime, Samples: n.samples)).OrderBy(h => h.Column).ToList();

                List<(int column, double startTime, double endTime, IList<HitSampleInfo> samples)> line = new List<(int column, double startTime, double endTime, IList<HitSampleInfo> samples)>();

                foreach (var note in newLocations)
                {
                    line.Add((note.Column, note.StartTime, note.EndTime, note.Samples));
                }

                //manyLine.Add(line);
                int blankColumn = BlankColumn.Value;

                sumTime += timingPoint.Key - lastTime;
                lastTime = timingPoint.Key;

                area.AddRange(line);

                double gap = 29998.8584 * Math.Pow(Math.E, -0.3176 * Gap.Value) + 347.7248;

                if (Gap.Value == 0)
                {
                    gap = double.MaxValue;
                }

                if (sumTime >= gap)
                {
                    sumTime = 0;
                    // Process area
                    var processed = ProcessArea(maniaBeatmap, Rng, area, keys, Key.Value, blank, CleanDivide.Value, ERROR, checkList);
                    newObjects.AddRange(processed.result);
                    checkList = processed.checkList.ToList();
                    area.Clear();
                }
            }

            if (area.Count > 0)
            {
                var processed = ProcessArea(maniaBeatmap, Rng, area, keys, Key.Value, blank, CleanDivide.Value, ERROR, checkList);
                newObjects.AddRange(processed.result);
            }

            newObjects = newObjects.OrderBy(h => h.StartTime).ToList();

            var cleanObjects = new List<ManiaHitObject>();

            foreach (var column in newObjects.GroupBy(h => h.Column))
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

                    if (Math.Abs(cleanLocations[i].startTime - lastStartTime) <= INTERVAL)
                    {
                        lastStartTime = cleanLocations[i].startTime;
                        lastEndTime = cleanLocations[i].endTime;
                        lastSample = cleanLocations[i].samples;
                        cleanLocations.RemoveAt(i);
                        i--;
                        continue;
                    } // interval judgement

                    if (Math.Abs(cleanLocations[i].startTime - lastEndTime) <= LN_INTERVAL)
                    {
                        lastStartTime = cleanLocations[i].startTime;
                        lastEndTime = cleanLocations[i].endTime;
                        lastSample = cleanLocations[i].samples;
                        cleanLocations.RemoveAt(i);
                        i--;
                        continue;
                    } // LN interval judgement

                    newColumnObjects.AddNote(lastSample, column.Key, lastStartTime, lastEndTime);
                    lastStartTime = cleanLocations[i].startTime;
                    lastEndTime = cleanLocations[i].endTime;
                    lastSample = cleanLocations[i].samples;
                }

                newColumnObjects.AddNote(lastSample, column.Key, lastStartTime, lastEndTime);

                cleanObjects.AddRange(newColumnObjects);
            }

            if (Clean.Value)
            {
                maniaBeatmap.HitObjects = cleanObjects.OrderBy(h => h.StartTime).ToList();
            }
            else
            {
                maniaBeatmap.HitObjects = newObjects.OrderBy(h => h.StartTime).ToList();
            }
        }

        public (List<ManiaHitObject> result, List<ManiaHitObject> checkList) ProcessArea(ManiaBeatmap beatmap, Random Rng, List<(int column, double startTime, double endTime, IList<HitSampleInfo> samples)> hitObjects, int fromKeys, int toKeys, int blankNum = 0, int clean = 0, double error = 0, List<ManiaHitObject>? checkList = null)
        {
            List<ManiaHitObject> newObjects = new List<ManiaHitObject>();
            List<(int column, bool isBlank)> copyColumn = [];
            List<int> insertColumn = [];
            List<ManiaHitObject> checkColumn = [];
            bool isFirst = true;

            int num = toKeys - fromKeys - blankNum;
            while (num > 0)
            {
                int copy = Rng.Next(fromKeys);
                if (!copyColumn.Contains((copy, false)))
                {
                    copyColumn.Add((copy, false));
                    num--;
                }
            }

            num = blankNum;
            while (num > 0)
            {
                int copy = -1;
                copyColumn.Add((copy, true));
                num--;
            }

            num = toKeys - fromKeys;
            while (num > 0)
            {
                int insert = Rng.Next(toKeys);
                if (!insertColumn.Contains(insert))
                {
                    insertColumn.Add(insert);
                    num--;
                }
            }
            insertColumn = insertColumn.OrderBy(c => c).ToList();

            foreach (var timingPoint in hitObjects.GroupBy(h => h.startTime))
            {
                var locations = timingPoint.OfType<(int column, double startTime, double endTime, IList<HitSampleInfo> samples)>().ToList();
                var tempObjects = new List<ManiaHitObject>();
                int length = copyColumn.Count;

                for (int i = 0; i < locations.Count; i++)
                {
                    int column = locations[i].column;
                    for (int j = 0; j < length; j++)
                    {
                        if (column == copyColumn[j].column && !copyColumn[j].isBlank)
                        {
                            tempObjects.AddNote(locations[i].samples, insertColumn[j], locations[i].startTime, locations[i].endTime);
                        }

                        if (locations[i].column >= insertColumn[j])
                        {
                            locations[i] = (locations[i].column + 1, locations[i].startTime, locations[i].endTime, locations[i].samples);
                        }
                    }
                    tempObjects.AddNote(locations[i].samples, locations[i].column, locations[i].startTime, locations[i].endTime);
                }

                if (isFirst && checkList is not null && checkList.Count > 0 && clean > 0)
                {
                    var checkC = checkList.Select(h => h.Column).ToList();
                    var checkS = checkList.Select(h => h.StartTime).ToList();
                    for (int i = 0; i < tempObjects.Count; i++)
                    {
                        if (checkC.Contains(tempObjects[i].Column))
                        {
                            if (clean != 0)
                            {
                                double beatLength = beatmap.ControlPointInfo.TimingPointAt(tempObjects[i].StartTime).BeatLength;
                                double timeDivide = beatLength / clean;
                                int index = checkC.IndexOf(tempObjects[i].Column);

                                if (tempObjects[i].StartTime - checkS[index] < timeDivide + error)
                                {
                                    tempObjects.RemoveAt(i);
                                    i--;
                                }
                            }
                            else
                            {
                                tempObjects.RemoveAt(i);
                                i--;
                            }
                        }
                    }
                    isFirst = false;
                }

                checkColumn.Clear();
                checkColumn.AddRange(tempObjects);
                newObjects.AddRange(tempObjects);
            }

            return (newObjects, checkColumn);
        }
    }
}

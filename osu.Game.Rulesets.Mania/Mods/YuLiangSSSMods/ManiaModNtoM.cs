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
using osu.Game.Rulesets.Objects;
using osuTK.Input;

namespace osu.Game.Rulesets.Mania.Mods.YuLiangSSSMods
{
    public class ManiaModNtoM : Mod, IApplicableToBeatmapConverter, IApplicableAfterBeatmapConversion, IHasSeed, osu.Game.Rulesets.Mods.IHasApplyOrder
    {
        public override string Name => "Nk to Mk Converter";

        public override string Acronym => "NTM"; //Nk to Mk Letter

        public override double ScoreMultiplier => 1;

        public override LocalisableString Description => "Convert to upper Keys mode.";

        public override IconUsage? Icon => FontAwesome.Solid.Moon;

        public override ModType Type => ModType.YuLiangSSS_Mod;

        public override bool Ranked => false;

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                yield return ("Probability", $"{Probability.Value}%");
                yield return ("Key", $"{Key.Value}");
                yield return ("Seed", $"{(Seed.Value == null ? "Null" : Seed.Value)}");
            }
        }

        [SettingSource("Probability", "Needed convert column movement probability")]
        public BindableNumber<int> Probability { get; set; } = new BindableInt(70)
        {
            MinValue = 0,
            MaxValue = 100,
            Precision = 5
        };

        [SettingSource("Key", "To Keys")]
        public BindableNumber<int> Key { get; set; } = new BindableInt(8)
        {
            MinValue = 2,
            MaxValue = 10,
            Precision = 1
        };

        [SettingSource("Seed", "Use a custom seed instead of a random one.", SettingControlType = typeof(SettingsNumberBox))]
        public Bindable<int?> Seed { get; } = new Bindable<int?>();

        [SettingSource("Apply Order", "Order in which this mod is applied after beatmap conversion. Lower runs earlier.", SettingControlType = typeof(SettingsNumberBox))]
        public Bindable<int?> ApplyOrderSetting { get; } = new Bindable<int?>(0);

        public void ApplyToBeatmapConverter(IBeatmapConverter converter)
        {
            var mbc = (ManiaBeatmapConverter)converter;

            float keys = mbc.TotalColumns;

            if (keys > 9 || Key.Value <= keys) return;

            mbc.TargetColumns = Key.Value;
        }

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            Random? Rng;
            Seed.Value ??= RNG.Next();
            Rng = new Random((int)Seed.Value);

            var maniaBeatmap = (ManiaBeatmap)beatmap;

            int keys = (int)maniaBeatmap.Difficulty.CircleSize;

            if (keys > 9 || Key.Value <= keys) return;

            var newObjects = new List<ManiaHitObject>();

            var newColumnObjects = new List<ManiaHitObject>();

            var fixedColumnObjects = new List<ManiaHitObject>();

            var locations = maniaBeatmap.HitObjects.OfType<Note>().Select(n =>
                                        (
                                            startTime: n.StartTime,
                                            samples: n.Samples,
                                            column: n.Column,
                                            endTime: n.StartTime,
                                            duration: n.StartTime - n.StartTime
                                        ))
                                        .Concat(maniaBeatmap.HitObjects.OfType<HoldNote>().Select(h =>
                                        (
                                            startTime: h.StartTime,
                                            samples: h.Samples,
                                            column: h.Column,
                                            endTime: h.EndTime,
                                            duration: h.EndTime - h.StartTime
                                        ))).OrderBy(h => h.startTime).ThenBy(n => n.column).ToList();

            #region Null column

            int keyValue = keys + 1;
            bool firstKeyFlag = true;

            int emptyColumn = Rng.Next(-1, 1 + keyValue - 2);

            while (keyValue <= Key.Value)
            {
                var confirmNull = new List<bool>();
                for (int i = 0; i <= Key.Value; i++) confirmNull.Add(false);
                var nullColumnList = new List<int>();

                if (firstKeyFlag)
                {
                    foreach (var column in maniaBeatmap.HitObjects.GroupBy(h => h.Column))
                    {
                        int count = column.Count();
                        if (!confirmNull[column.Key] && count != 0) confirmNull[column.Key] = true;
                    }

                    for (int i = 0; i < Key.Value; i++)
                        if (!confirmNull[i])
                            nullColumnList.Add(i);
                    firstKeyFlag = false;
                }

                int atLeast = 5;
                double changeTime = 0;

                bool plus = true;
                bool minus = false;
                bool next = false;

                for (int i = 0; i < locations.Count; i++)
                {
                    bool isLN = false;
                    var note = new Note();
                    var hold = new HoldNote();
                    int columnNum = locations[i].column;
                    int minusColumn = 0;
                    foreach (int nul in nullColumnList)
                        if (columnNum > nul)
                            minusColumn++;
                    columnNum -= minusColumn;

                    #endregion

                    atLeast--;

                    if (locations[i].startTime == locations[i].endTime)
                    {
                        note.StartTime = locations[i].startTime;
                        note.Samples = locations[i].samples;
                    }
                    else
                    {
                        hold.StartTime = locations[i].startTime;
                        hold.Samples = locations[i].samples;
                        hold.EndTime = locations[i].endTime;
                        isLN = true;
                    }

                    bool error = changeTime != locations[i].startTime;

                    if (keys < 4) // why you are converting 1k 2k 3k into upper keys?
                        columnNum = Rng.Next(keyValue);
                    else
                    {
                        if (error && Rng.Next(100) < Probability.Value && atLeast < 0)
                        {
                            changeTime = locations[i].startTime;
                            atLeast = keys - 2;
                            next = true;
                        }

                        if (next && plus)
                        {
                            next = false;
                            emptyColumn++;

                            if (emptyColumn > keyValue - 2)
                            {
                                plus = !plus;
                                minus = !minus;
                                emptyColumn = keyValue - 2;
                            }
                        }
                        else if (next && minus)
                        {
                            next = false;
                            emptyColumn--;

                            if (emptyColumn < -1)
                            {
                                plus = !plus;
                                minus = !minus;
                                emptyColumn = -1;
                            }
                        }

                        if (columnNum > emptyColumn) columnNum++;
                    }

                    bool overlap = ManiaModHelper.FindOverlapInList(newColumnObjects, columnNum, locations[i].startTime, locations[i].endTime);

                    if (overlap)
                    {
                        for (int k = 0; k < keyValue; k++)
                        {
                            if (!ManiaModHelper.FindOverlapInList(newColumnObjects, columnNum - k, locations[i].startTime, locations[i].endTime) && columnNum - k >= 0)
                                columnNum -= k;
                            else if (!ManiaModHelper.FindOverlapInList(newColumnObjects, columnNum + k, locations[i].startTime, locations[i].endTime) && columnNum + k <= keyValue - 1) columnNum += k;
                        }
                    }

                    if (isLN)
                    {
                        newColumnObjects.Add(new HoldNote
                        {
                            Column = columnNum,
                            StartTime = locations[i].startTime,
                            Duration = locations[i].endTime - locations[i].startTime,
                            NodeSamples = [locations[i].samples, Array.Empty<HitSampleInfo>()]
                        });
                    }
                    else
                    {
                        newColumnObjects.Add(new Note
                        {
                            Column = columnNum,
                            StartTime = locations[i].startTime,
                            Samples = locations[i].samples
                        });
                    }
                }

                for (int i = 0; i < newColumnObjects.Count; i++)
                {
                    bool overlap = false, outIndex = false;

                    if (newColumnObjects[i].Column < 0 || newColumnObjects[i].Column > Key.Value - 1)
                    {
                        outIndex = true;
                        newColumnObjects[i].Column = Rng.Next(Key.Value - 1);
                    }

                    for (int j = i + 1; j < newColumnObjects.Count; j++)
                    {
                        if (newColumnObjects[i].Column == newColumnObjects[j].Column && newColumnObjects[i].StartTime >= newColumnObjects[j].StartTime - 2
                                                                                     && newColumnObjects[i].StartTime <= newColumnObjects[j].StartTime + 2) overlap = true;

                        if (newColumnObjects[j].StartTime != newColumnObjects[j].GetEndTime())
                            if (newColumnObjects[i].Column == newColumnObjects[j].Column && newColumnObjects[i].StartTime >= newColumnObjects[j].StartTime - 2
                                                                                         && newColumnObjects[i].StartTime <= newColumnObjects[j].GetEndTime() + 2)
                                overlap = true;
                    }

                    if (outIndex) overlap = true;

                    if (!overlap)
                        fixedColumnObjects.Add(newColumnObjects[i]);
                    else
                    {
                        for (int k = 0; k < keyValue; k++)
                        {
                            if (!ManiaModHelper.FindOverlapInList(newColumnObjects[i], newColumnObjects.Where(h => h.Column == newColumnObjects[i].Column - k).ToList())
                                && newColumnObjects[i].Column - k >= 0)
                                newColumnObjects[i].Column -= k;
                            else if (!ManiaModHelper.FindOverlapInList(newColumnObjects[i], newColumnObjects.Where(h => h.Column == newColumnObjects[i].Column + k).ToList())
                                     && newColumnObjects[i].Column + k <= keyValue - 1) newColumnObjects[i].Column += k;
                        }

                        fixedColumnObjects.Add(newColumnObjects[i]);
                    }
                }

                if (keyValue < Key.Value)
                {
                    keys++;
                    keyValue = keys + 1;

                    locations = fixedColumnObjects.OfType<Note>().Select(n =>
                                                  (
                                                      startTime: n.StartTime,
                                                      samples: n.Samples,
                                                      column: n.Column,
                                                      endTime: n.StartTime,
                                                      duration: n.StartTime - n.StartTime
                                                  ))
                                                  .Concat(fixedColumnObjects.OfType<HoldNote>().Select(h =>
                                                  (
                                                      startTime: h.StartTime,
                                                      samples: h.Samples,
                                                      column: h.Column,
                                                      endTime: h.EndTime,
                                                      duration: h.EndTime - h.StartTime
                                                  ))).OrderBy(h => h.startTime).ThenBy(n => n.column).ToList();
                    ;
                    emptyColumn = -1;
                    fixedColumnObjects.Clear();
                    newColumnObjects.Clear();
                }
                else
                    break;
            }

            newObjects.AddRange(fixedColumnObjects);

            maniaBeatmap.HitObjects = newObjects;
        }

        public int ApplyOrder => ApplyOrderSetting.Value ?? 0;
    }
}

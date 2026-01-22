// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.LAsEzExtensions.Mods;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.LAsEZMania;
using osu.Game.Rulesets.Mania.LAsEzMania.Mods;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Mods.YuLiangSSSMods
{
    public class ManiaModCleaner : Mod, IApplicableAfterBeatmapConversion
    {
        public override string Name => "Cleaner";

        public override string Acronym => "CL";

        public override LocalisableString Description => EzManiaModStrings.Cleaner_Description;

        public override IconUsage? Icon => FontAwesome.Solid.Broom;

        public override ModType Type => ModType.YuLiangSSS_Mod;

        public override double ScoreMultiplier => 1;

        public override bool Ranked => false;

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                yield return ("Style", $"{Style.Value}");
                yield return ("Interval", $"{Interval.Value}ms");
                yield return ("LN Interval", $"{LNInterval.Value}ms");
            }
        }

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.Style_Label), nameof(EzManiaModStrings.Style_Description))]
        public BindableNumber<int> Style { get; set; } = new BindableInt(2)
        {
            MinValue = 1,
            MaxValue = 2,
            Precision = 1,
        };

        // DurationEveryDivide =  60 / bpm / divide * 10000
        // 125ms is equivalent to duration time between adjacent every two 120BPM 1/4 timing line.
        // 125ms 相当于 120BPM 1/4 叠键每两行的时间间隔
        // 以下个人用方便消除乱键子弹使用
        //
        // Level 1: 125.00ms
        // 120BPM - 125.00ms    130BPM - 115.38ms    140BPM - 107.14ms
        // 150BPM - 100.00ms
        //
        // Level 2: 100.00ms
        // 160BPM - 93.75ms    170BPM - 88.23ms    180BPM - 83.33ms
        // 190BPM - 78.94ms
        //
        // Level 3: 75.00ms
        // 200BPM - 75.00ms    210BPM - 71.42ms    220BPM - 68.18ms
        // 230BPM - 65.21ms    240BPM - 62.50ms
        //
        // Level4: 60.00ms
        // 250BPM - 60.00ms    260BPM - 57.69ms    270BPM - 55.55ms
        // 280BPM - 53.57ms    290BPM - 51.72ms    300BPM - 50.00ms
        //
        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.Interval_Label), nameof(EzManiaModStrings.Interval_Description))]
        public BindableNumber<int> Interval { get; set; } = new BindableInt(80)
        {
            MinValue = 1,
            MaxValue = 125,
            Precision = 1,
        };

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.LNInterval_Label), nameof(EzManiaModStrings.LNInterval_Description))]
        public BindableNumber<int> LNInterval { get; set; } = new BindableInt(30)
        {
            MinValue = 1,
            MaxValue = 125,
            Precision = 1,
        };

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            var maniaBeatmap = (ManiaBeatmap)beatmap;

            var newObjects = new List<ManiaHitObject>();

            foreach (var column in maniaBeatmap.HitObjects.GroupBy(h => h.Column))
            {
                var newColumnObjects = new List<ManiaHitObject>();

                var locations = column.OfType<Note>().Select(n => (startTime: n.StartTime, samples: n.Samples, endTime: n.StartTime))
                                      .Concat(column.OfType<HoldNote>().SelectMany(h => new[]
                                      {
                                          (startTime: h.StartTime, samples: h.GetNodeSamples(0), endTime: h.EndTime)
                                      }))
                                      .OrderBy(h => h.startTime).ToList();

                double lastStartTime = locations[0].startTime;
                double lastEndTime = locations[0].endTime;
                var lastSample = locations[0].samples;

                // Zero
                //if (lastStartTime != lastEndTime)
                //{
                //    newColumnObjects.Add(new HoldNote
                //    {
                //        Column = column.Key,
                //        StartTime = lastStartTime,
                //        Duration = lastEndTime - lastStartTime,
                //        NodeSamples = [locations[0].samples, Array.Empty<HitSampleInfo>()]
                //    });
                //}
                //else
                //{
                //    newColumnObjects.Add(new Note
                //    {
                //        Column = column.Key,
                //        StartTime = lastStartTime,
                //        Samples = locations[0].samples
                //    });
                //}

                for (int i = 0; i < locations.Count; i++)
                {
                    if (i == 0)
                    {
                        lastStartTime = locations[0].startTime;
                        lastEndTime = locations[0].endTime;
                        lastSample = locations[0].samples;
                        continue;
                    }

                    if (locations[i].startTime >= lastStartTime && locations[i].startTime <= lastEndTime)
                    {
                        locations.RemoveAt(i);
                        i--;
                        continue;
                    } // if the note in a LN

                    if (Math.Abs(locations[i].startTime - lastStartTime) <= Interval.Value)
                    {
                        if (Style.Value == 2)
                        {
                            lastStartTime = locations[i].startTime;
                            lastEndTime = locations[i].endTime;
                            lastSample = locations[i].samples;
                        }

                        locations.RemoveAt(i);
                        i--;
                        continue;
                    } // interval judgement

                    if (Math.Abs(locations[i].startTime - lastEndTime) <= LNInterval.Value)
                    {
                        if (Style.Value == 2)
                        {
                            lastStartTime = locations[i].startTime;
                            lastEndTime = locations[i].endTime;
                            lastSample = locations[i].samples;
                        }

                        locations.RemoveAt(i);
                        i--;
                        continue;
                    } // LN interval judgement

                    newColumnObjects.AddNote(lastSample, column.Key, lastStartTime, lastEndTime);

                    lastStartTime = locations[i].startTime;
                    lastEndTime = locations[i].endTime;
                    lastSample = locations[i].samples;
                }

                newColumnObjects.AddNote(lastSample, column.Key, lastStartTime, lastEndTime);

                // Last
                //if (lastStartTime != lastEndTime)
                //{
                //    newColumnObjects.Add(new HoldNote
                //    {
                //        Column = column.Key,
                //        StartTime = locations[locations.Count - 1].startTime,
                //        Duration = locations[locations.Count - 1].endTime - locations[locations.Count - 1].startTime,
                //        NodeSamples = [locations[locations.Count - 1].samples, Array.Empty<HitSampleInfo>()]
                //    });
                //}
                //else
                //{
                //    newColumnObjects.Add(new Note
                //    {
                //        Column = column.Key,
                //        StartTime = lastStartTime,
                //        Samples = locations[locations.Count - 1].samples
                //    });
                //}

                newObjects.AddRange(newColumnObjects);
            }

            maniaBeatmap.HitObjects = [.. newObjects.OrderBy(h => h.StartTime)];
        }
    }
}

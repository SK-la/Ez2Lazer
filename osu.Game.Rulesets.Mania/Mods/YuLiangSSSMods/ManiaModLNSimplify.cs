// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Mods.YuLiangSSSMods
{
    public class ManiaModLNSimplify : Mod, IApplicableAfterBeatmapConversion
    {
        public override string Name => "LN Simplify";

        public override string Acronym => "SP";

        public override double ScoreMultiplier => 1;

        public override LocalisableString Description => "Simplifies rhythms by converting.";

        public override IconUsage? Icon => FontAwesome.Solid.YinYang;

        public override ModType Type => ModType.YuLiangSSS_Mod;

        public override bool Ranked => false;

        public readonly double ERROR = 1.5;

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                yield return ("Limit Divide", $"{LimitDivide.Value}");
                yield return ("Easier Divide", $"{EasierDivide.Value}");
                yield return ("Longest LN", $"{Gap.Value}");
                yield return ("Shortest LN", $"{Len.Value}");
            }
        }

        [SettingSource("Limit Divide", "Select limit.")]
        public BindableInt LimitDivide { get; set; } = new BindableInt(4)
        {
            MinValue = 1,
            MaxValue = 16,
            Precision = 1,
        };

        [SettingSource("Easier Divide", "Select complexity.")]
        public BindableInt EasierDivide { get; set; } = new BindableInt(4)
        {
            MinValue = 1,
            MaxValue = 16,
            Precision = 1,
        };

        [SettingSource("Gap", "Longest LN.")]
        public BindableBool Gap { get; set; } = new BindableBool(true);

        [SettingSource("Len", "Shortest LN.")]
        public BindableBool Len { get; set; } = new BindableBool(true);

        //[SettingSource("Allowable ms", "Minimum ms.")]
        //public BindableInt Allowable { get; set; } = new BindableInt(10)
        //{

        //};

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            var maniaBeatmap = (ManiaBeatmap)beatmap;

            var newObjects = new List<ManiaHitObject>();

            foreach (var column in maniaBeatmap.HitObjects.GroupBy(h => h.Column))
            {
                var locations = column.OfType<Note>().Select(n => (startTime: n.StartTime, endTime: n.StartTime, samples: n.Samples))
                    .Concat(column.OfType<HoldNote>().SelectMany(h => new[]
                    {
                        (startTime: h.StartTime, endTime: h.EndTime, samples: h.GetNodeSamples(0))
                    }))
                    .OrderBy(h => h.startTime).ToList();

                var newColumnObjects = new List<ManiaHitObject>();

                for (int i = 0; i < locations.Count - 1; i++)
                {
                    if (locations[i].startTime == locations[i].endTime)
                    {
                        newColumnObjects.Add(new Note
                        {
                            Column = column.Key,
                            StartTime = locations[i].startTime,
                            Samples = locations[i].samples,
                        });
                        continue;
                    }
                    double beatLength = beatmap.ControlPointInfo.TimingPointAt(locations[i].startTime).BeatLength;

                    double gap = locations[i + 1].startTime - locations[i].endTime;

                    double timeDivide = beatLength / LimitDivide.Value;

                    double easierDivide = beatLength / EasierDivide.Value;

                    double duration = locations[i].endTime - locations[i].startTime;

                    if (duration < timeDivide + ERROR && Len.Value)
                    {
                        duration = easierDivide;
                        gap = locations[i + 1].startTime - (locations[i].startTime + duration);
                        if (gap < timeDivide + ERROR)
                        {
                            duration = locations[i + 1].startTime - locations[i].startTime - easierDivide;
                        }
                    }

                    if (gap < timeDivide + ERROR && Gap.Value)
                    {
                        duration = locations[i + 1].startTime - locations[i].startTime - easierDivide;
                    }

                    if (duration < easierDivide - ERROR)
                    {
                        duration = 0;
                    }

                    if (duration > 0)
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
                            Samples = locations[i].samples,
                        });
                    }
                }

                int last = locations.Count - 1;

                if (locations[last].startTime == locations[last].endTime)
                {
                    newColumnObjects.Add(new Note
                    {
                        Column = column.Key,
                        StartTime = locations[last].startTime,
                        Samples = locations[last].samples,
                    });
                }
                else
                {
                    newColumnObjects.Add(new HoldNote
                    {
                        Column = column.Key,
                        StartTime = locations[last].startTime,
                        EndTime = locations[last].endTime,
                        NodeSamples = [locations[last].samples, Array.Empty<HitSampleInfo>()]
                    });
                }

                newObjects.AddRange(newColumnObjects);
            }

            maniaBeatmap.HitObjects = [.. newObjects.OrderBy(h => h.StartTime)];

            //maniaBeatmap.Breaks.Clear();
        }
    }
}

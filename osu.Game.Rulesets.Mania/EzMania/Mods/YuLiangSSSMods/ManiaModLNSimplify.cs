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
using osu.Game.EzOsuGame.Localization;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.EzMania.Mods.YuLiangSSSMods
{
    public class ManiaModLNSimplify : Mod, IApplicableAfterBeatmapConversion, IEzApplyOrder
    {
        public override string Name => "LN Simplify";

        public override string Acronym => "SP";

        public override double ScoreMultiplier => 1;

        public override LocalisableString Description => LNSimplifyStrings.LN_SIMPLIFY_DESCRIPTION;

        public override IconUsage? Icon => FontAwesome.Solid.YinYang;

        public override ModType Type => ModType.YuLiangSSS_Mod;

        public override bool Ranked => false;
        public override bool ValidForMultiplayer => true;
        public override bool ValidForFreestyleAsRequiredMod => false;

        public readonly double Error = 1.5;

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                yield return (LNSimplifyStrings.LIMIT_DIVIDE_LABEL, $"{LimitDivide.Value}");
                yield return (LNSimplifyStrings.EASIER_DIVIDE_LABEL, $"{EasierDivide.Value}");
                yield return (LNSimplifyStrings.LONGEST_LN_LABEL, $"{Gap.Value}");
                yield return (LNSimplifyStrings.SHORTEST_LN_LABEL, $"{Len.Value}");
            }
        }

        [SettingSource(typeof(LNSimplifyStrings), nameof(LNSimplifyStrings.LIMIT_DIVIDE_LABEL), nameof(LNSimplifyStrings.LIMIT_DIVIDE_DESCRIPTION))]
        public BindableInt LimitDivide { get; set; } = new BindableInt(4)
        {
            MinValue = 1,
            MaxValue = 16,
            Precision = 1,
        };

        [SettingSource(typeof(LNSimplifyStrings), nameof(LNSimplifyStrings.EASIER_DIVIDE_LABEL), nameof(LNSimplifyStrings.EASIER_DIVIDE_DESCRIPTION))]
        public BindableInt EasierDivide { get; set; } = new BindableInt(4)
        {
            MinValue = 1,
            MaxValue = 16,
            Precision = 1,
        };

        [SettingSource(typeof(LNSimplifyStrings), nameof(LNSimplifyStrings.LONGEST_LN_LABEL), nameof(LNSimplifyStrings.LONGEST_LN_DESCRIPTION))]
        public BindableBool Gap { get; set; } = new BindableBool(true);

        [SettingSource(typeof(LNSimplifyStrings), nameof(LNSimplifyStrings.SHORTEST_LN_LABEL), nameof(LNSimplifyStrings.SHORTEST_LN_DESCRIPTION))]
        public BindableBool Len { get; set; } = new BindableBool(true);

        [SettingSource(typeof(EzCommonModStrings), nameof(EzCommonModStrings.APPLY_ORDER_LABEL), nameof(EzCommonModStrings.APPLY_ORDER_DESCRIPTION))]
        public BindableNumber<int> ApplyOrderIndex { get; } = new BindableInt(50)
        {
            MinValue = 0,
            MaxValue = 100
        };

        public int ApplyOrder => ApplyOrderIndex.Value;

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

                    if (duration < timeDivide + Error && Len.Value)
                    {
                        duration = easierDivide;
                        gap = locations[i + 1].startTime - (locations[i].startTime + duration);

                        if (gap < timeDivide + Error)
                        {
                            duration = locations[i + 1].startTime - locations[i].startTime - easierDivide;
                        }
                    }

                    if (gap < timeDivide + Error && Gap.Value)
                    {
                        duration = locations[i + 1].startTime - locations[i].startTime - easierDivide;
                    }

                    if (duration < easierDivide - Error)
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

    public static class LNSimplifyStrings
    {
        public static readonly LocalisableString LN_SIMPLIFY_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("通过转换简化节奏", "Simplifies rhythms by converting.");
        public static readonly LocalisableString LIMIT_DIVIDE_LABEL = new EzLocalizationManager.EzLocalisableString("限制分割", "Limit Divide");
        public static readonly LocalisableString LIMIT_DIVIDE_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("选择限制", "Select limit.");
        public static readonly LocalisableString EASIER_DIVIDE_LABEL = new EzLocalizationManager.EzLocalisableString("简化分割", "Easier Divide");
        public static readonly LocalisableString EASIER_DIVIDE_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("选择复杂度", "Select complexity.");
        public static readonly LocalisableString LONGEST_LN_LABEL = new EzLocalizationManager.EzLocalisableString("最长LN", "Longest LN");
        public static readonly LocalisableString LONGEST_LN_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("最长LN", "Longest LN.");
        public static readonly LocalisableString SHORTEST_LN_LABEL = new EzLocalizationManager.EzLocalisableString("最短LN", "Shortest LN");
        public static readonly LocalisableString SHORTEST_LN_DESCRIPTION = new EzLocalizationManager.EzLocalisableString("最短LN", "Shortest LN.");
    }
}

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
using osu.Game.Rulesets.Mania.LAsEZMania;
using osu.Game.Rulesets.Mania.LAsEzMania.Mods;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Mods.YuLiangSSSMods
{
    public class ManiaModDoublePlay : Mod, IApplicableToBeatmapConverter, IApplicableAfterBeatmapConversion
    {
        public override string Name => "Double Play";

        public override string Acronym => "DP";

        public override IconUsage? Icon => FontAwesome.Solid.Sun;

        public override ModType Type => ModType.YuLiangSSS_Mod;

        public override LocalisableString Description => "Convert 4k to 8k (Double 4k).";

        public override double ScoreMultiplier => 1;

        public override bool Ranked => false;
        public override bool ValidForMultiplayer => true;
        public override bool ValidForFreestyleAsRequiredMod => false;

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                yield return ("Style", $"Style {Style.Value}");
            }
        }

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.DoublePlayStyle_Label), nameof(EzManiaModStrings.DoublePlayStyle_Description))]
        public BindableNumber<int> Style { get; } = new BindableInt(1)
        {
            MinValue = 1,
            MaxValue = 8,
            Precision = 1,
        };

        public void ApplyToBeatmapConverter(IBeatmapConverter converter)
        {
            var mbc = (ManiaBeatmapConverter)converter;

            float keys = mbc.TotalColumns;

            if (keys != 4)
            {
                return;
            }

            mbc.TargetColumns = 8;
        }

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            var maniaBeatmap = (ManiaBeatmap)beatmap;

            int keys = (int)maniaBeatmap.Difficulty.CircleSize;

            if (keys != 4)
            {
                return;
            }

            var newObjects = new List<ManiaHitObject>();

            var newColumnObjects = new List<ManiaHitObject>();

            var locations = maniaBeatmap.HitObjects.OfType<Note>().Select(n => (
                                            startTime: n.StartTime,
                                            samples: n.Samples,
                                            column: n.Column,
                                            endTime: n.StartTime
                                        ))
                                        .Concat(maniaBeatmap.HitObjects.OfType<HoldNote>().Select(h => (
                                            startTime: h.StartTime,
                                            samples: h.Samples,
                                            column: h.Column,
                                            endTime: h.EndTime
                                        ))).OrderBy(h => h.startTime).ToList();

            for (int i = 0; i < locations.Count; i++)
            {
                bool isLN = false;
                var note = new Note();
                var hold = new HoldNote();
                int columnIndex = locations[i].column;

                switch (columnIndex)
                {
                    case 1:
                    {
                        columnIndex = 0;
                    }
                        break;

                    case 3:
                    {
                        columnIndex = 1;
                    }
                        break;

                    case 5:
                    {
                        columnIndex = 2;

                        if (Style.Value >= 5 && Style.Value <= 8)
                        {
                            columnIndex = 4;
                        }
                    }
                        break;

                    case 7:
                    {
                        columnIndex = 3;

                        if (Style.Value >= 5 && Style.Value <= 8)
                        {
                            columnIndex = 5;
                        }
                    }
                        break;
                }

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

                if (isLN)
                {
                    switch (Style.Value)
                    {
                        case 1:
                        {
                            newColumnObjects.Add(new HoldNote
                            {
                                Column = columnIndex,
                                StartTime = locations[i].startTime,
                                Duration = locations[i].endTime - locations[i].startTime,
                                NodeSamples = [locations[i].samples, Array.Empty<HitSampleInfo>()]
                            });
                            newColumnObjects.Add(new HoldNote
                            {
                                Column = 4 + columnIndex,
                                StartTime = locations[i].startTime,
                                Duration = locations[i].endTime - locations[i].startTime,
                                NodeSamples = [locations[i].samples, Array.Empty<HitSampleInfo>()]
                            });
                        }
                            break;

                        case 2:
                        {
                            newColumnObjects.Add(new HoldNote
                            {
                                Column = 3 - columnIndex,
                                StartTime = locations[i].startTime,
                                Duration = locations[i].endTime - locations[i].startTime,
                                NodeSamples = [locations[i].samples, Array.Empty<HitSampleInfo>()]
                            });
                            newColumnObjects.Add(new HoldNote
                            {
                                Column = 7 - columnIndex,
                                StartTime = locations[i].startTime,
                                Duration = locations[i].endTime - locations[i].startTime,
                                NodeSamples = [locations[i].samples, Array.Empty<HitSampleInfo>()]
                            });
                        }
                            break;

                        case 3:
                        {
                            newColumnObjects.Add(new HoldNote
                            {
                                Column = columnIndex,
                                StartTime = locations[i].startTime,
                                Duration = locations[i].endTime - locations[i].startTime,
                                NodeSamples = [locations[i].samples, Array.Empty<HitSampleInfo>()]
                            });
                            newColumnObjects.Add(new HoldNote
                            {
                                Column = 7 - columnIndex,
                                StartTime = locations[i].startTime,
                                Duration = locations[i].endTime - locations[i].startTime,
                                NodeSamples = [locations[i].samples, Array.Empty<HitSampleInfo>()]
                            });
                        }
                            break;

                        case 4:
                        {
                            newColumnObjects.Add(new HoldNote
                            {
                                Column = 3 - columnIndex,
                                StartTime = locations[i].startTime,
                                Duration = locations[i].endTime - locations[i].startTime,
                                NodeSamples = [locations[i].samples, Array.Empty<HitSampleInfo>()]
                            });
                            newColumnObjects.Add(new HoldNote
                            {
                                Column = 4 + columnIndex,
                                StartTime = locations[i].startTime,
                                Duration = locations[i].endTime - locations[i].startTime,
                                NodeSamples = [locations[i].samples, Array.Empty<HitSampleInfo>()]
                            });
                        }
                            break;

                        case 5:
                        {
                            newColumnObjects.Add(new HoldNote
                            {
                                Column = columnIndex,
                                StartTime = locations[i].startTime,
                                Duration = locations[i].endTime - locations[i].startTime,
                                NodeSamples = [locations[i].samples, Array.Empty<HitSampleInfo>()]
                            });
                            newColumnObjects.Add(new HoldNote
                            {
                                Column = 2 + columnIndex,
                                StartTime = locations[i].startTime,
                                Duration = locations[i].endTime - locations[i].startTime,
                                NodeSamples = [locations[i].samples, Array.Empty<HitSampleInfo>()]
                            });
                        }
                            break;

                        case 6:
                        {
                            if (columnIndex <= 1)
                            {
                                columnIndex = 3 - columnIndex;
                            }

                            if (columnIndex >= 4)
                            {
                                columnIndex = 7 - columnIndex + 4;
                            }

                            newColumnObjects.Add(new HoldNote
                            {
                                Column = columnIndex,
                                StartTime = locations[i].startTime,
                                Duration = locations[i].endTime - locations[i].startTime,
                                NodeSamples = [locations[i].samples, Array.Empty<HitSampleInfo>()]
                            });
                            newColumnObjects.Add(new HoldNote
                            {
                                Column = columnIndex - 2,
                                StartTime = locations[i].startTime,
                                Duration = locations[i].endTime - locations[i].startTime,
                                NodeSamples = [locations[i].samples, Array.Empty<HitSampleInfo>()]
                            });
                        }
                            break;

                        case 7:
                        case 8:
                        {
                            if (Style.Value == 8)
                            {
                                if (columnIndex == 0 || columnIndex == 4)
                                {
                                    columnIndex++;
                                }
                                else if (columnIndex == 1 || columnIndex == 5)
                                {
                                    columnIndex--;
                                }
                            }

                            if (columnIndex < 4)
                            {
                                newColumnObjects.Add(new HoldNote
                                {
                                    Column = columnIndex,
                                    StartTime = locations[i].startTime,
                                    Duration = locations[i].endTime - locations[i].startTime,
                                    NodeSamples = [locations[i].samples, Array.Empty<HitSampleInfo>()]
                                });
                                newColumnObjects.Add(new HoldNote
                                {
                                    Column = 3 - columnIndex,
                                    StartTime = locations[i].startTime,
                                    Duration = locations[i].endTime - locations[i].startTime,
                                    NodeSamples = [locations[i].samples, Array.Empty<HitSampleInfo>()]
                                });
                            }

                            if (columnIndex > 3)
                            {
                                newColumnObjects.Add(new HoldNote
                                {
                                    Column = columnIndex,
                                    StartTime = locations[i].startTime,
                                    Duration = locations[i].endTime - locations[i].startTime,
                                    NodeSamples = [locations[i].samples, Array.Empty<HitSampleInfo>()]
                                });
                                newColumnObjects.Add(new HoldNote
                                {
                                    Column = 7 - (columnIndex - 4),
                                    StartTime = locations[i].startTime,
                                    Duration = locations[i].endTime - locations[i].startTime,
                                    NodeSamples = [locations[i].samples, Array.Empty<HitSampleInfo>()]
                                });
                            }
                        }
                            break;
                    }
                }
                else
                {
                    switch (Style.Value)
                    {
                        case 1:
                        {
                            newColumnObjects.Add(new Note
                            {
                                Column = columnIndex,
                                StartTime = locations[i].startTime,
                                Samples = locations[i].samples
                            });
                            newColumnObjects.Add(new Note
                            {
                                Column = columnIndex + 4,
                                StartTime = locations[i].startTime,
                                Samples = locations[i].samples
                            });
                        }
                            break;

                        case 2:
                        {
                            newColumnObjects.Add(new Note
                            {
                                Column = 3 - columnIndex,
                                StartTime = locations[i].startTime,
                                Samples = locations[i].samples
                            });
                            newColumnObjects.Add(new Note
                            {
                                Column = 7 - columnIndex,
                                StartTime = locations[i].startTime,
                                Samples = locations[i].samples
                            });
                        }
                            break;

                        case 3:
                        {
                            newColumnObjects.Add(new Note
                            {
                                Column = columnIndex,
                                StartTime = locations[i].startTime,
                                Samples = locations[i].samples
                            });
                            newColumnObjects.Add(new Note
                            {
                                Column = 7 - columnIndex,
                                StartTime = locations[i].startTime,
                                Samples = locations[i].samples
                            });
                        }
                            break;

                        case 4:
                        {
                            newColumnObjects.Add(new Note
                            {
                                Column = 3 - columnIndex,
                                StartTime = locations[i].startTime,
                                Samples = locations[i].samples
                            });
                            newColumnObjects.Add(new Note
                            {
                                Column = 4 + columnIndex,
                                StartTime = locations[i].startTime,
                                Samples = locations[i].samples
                            });
                        }
                            break;

                        case 5:
                        {
                            newColumnObjects.Add(new Note
                            {
                                Column = columnIndex,
                                StartTime = locations[i].startTime,
                                Samples = locations[i].samples
                            });
                            newColumnObjects.Add(new Note
                            {
                                Column = 2 + columnIndex,
                                StartTime = locations[i].startTime,
                                Samples = locations[i].samples
                            });
                        }
                            break;

                        case 6:
                        {
                            if (columnIndex <= 1)
                            {
                                columnIndex = 3 - columnIndex;
                            }

                            if (columnIndex >= 4)
                            {
                                columnIndex = 7 - columnIndex + 4;
                            }

                            newColumnObjects.Add(new Note
                            {
                                Column = columnIndex,
                                StartTime = locations[i].startTime,
                                Samples = locations[i].samples
                            });
                            newColumnObjects.Add(new Note
                            {
                                Column = columnIndex - 2,
                                StartTime = locations[i].startTime,
                                Samples = locations[i].samples
                            });
                        }
                            break;

                        case 7:
                        case 8:
                        {
                            if (Style.Value == 8)
                            {
                                if (columnIndex == 0 || columnIndex == 4)
                                {
                                    columnIndex++;
                                }
                                else if (columnIndex == 1 || columnIndex == 5)
                                {
                                    columnIndex--;
                                }
                            }

                            if (columnIndex < 4)
                            {
                                newColumnObjects.Add(new Note
                                {
                                    Column = columnIndex,
                                    StartTime = locations[i].startTime,
                                    Samples = locations[i].samples
                                });
                                newColumnObjects.Add(new Note
                                {
                                    Column = 3 - columnIndex,
                                    StartTime = locations[i].startTime,
                                    Samples = locations[i].samples
                                });
                            }

                            if (columnIndex > 3)
                            {
                                newColumnObjects.Add(new HoldNote
                                {
                                    Column = columnIndex,
                                    StartTime = locations[i].startTime,
                                    Duration = locations[i].endTime - locations[i].startTime,
                                    NodeSamples = [locations[i].samples, Array.Empty<HitSampleInfo>()]
                                });
                                newColumnObjects.Add(new HoldNote
                                {
                                    Column = 7 - (columnIndex - 4),
                                    StartTime = locations[i].startTime,
                                    Duration = locations[i].endTime - locations[i].startTime,
                                    NodeSamples = [locations[i].samples, Array.Empty<HitSampleInfo>()]
                                });
                            }
                        }
                            break;
                    }
                }
            }

            newObjects.AddRange(newColumnObjects);
            maniaBeatmap.HitObjects = newObjects;
        }
    }
}

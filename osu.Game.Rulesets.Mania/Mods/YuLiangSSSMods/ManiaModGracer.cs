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
using osu.Game.Rulesets.Mania.LAsEZMania;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Mods.YuLiangSSSMods
{
    public class ManiaModGracer : Mod, IApplicableAfterBeatmapConversion, IHasSeed
    {
        public const double MIN_INTERVAL = 10;

        public const double PLUS_INTERVAL = 2.2;

        public override string Name => "Gracer";

        public override string Acronym => "GR";

        public override double ScoreMultiplier => 1;

        public override IconUsage? Icon => FontAwesome.Solid.Star;

        public override bool Ranked => false;

        public override LocalisableString Description => EzManiaModStrings.Gracer_Description;

        public override ModType Type => ModType.YuLiangSSS_Mod;

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                yield return ("Bias", $"{Bias.Value}");
                yield return ("Interval", $"{Interval.Value}ms");
                yield return ("Probability", $"{Probability.Value}%");
                yield return ("Seed", $"{(Seed.Value == null ? "Null" : Seed.Value)}");
            }
        }

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.Bias_Label), nameof(EzManiaModStrings.Bias_Description))]
        public BindableNumber<int> Bias { get; set; } = new BindableInt(16)
        {
            MinValue = 1,
            MaxValue = 50,
            Precision = 1
        };

        // If interval is too high which will have bug taken place.
        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.Interval_Label), nameof(EzManiaModStrings.Interval_Description))]
        public BindableNumber<double> Interval { get; set; } = new BindableNumber<double>(20)
        {
            MinValue = 1,
            MaxValue = 50,
            Precision = 1
        };

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.Probability_Label), nameof(EzManiaModStrings.Probability_Description))]
        public BindableNumber<int> Probability { get; set; } = new BindableInt(100)
        {
            MinValue = 0,
            MaxValue = 100,
            Precision = 5
        };

        [SettingSource(typeof(EzManiaModStrings), nameof(EzManiaModStrings.Seed_Label), nameof(EzManiaModStrings.Seed_Description), SettingControlType = typeof(SettingsNumberBox))]
        public Bindable<int?> Seed { get; } = new Bindable<int?>();

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            var maniaBeatmap = (ManiaBeatmap)beatmap;

            var newObjects = new List<ManiaHitObject>();

            Seed.Value ??= RNG.Next();
            var rng = new Random((int)Seed.Value);

            var newColumnObjects = new List<ManiaHitObject>();

            foreach (var column in maniaBeatmap.HitObjects.GroupBy(h => h.Column))
            {
                var locations = column.OfType<Note>().Select(n => (startTime: n.StartTime, samples: n.Samples, endTime: n.StartTime))
                                      .Concat(column.OfType<HoldNote>().SelectMany(h => new[]
                                      {
                                          (startTime: h.StartTime, samples: h.GetNodeSamples(0), endTime: h.EndTime)
                                      }))
                                      .OrderBy(h => h.startTime).ToList();

                double lastStartTime = int.MinValue;
                double lastEndTime = int.MaxValue;
                bool? lastIsLN = null;

                for (int i = 0; i < locations.Count; i++)
                {
                    bool isLN = locations[i].startTime != locations[i].endTime;
                    double startTime = locations[i].startTime + rng.Next(-Bias.Value, Bias.Value) + rng.NextDouble();
                    double endTime = locations[i].endTime + rng.Next(-Bias.Value, Bias.Value) + rng.NextDouble();

                    if (lastStartTime != int.MinValue && lastEndTime != int.MaxValue)
                    {
                        if (lastIsLN == true)
                        {
                            while (startTime >= lastStartTime && startTime <= lastEndTime + Interval.Value)
                            {
                                startTime += PLUS_INTERVAL;
                            }

                            while (endTime <= startTime /* + Interval.Value*/)
                            {
                                endTime += PLUS_INTERVAL;
                            }
                        }
                        else
                        {
                            while (startTime <= lastStartTime + Interval.Value)
                            {
                                startTime += PLUS_INTERVAL;
                            }

                            while (endTime <= startTime /* + Interval.Value */)
                            {
                                endTime += PLUS_INTERVAL;
                            }
                        }
                    }

                    if (rng.Next(100) < Probability.Value)
                    {
                        if (locations[i].startTime != locations[i].endTime)
                        {
                            newColumnObjects.Add(new HoldNote
                            {
                                Column = column.Key,
                                StartTime = startTime,
                                Duration = endTime - startTime,
                                NodeSamples = [locations[i].samples, Array.Empty<HitSampleInfo>()]
                            });
                        }
                        else
                        {
                            newColumnObjects.Add(new Note
                            {
                                Column = column.Key,
                                StartTime = startTime,
                                Samples = locations[i].samples
                            });
                        }
                    }
                    else
                    {
                        if (locations[i].startTime != locations[i].endTime)
                        {
                            newColumnObjects.Add(new HoldNote
                            {
                                Column = column.Key,
                                StartTime = locations[i].startTime,
                                Duration = locations[i].endTime - locations[i].startTime,
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

                    lastStartTime = startTime;
                    lastEndTime = endTime;
                    lastIsLN = isLN;
                }
            }

            newObjects.AddRange(newColumnObjects);
            maniaBeatmap.HitObjects = [.. newObjects.OrderBy(h => h.StartTime)];
        }
    }
}

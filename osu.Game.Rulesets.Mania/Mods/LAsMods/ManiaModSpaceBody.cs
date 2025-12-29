// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Mods.LAsMods
{
    public class ManiaModSpaceBody : Mod, IApplicableAfterBeatmapConversion
    {
        public override string Name => "SpaceBody";

        public override string Acronym => "SB";
        public override double ScoreMultiplier => 1;

        public override LocalisableString Description => "LaMod: Full LN 面海, 可调面缝";

        public override ModType Type => ModType.LA_Mod;

        public override Type[] IncompatibleMods => new[] { typeof(ManiaModHoldOff) };

        [SettingSource("Space Body", "面海缝隙的最大间隔, 1/?拍", SettingControlType = typeof(MultiplierSettingsSlider))]
        public BindableNumber<double> SpaceBeat { get; } = new BindableDouble(4)
        {
            MinValue = 1,
            MaxValue = 16,
            Precision = 1
        };

        [SettingSource("Add Shield", "给面海加个盾牌，超难。Add Shield is super hard.", SettingControlType = typeof(MultiplierSettingsSlider))]
        public BindableBool Shield { get; } = new BindableBool();

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            var maniaBeatmap = (ManiaBeatmap)beatmap;

            var newObjects = new List<ManiaHitObject>();

            foreach (var column in maniaBeatmap.HitObjects.GroupBy(h => h.Column))
            {
                var newColumnObjects = new List<ManiaHitObject>();

                var locations = Shield.Value
                    ? column.OfType<Note>().Select(n => (startTime: n.StartTime, samples: n.Samples))
                            .Concat(column.OfType<HoldNote>().SelectMany(h => new[]
                            {
                                (startTime: h.StartTime, samples: h.GetNodeSamples(0)),
                                (startTime: h.EndTime, samples: h.GetNodeSamples(1))
                            }))
                            .OrderBy(h => h.startTime).ToList()
                    : column.OfType<Note>().Select(n => (startTime: n.StartTime, samples: n.Samples))
                            .Concat(column.OfType<HoldNote>().SelectMany(h => new[]
                            {
                                (startTime: h.StartTime, samples: h.GetNodeSamples(0)),
                            }))
                            .OrderBy(h => h.startTime).ToList();

                for (int i = 0; i < locations.Count - 1; i++)
                {
                    // Full duration of the hold note.
                    double duration = locations[i + 1].startTime - locations[i].startTime;

                    // Beat length at the end of the hold note.
                    double beatLength = beatmap.ControlPointInfo.TimingPointAt(locations[i + 1].startTime).BeatLength;

                    // Decrease the duration by at most a 1/4 beat to ensure there's no instantaneous notes.
                    // duration = Math.Max(duration / 2, duration - beatLength / 4);
                    duration = Math.Max(duration / 2, duration - beatLength / SpaceBeat.Value);

                    newColumnObjects.Add(new HoldNote
                    {
                        Column = column.Key,
                        StartTime = locations[i].startTime,
                        Duration = duration,
                        NodeSamples = new List<IList<HitSampleInfo>> { locations[i].samples, Array.Empty<HitSampleInfo>() }
                    });
                }

                newObjects.AddRange(newColumnObjects);
            }

            maniaBeatmap.HitObjects = newObjects.OrderBy(h => h.StartTime).ToList();

            // No breaks
            maniaBeatmap.Breaks.Clear();
        }
    }
}

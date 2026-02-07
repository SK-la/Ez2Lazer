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
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Configuration;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Mods.LAsMods
{
    public class ManiaModPatternShiftDelay : Mod, IApplicableAfterBeatmapConversion, IHasSeed, IHasApplyOrder
    {
        public override string Name => "Pattern Shift Delay";

        public override string Acronym => "PSD";

        public override double ScoreMultiplier => 1;

        public override LocalisableString Description => "Randomly offset some notes in time.";

        public override IconUsage? Icon => FontAwesome.Solid.Clock;

        public override ModType Type => ModType.LA_Mod;

        public override bool Ranked => false;
        public override bool ValidForMultiplayer => true;
        public override bool ValidForFreestyleAsRequiredMod => false;

        [SettingSource("Delay Level", "0=off, 1-10. Higher levels shift more notes per row.")]
        public BindableNumber<int> DelayLevel { get; } = new BindableInt(0)
        {
            MinValue = 0,
            MaxValue = 10,
            Precision = 1
        };

        [SettingSource("Seed", "Use a custom seed instead of a random one", SettingControlType = typeof(SettingsNumberBox))]
        public Bindable<int?> Seed { get; } = new Bindable<int?>();

        [SettingSource("Apply Order", "Lower values apply earlier.")]
        public BindableNumber<int> ApplyOrderIndex { get; } = new BindableInt(50)
        {
            MinValue = 0,
            MaxValue = 100
        };

        public int ApplyOrder => ApplyOrderIndex.Value;

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                yield return ("Delay Level", $"{DelayLevel.Value}");
                yield return ("Seed", Seed.Value?.ToString() ?? "Random");
            }
        }

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            if (DelayLevel.Value <= 0)
                return;

            Seed.Value ??= RNG.Next();
            var rng = new Random(Seed.Value.Value);

            var chords = beatmap.HitObjects
                                .OfType<ManiaHitObject>()
                                .GroupBy(h => h.StartTime)
                                .OrderBy(g => g.Key)
                                .Select(g => g.ToList())
                                .ToList();

            foreach (var chord in chords)
            {
                int noteCount = chord.Count;
                int maxShift = getMaxShiftCount(DelayLevel.Value, noteCount);

                if (maxShift <= 0)
                    continue;

                double beatLength = beatmap.ControlPointInfo.TimingPointAt(chord[0].StartTime).BeatLength;
                double offsetAmount = beatLength * getDelayBeatFraction(DelayLevel.Value);

                var indexes = Enumerable.Range(0, noteCount).OrderBy(_ => rng.Next()).Take(maxShift).ToList();

                foreach (int index in indexes)
                {
                    var obj = chord[index];
                    double direction = rng.NextDouble() < 0.5 ? -1 : 1;
                    double offset = direction * offsetAmount;

                    if (obj is HoldNote hold)
                    {
                        double newStart = Math.Max(0, hold.StartTime + offset);
                        double newEnd = Math.Max(newStart, hold.EndTime + offset);
                        hold.StartTime = newStart;
                        hold.EndTime = newEnd;
                    }
                    else
                    {
                        obj.StartTime = Math.Max(0, obj.StartTime + offset);
                    }
                }
            }

            ManiaNoteCleanupTool.CleanupBeatmap((ManiaBeatmap)beatmap, DelayLevel.Value, 2, seed: Seed.Value.Value);
        }

        private static int getMaxShiftCount(int level, int noteCount)
        {
            if (noteCount <= 0)
                return 0;

            if (level <= 3)
                return Math.Max(0, Math.Min(level, noteCount - level));

            if (level <= 6)
                return Math.Max(0, Math.Min(level, noteCount - 1));

            return Math.Min(level, noteCount);
        }

        private static double getDelayBeatFraction(int level)
        {
            double t;

            if (level <= 3)
                t = (level - 1) / 2.0;
            else if (level <= 6)
                t = (level - 4) / 2.0;
            else
                t = (level - 7) / 3.0;

            return (1.0 / 16.0) * (1 + t);
        }
    }
}

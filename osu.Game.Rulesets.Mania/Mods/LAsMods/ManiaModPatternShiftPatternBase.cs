// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Mods.LAsMods
{
    public abstract class ManiaModPatternShiftPatternBase : Mod, IApplicableAfterBeatmapConversion, IHasSeed, IHasApplyOrder
    {
        protected const double TIME_TOLERANCE = 3;

        protected abstract KeyPatternType PatternType { get; }
        protected abstract string PatternName { get; }
        protected abstract string PatternAcronym { get; }

        protected abstract void ApplyPatternForWindow(List<ManiaHitObject> windowObjects,
                                                      ManiaBeatmap beatmap,
                                                      double windowStart,
                                                      double windowEnd,
                                                      KeyPatternSettings settings,
                                                      Random rng,
                                                      int maxIterationsPerWindow);

        protected virtual int DefaultLevel => 0;
        protected virtual EzOscillator.Waveform DefaultWaveform => EzOscillator.Waveform.Sine;
        protected virtual int DefaultOscillationBeats => 1;
        protected virtual int DefaultWindowProcessInterval => 1;
        protected virtual int DefaultWindowProcessOffset => 1;
        protected virtual int DefaultApplyOrder => 50;

        public override string Name => $"Pattern Shift {PatternName}";

        public override string Acronym => PatternAcronym;

        public override double ScoreMultiplier => 1;

        public override LocalisableString Description => $"Pattern shift ({PatternName})";

        public override IconUsage? Icon => FontAwesome.Solid.Magic;

        public override ModType Type => ModType.LA_Mod;

        public override bool Ranked => false;
        public override bool ValidForMultiplayer => false;
        public override bool ValidForFreestyleAsRequiredMod => false;

        [SettingSource("Level", "0=off, 1-10. Controls how many notes are generated per window.")]
        public BindableNumber<int> Level { get; } = new BindableInt(0)
        {
            MinValue = 0,
            MaxValue = 10,
            Precision = 1
        };

        [SettingSource("Waveform", "Oscillator waveform used to vary pattern intensity.")]
        public Bindable<EzOscillator.Waveform> Waveform { get; } = new Bindable<EzOscillator.Waveform>(EzOscillator.Waveform.Sine);

        [SettingSource("Oscillation Beats", "Beat interval for oscillator changes. 1=every beat.")]
        public BindableNumber<int> OscillationBeats { get; } = new BindableInt(2)
        {
            MinValue = 1,
            MaxValue = 8,
            Precision = 1
        };

        [SettingSource("Window Interval", "Process every N half-beats. 1=every half-beat.")]
        public BindableNumber<int> WindowInterval { get; } = new BindableInt(2)
        {
            MinValue = 1,
            MaxValue = 4,
            Precision = 1
        };

        [SettingSource("Window Start Offset", "1-4: first to fourth half-beat.")]
        public BindableNumber<int> WindowStartOffset { get; } = new BindableInt(1)
        {
            MinValue = 1,
            MaxValue = 4,
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
                yield return ("Level", $"{Level.Value}");
                yield return ("Waveform", Waveform.Value.ToString());
                yield return ("Oscillation Beats", $"{OscillationBeats.Value}");
                yield return ("Window Interval", $"{WindowInterval.Value}");
                yield return ("Window Start Offset", $"{WindowStartOffset.Value}");
                yield return ("Seed", Seed.Value?.ToString() ?? "Random");
            }
        }

        protected virtual int WindowProcessInterval => Math.Clamp(WindowInterval.Value, 1, 4);
        protected virtual int WindowProcessOffset => Math.Clamp(WindowStartOffset.Value - 1, 0, WindowProcessInterval - 1);
        protected virtual int MaxIterationsPerWindow => 1;

        protected ManiaModPatternShiftPatternBase()
        {
            Level.Value = DefaultLevel;
            Waveform.Value = DefaultWaveform;
            OscillationBeats.Value = DefaultOscillationBeats;
            WindowInterval.Value = DefaultWindowProcessInterval;
            WindowStartOffset.Value = DefaultWindowProcessOffset;
            ApplyOrderIndex.Value = DefaultApplyOrder;
        }

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            if (Level.Value <= 0)
                return;

            Seed.Value ??= RNG.Next();
            var oscillator = new EzOscillator(Seed.Value.Value, waveform: Waveform.Value);

            var maniaBeatmap = (ManiaBeatmap)beatmap;
            ManiaKeyPatternHelp.ProcessRollingWindowWithOscillator(maniaBeatmap,
                PatternType,
                Level.Value,
                oscillator,
                Seed.Value.Value,
                OscillationBeats.Value,
                WindowProcessInterval,
                WindowProcessOffset,
                MaxIterationsPerWindow,
                ApplyPatternForWindow);
            ManiaNoteCleanupTool.CleanupBeatmap(maniaBeatmap, seed: Seed.Value.Value);
        }
    }
}

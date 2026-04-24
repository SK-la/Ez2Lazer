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
using osu.Game.EzOsuGame.Localization;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.EzMania.Mods.LAsMods
{
    public abstract class ManiaModPatternShiftPatternBase : Mod, IApplicableAfterBeatmapConversion, IHasSeed, IEzApplyOrder
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

        protected virtual EzOscillator.EzWaveform DefaultWaveform => EzOscillator.EzWaveform.Sine;
        protected virtual LocalisableString LevelSettingLabel => PatternShiftStrings.PATTERN_SHIFT_LEVEL_LABEL;

        protected virtual int DefaultLevel => 0;
        protected virtual int DefaultOscillationBeats => 1;
        protected virtual int DefaultWindowProcessInterval => 1;
        protected virtual int DefaultWindowProcessOffset => 1;
        protected virtual int DefaultApplyOrder => 50;

        public override string Name => $"Pattern Shift {PatternName}";

        public override string Acronym => PatternAcronym;

        public override double ScoreMultiplier => 1;

        public override LocalisableString Description => PatternShiftStrings.PATTERN_SHIFT_DESCRIPTION;

        public override IconUsage? Icon => FontAwesome.Solid.Magic;

        public override ModType Type => ModType.LA_Mod;

        public override bool Ranked => false;
        public override bool ValidForMultiplayer => false;
        public override bool ValidForFreestyleAsRequiredMod => false;

        [SettingSource(typeof(PatternShiftStrings), nameof(PatternShiftStrings.PATTERN_SHIFT_WAVEFORM_LABEL), nameof(PatternShiftStrings.PATTERN_SHIFT_WAVEFORM_DESCRIPTION))]
        public Bindable<EzOscillator.EzWaveform> Waveform { get; } = new Bindable<EzOscillator.EzWaveform>(EzOscillator.EzWaveform.Sine);

        public BindableNumber<int> Level { get; } = new BindableInt(0)
        {
            MinValue = 0,
            MaxValue = 10,
            Precision = 1
        };

        [SettingSource(typeof(PatternShiftStrings), nameof(PatternShiftStrings.PATTERN_SHIFT_OSCILLATION_BEATS_LABEL), nameof(PatternShiftStrings.PATTERN_SHIFT_OSCILLATION_BEATS_DESCRIPTION))]
        public BindableNumber<int> OscillationBeats { get; } = new BindableInt(2)
        {
            MinValue = 1,
            MaxValue = 8,
            Precision = 1
        };

        [SettingSource(typeof(PatternShiftStrings), nameof(PatternShiftStrings.PATTERN_SHIFT_WINDOW_INTERVAL_LABEL), nameof(PatternShiftStrings.PATTERN_SHIFT_WINDOW_INTERVAL_DESCRIPTION))]
        public BindableNumber<int> WindowInterval { get; } = new BindableInt(2)
        {
            MinValue = 1,
            MaxValue = 4,
            Precision = 1
        };

        [SettingSource(typeof(PatternShiftStrings), nameof(PatternShiftStrings.PATTERN_SHIFT_WINDOW_START_OFFSET_LABEL), nameof(PatternShiftStrings.PATTERN_SHIFT_WINDOW_START_OFFSET_DESCRIPTION))]
        public BindableNumber<int> WindowStartOffset { get; } = new BindableInt(1)
        {
            MinValue = 1,
            MaxValue = 4,
            Precision = 1
        };

        [SettingSource(typeof(PatternShiftStrings), nameof(PatternShiftStrings.PATTERN_SHIFT_SKIP_FINE_THRESHOLD_LABEL), nameof(PatternShiftStrings.PATTERN_SHIFT_SKIP_FINE_THRESHOLD_DESCRIPTION))]
        public BindableNumber<int> SkipFineThreshold { get; } = new BindableInt(2)
        {
            MinValue = 1,
            MaxValue = 10,
            Precision = 1
        };

        [SettingSource(typeof(PatternShiftStrings), nameof(PatternShiftStrings.PATTERN_SHIFT_SKIP_QUARTER_DIVISOR_LABEL), nameof(PatternShiftStrings.PATTERN_SHIFT_SKIP_QUARTER_DIVISOR_DESCRIPTION))]
        public BindableNumber<int> SkipQuarterDivisor { get; } = new BindableInt(2)
        {
            MinValue = 1,
            MaxValue = 10,
            Precision = 1
        };

        [SettingSource(typeof(EzCommonModStrings), nameof(EzCommonModStrings.SEED_LABEL), nameof(EzCommonModStrings.SEED_DESCRIPTION), SettingControlType = typeof(SettingsNumberBox))]
        public Bindable<int?> Seed { get; } = new Bindable<int?>();

        [SettingSource(typeof(EzCommonModStrings), nameof(EzCommonModStrings.APPLY_ORDER_LABEL), nameof(EzCommonModStrings.APPLY_ORDER_DESCRIPTION))]
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
                yield return (LevelSettingLabel, $"{Level.Value}");
                yield return (PatternShiftStrings.PATTERN_SHIFT_WAVEFORM_LABEL, Waveform.Value.ToString());
                yield return (PatternShiftStrings.PATTERN_SHIFT_OSCILLATION_BEATS_LABEL, $"{OscillationBeats.Value}");
                yield return (PatternShiftStrings.PATTERN_SHIFT_WINDOW_INTERVAL_LABEL, $"{WindowInterval.Value}");
                yield return (PatternShiftStrings.PATTERN_SHIFT_WINDOW_START_OFFSET_LABEL, $"{WindowStartOffset.Value}");
                yield return (EzCommonModStrings.SEED_LABEL, Seed.Value?.ToString() ?? "Random");
                yield return (PatternShiftStrings.PATTERN_SHIFT_SKIP_FINE_THRESHOLD_LABEL, $"{SkipFineThreshold.Value}");
                yield return (PatternShiftStrings.PATTERN_SHIFT_SKIP_QUARTER_DIVISOR_LABEL, $"{SkipQuarterDivisor.Value}");
                yield return (EzCommonModStrings.APPLY_ORDER_LABEL, $"{ApplyOrderIndex.Value}");
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

            var oscillator = new EzOscillator(Seed.Value.Value, ezWaveform: Waveform.Value);

            var maniaBeatmap = (ManiaBeatmap)beatmap;

            var psSettings = new KeyPatternSettings
            {
                Level = Level.Value,
                OscillationBeats = OscillationBeats.Value,
                WindowProcessInterval = WindowProcessInterval,
                WindowProcessOffset = WindowProcessOffset,
                MaxIterationsPerWindow = MaxIterationsPerWindow,
                Seed = Seed.Value.Value,
                FineCountThreshold = SkipFineThreshold.Value,
                QuarterLineDivisor = SkipQuarterDivisor.Value
            };

            ManiaKeyPatternHelp.ProcessRollingWindowWithOscillator(maniaBeatmap,
                PatternType,
                psSettings,
                oscillator,
                ApplyPatternForWindow);
            ManiaNoteCleanupTool.CleanupBeatmap(maniaBeatmap, seed: Seed.Value.Value);
        }
    }
}

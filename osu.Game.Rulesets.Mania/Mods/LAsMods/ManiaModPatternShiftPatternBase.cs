// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Framework.Utils;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Mods.LAsMods
{
    public abstract class ManiaModPatternShiftPatternBase : Mod, IApplicableAfterBeatmapConversion, IHasSeed, IHasApplyOrder
    {
        protected abstract KeyPatternType PatternType { get; }
        protected abstract string PatternName { get; }
        protected abstract string PatternAcronym { get; }

        public override string Name => $"Pattern Shift {PatternName}";

        public override string Acronym => PatternAcronym;

        public override double ScoreMultiplier => 1;

        public override LocalisableString Description => $"Pattern shift ({PatternName})";

        public override IconUsage? Icon => FontAwesome.Solid.Magic;

        public override ModType Type => ModType.LA_Mod;

        public override bool Ranked => false;
        public override bool ValidForMultiplayer => true;
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
        public BindableNumber<int> OscillationBeats { get; } = new BindableInt(1)
        {
            MinValue = 1,
            MaxValue = 16,
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
                yield return ("Seed", Seed.Value?.ToString() ?? "Random");
            }
        }

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            if (Level.Value <= 0)
                return;

            Seed.Value ??= RNG.Next();
            var oscillator = new EzOscillator(Seed.Value.Value, waveform: Waveform.Value);

            var maniaBeatmap = (ManiaBeatmap)beatmap;
            ManiaKeyPatternHelp.ProcessRollingWindowWithOscillator(maniaBeatmap, PatternType, Level.Value, oscillator, Seed.Value.Value, OscillationBeats.Value);
            ManiaNoteCleanupTool.CleanupBeatmap(maniaBeatmap, Level.Value, 2, seed: Seed.Value.Value);
        }
    }
}

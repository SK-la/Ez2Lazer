// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Overlays.Settings;

namespace osu.Game.Rulesets.Mania.Mods.LAsMods
{
    public class ManiaModPatternShiftJack : ManiaModPatternShiftPatternBase
    {
        protected override KeyPatternType PatternType => KeyPatternType.Jack;
        protected override string PatternName => "Jack";
        protected override string PatternAcronym => "PSJ";

        protected override int DefaultLevel => 5;
        protected override EzOscillator.Waveform DefaultWaveform => EzOscillator.Waveform.Sine;
        protected override int DefaultOscillationBeats => 1;
        protected override int DefaultWindowProcessInterval => 2;
        protected override int DefaultWindowProcessOffset => 0;
        protected override int DefaultApplyOrder => 50;

        [SettingSource("Level", "0=off. 1-2: 1/2 move (one-side/both). 3-4: 1/4 move (one-side/both). 5-6: 1/2 add (one-side/both). 7-8: 1/4 add (one-side/both). 9: 5+7. 10: 6+8.")]
        public new BindableNumber<int> Level => base.Level;

        [SettingSource("Window Max Iterations", "Max iterations per window (1-4).")]
        public BindableNumber<int> WindowMaxIterations { get; } = new BindableInt(2)
        {
            MinValue = 1,
            MaxValue = 4,
            Precision = 1
        };

        protected override int MaxIterationsPerWindow => Math.Clamp(WindowMaxIterations.Value, 1, 4);

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                foreach (var item in base.SettingDescription)
                    yield return item;

                yield return ("Window Max Iterations", $"{WindowMaxIterations.Value}");
            }
        }
    }
}

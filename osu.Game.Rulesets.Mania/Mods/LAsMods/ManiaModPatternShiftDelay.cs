// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Game.Configuration;

namespace osu.Game.Rulesets.Mania.Mods.LAsMods
{
    public class ManiaModPatternShiftDelay : ManiaModPatternShiftPatternBase
    {
        protected override KeyPatternType PatternType => KeyPatternType.Delay;
        protected override string PatternName => "Delay";
        protected override string PatternAcronym => "PSD";

        protected override int DefaultLevel => 1;
        protected override EzOscillator.Waveform DefaultWaveform => EzOscillator.Waveform.Sine;
        protected override int DefaultOscillationBeats => 1;
        protected override int DefaultWindowProcessInterval => 1;
        protected override int DefaultWindowProcessOffset => 1;
        protected override int DefaultApplyOrder => 50;

        [SettingSource("Level", "0=off. 1-3: offsets 1/16, 3/32, 1/8; shift up to level and keep at least level unshifted. 4-6: same offsets; keep at least 1 unshifted. 7-10: offsets 1/16, 1/12, 5/48, 1/8; shift up to level with no minimum unshifted.")]
        public new BindableNumber<int> Level => base.Level;
    }
}

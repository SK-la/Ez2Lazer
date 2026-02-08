// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Mania.Mods.LAsMods
{
    public class ManiaModPatternShiftCut : ManiaModPatternShiftPatternBase
    {
        protected override KeyPatternType PatternType => KeyPatternType.Cut;
        protected override string PatternName => "Cut";
        protected override string PatternAcronym => "PSC";

        protected override int DefaultLevel => 4;
        protected override EzOscillator.Waveform DefaultWaveform => EzOscillator.Waveform.Sine;
        protected override int DefaultOscillationBeats => 1;
        protected override int DefaultWindowProcessInterval => 2;
        protected override int DefaultWindowProcessOffset => 1;
        protected override int DefaultApplyOrder => 50;
    }
}

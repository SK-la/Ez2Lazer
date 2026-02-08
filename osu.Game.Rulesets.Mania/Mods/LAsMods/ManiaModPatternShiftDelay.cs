// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Mania.Mods.LAsMods
{
    public class ManiaModPatternShiftDelay : ManiaModPatternShiftPatternBase
    {
        protected override KeyPatternType PatternType => KeyPatternType.Delay;
        protected override string PatternName => "Delay";
        protected override string PatternAcronym => "PSD";
    }
}

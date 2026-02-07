// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Mania.Mods.LAsMods
{
    public class ManiaModPatternShiftBeat : ManiaModPatternShiftPatternBase
    {
        protected override KeyPatternType PatternType => KeyPatternType.Beat;
        protected override string PatternName => "Beat";
        protected override string PatternAcronym => "PSB";
    }
}

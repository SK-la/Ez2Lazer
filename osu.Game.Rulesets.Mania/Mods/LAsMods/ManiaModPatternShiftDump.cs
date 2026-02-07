// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Mania.Mods.LAsMods
{
    public class ManiaModPatternShiftDump : ManiaModPatternShiftPatternBase
    {
        protected override KeyPatternType PatternType => KeyPatternType.Dump;
        protected override string PatternName => "Dump";
        protected override string PatternAcronym => "PSS";
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.EzOsuGame.Analysis;

namespace osu.Game.Rulesets.Mania.EzMania.Analysis.SR4Pattern
{
    internal static class EzManiaSR4PatternRadarBuilder
    {
        public static EzRadarChartData<string> Create(EzManiaSR4PatternSnapshot snapshot)
        {
            return EzRadarChartData<string>.Create(
                new EzRadarAxisValue<string>("Bracket", EzManiaSR4PatternBracket.Compute(snapshot), "0.00"),
                new EzRadarAxisValue<string>("Jack", EzManiaSR4PatternJack.Compute(snapshot), "0.00"),
                new EzRadarAxisValue<string>("Burst", EzManiaSR4PatternBurst.Compute(snapshot), "0.00"),
                new EzRadarAxisValue<string>("LN Hold", EzManiaSR4PatternLongNoteUsage.Compute(snapshot), "0.00"),
                new EzRadarAxisValue<string>("LN Release", EzManiaSR4PatternLongNoteRelease.Compute(snapshot), "0.00"),
                new EzRadarAxisValue<string>("Anchor", EzManiaSR4PatternAnchor.Compute(snapshot), "0.00"));
        }
    }
}

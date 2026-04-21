// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Mania.EzMania.Analysis.SR4Pattern
{
    internal static class EzManiaSR4PatternAnchor
    {
        public static double Compute(EzManiaSR4PatternSnapshot snapshot)
            => snapshot.AnchorStar;
    }
}

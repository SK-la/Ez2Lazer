// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Mania.EzMania.Analysis.SR4Pattern
{
    internal sealed class EzManiaSR4PatternSnapshot
    {
        public EzManiaSR4PatternSnapshot(int keyCount,
                                         double[] effectiveWeights,
                                         double[] densityValues,
                                         double[] activeColumnValues,
                                         double[] bracketCurve,
                                         double[] jackCurve,
                                         double[] burstCurve,
                                         double[] longNoteUsageCurve,
                                         double[] longNoteReleaseCurve,
                                         double[] anchorCurve,
                                         double bracketStar,
                                         double jackStar,
                                         double burstStar,
                                         double longNoteUsageStar,
                                         double longNoteReleaseStar,
                                         double anchorStar)
        {
            KeyCount = keyCount;
            EffectiveWeights = effectiveWeights;
            DensityValues = densityValues;
            ActiveColumnValues = activeColumnValues;
            BracketCurve = bracketCurve;
            JackCurve = jackCurve;
            BurstCurve = burstCurve;
            LongNoteUsageCurve = longNoteUsageCurve;
            LongNoteReleaseCurve = longNoteReleaseCurve;
            AnchorCurve = anchorCurve;
            BracketStar = bracketStar;
            JackStar = jackStar;
            BurstStar = burstStar;
            LongNoteUsageStar = longNoteUsageStar;
            LongNoteReleaseStar = longNoteReleaseStar;
            AnchorStar = anchorStar;
        }

        public int KeyCount { get; }

        public double[] EffectiveWeights { get; }

        public double[] DensityValues { get; }

        public double[] ActiveColumnValues { get; }

        public double[] BracketCurve { get; }

        public double[] JackCurve { get; }

        public double[] BurstCurve { get; }

        public double[] LongNoteUsageCurve { get; }

        public double[] LongNoteReleaseCurve { get; }

        public double[] AnchorCurve { get; }

        public double BracketStar { get; }

        public double JackStar { get; }

        public double BurstStar { get; }

        public double LongNoteUsageStar { get; }

        public double LongNoteReleaseStar { get; }

        public double AnchorStar { get; }

        public int Length => EffectiveWeights.Length;
    }
}

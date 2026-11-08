// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.EzMania.Analysis;
using osu.Game.Rulesets.Mania.EzMania.Analysis.SR4Pattern;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Mania.Tests.Analysis
{
    /// <summary>
    /// The combined rating pass must produce the same numbers as the two single-purpose entry points it replaces:
    /// the SR4 breakdown is read off the curves the plain rating already needs.
    /// </summary>
    [TestFixture]
    public class SRCalculatorEquivalenceTest
    {
        [TestCase(4)]
        [TestCase(7)]
        public void CombinedMatchesSeparateCalls(int keyCount)
        {
            ManiaBeatmap beatmap = createChart(keyCount);

            double expectedSr = EzSSREngine.CalculateSR(beatmap);
            EzManiaSR4PatternSnapshot expectedSnapshot = EzSSREngine.CalculateSR4PatternSnapshot(beatmap);

            var combined = EzSSREngine.CalculateSRWithPatternSnapshot(beatmap);

            assertBitIdentical(expectedSr, combined.Sr, "total SR");
            assertBitIdentical(expectedSnapshot.BracketStar, combined.PatternSnapshot.BracketStar, "bracket star");
            assertBitIdentical(expectedSnapshot.JackStar, combined.PatternSnapshot.JackStar, "jack star");
            assertBitIdentical(expectedSnapshot.BurstStar, combined.PatternSnapshot.BurstStar, "burst star");
            assertBitIdentical(expectedSnapshot.LongNoteUsageStar, combined.PatternSnapshot.LongNoteUsageStar, "LN usage star");
            assertBitIdentical(expectedSnapshot.LongNoteReleaseStar, combined.PatternSnapshot.LongNoteReleaseStar, "LN release star");
            assertBitIdentical(expectedSnapshot.AnchorStar, combined.PatternSnapshot.AnchorStar, "anchor star");

            assertBitIdentical(expectedSnapshot.EffectiveWeights, combined.PatternSnapshot.EffectiveWeights, "weights");
            assertBitIdentical(expectedSnapshot.DensityValues, combined.PatternSnapshot.DensityValues, "density");
            assertBitIdentical(expectedSnapshot.ActiveColumnValues, combined.PatternSnapshot.ActiveColumnValues, "active columns");
            assertBitIdentical(expectedSnapshot.BracketCurve, combined.PatternSnapshot.BracketCurve, "bracket curve");
            assertBitIdentical(expectedSnapshot.JackCurve, combined.PatternSnapshot.JackCurve, "jack curve");
            assertBitIdentical(expectedSnapshot.BurstCurve, combined.PatternSnapshot.BurstCurve, "burst curve");
            assertBitIdentical(expectedSnapshot.LongNoteUsageCurve, combined.PatternSnapshot.LongNoteUsageCurve, "LN usage curve");
            assertBitIdentical(expectedSnapshot.LongNoteReleaseCurve, combined.PatternSnapshot.LongNoteReleaseCurve, "LN release curve");
            assertBitIdentical(expectedSnapshot.AnchorCurve, combined.PatternSnapshot.AnchorCurve, "anchor curve");
        }

        [Test]
        public void CombinedMatchesOnHoldHeavyChart()
        {
            ManiaBeatmap beatmap = createChart(4, holdEveryRow: true);

            double expectedSr = EzSSREngine.CalculateSR(beatmap);
            var combined = EzSSREngine.CalculateSRWithPatternSnapshot(beatmap);

            assertBitIdentical(expectedSr, combined.Sr, "total SR");
        }

        private static void assertBitIdentical(double expected, double actual, string label)
            => Assert.That(BitConverter.DoubleToInt64Bits(actual), Is.EqualTo(BitConverter.DoubleToInt64Bits(expected)), label);

        private static void assertBitIdentical(IReadOnlyList<double> expected, IReadOnlyList<double> actual, string label)
        {
            Assert.That(actual.Count, Is.EqualTo(expected.Count), $"{label} length");

            for (int i = 0; i < expected.Count; i++)
                assertBitIdentical(expected[i], actual[i], $"{label}[{i}]");
        }

        private static ManiaBeatmap createChart(int keyCount, bool holdEveryRow = false)
        {
            var beatmap = new ManiaBeatmap(new StageDefinition(keyCount))
            {
                BeatmapInfo = new BeatmapInfo { Metadata = new BeatmapMetadata { Title = "equivalence" } },
            };

            int seed = 12345;

            for (int row = 0; row < 240; row++)
            {
                seed = seed * 1103515245 + 12345;
                int size = 1 + Math.Abs(seed >> 16) % Math.Min(3, keyCount);
                double time = 500 + row * 125.0;

                for (int note = 0; note < size; note++)
                {
                    seed = seed * 1103515245 + 12345;
                    int column = Math.Abs(seed >> 16) % keyCount;

                    if (holdEveryRow || (seed & 3) == 0)
                    {
                        beatmap.HitObjects.Add(new HoldNote
                        {
                            StartTime = time,
                            Column = column,
                            Duration = 150 + Math.Abs(seed >> 8) % 400,
                        });
                    }
                    else
                    {
                        beatmap.HitObjects.Add(new Note { StartTime = time, Column = column });
                    }
                }
            }

            return beatmap;
        }
    }
}

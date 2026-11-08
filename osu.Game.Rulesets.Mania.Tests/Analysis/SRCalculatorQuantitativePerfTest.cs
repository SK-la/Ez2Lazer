// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.EzMania.Analysis;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Tests.EzOsuGame.Skills;

namespace osu.Game.Rulesets.Mania.Tests.Analysis
{
    /// <summary>
    /// Quantitative guard on the mania rating core, which is the heaviest single piece of an L3 pass on mania.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The combined entry point derives the rating and the SR4 breakdown together; the ceilings below are what that
    /// one derivation costs, so asking for a radar as well does not double the time.
    /// </para>
    /// <para>
    /// Measured as-is: <c>SRCalculator</c> currently spreads its per-corner loop over <c>Parallel.For</c>. Those
    /// numbers are the baseline, not evidence that the split pays. Charts are rated one after another by a single
    /// worker, so a per-chart fan-out competes for the same cores the backfill could otherwise use; a sequential loop
    /// is the comparison to make before deciding either way, and no new parallelism belongs in these algorithms.
    /// </para>
    /// <para>
    /// On hold-heavy charts the core still promotes to gen2: the corner arrays it builds for every head and tail
    /// cross the large-object threshold. That is measured and printed rather than asserted away, and is the concrete
    /// target for a span-based rewrite of the corner/curve buffers (a pooling pass would make the count zero).
    /// </para>
    /// </remarks>
    [TestFixture]
    public class SRCalculatorQuantitativePerfTest
    {
        private const int runs = 8;

        private const double sr_alloc_kb = 96 * 1024;
        private const double sr_median_ms = 600;

        [TestCase(4, false)]
        [TestCase(4, true)]
        [TestCase(7, false)]
        [TestCase(7, true)]
        public void CombinedRatingIsQuantified(int keyCount, bool holdHeavy)
        {
            ManiaBeatmap beatmap = createChart(keyCount, holdHeavy);

            // Resolve the cross matrix and JIT the hot paths before the first timed run.
            SRCalculator.CalculateSRWithPatternSnapshot(beatmap);

            var sample = EzPerfMeasurer.Measure("sr + radar", runs, () => SRCalculator.CalculateSRWithPatternSnapshot(beatmap));

            TestContext.Progress.WriteLine($"{keyCount}K holdHeavy={holdHeavy} notes={beatmap.HitObjects.Count}");
            EzPerfMeasurer.Report(new[] { sample });

            Assert.That(sample.AverageKb, Is.LessThanOrEqualTo(sr_alloc_kb),
                $"combined rating allocated {sample.AverageKb:F1} KB/run");
            Assert.That(sample.MedianMs, Is.LessThanOrEqualTo(sr_median_ms),
                $"combined rating median {sample.MedianMs:F2} ms");

            // A chart with no long notes produces fewer corners, so it stays under the large-object threshold and is
            // a hard requirement rather than a target.
            if (!holdHeavy)
                Assert.That(sample.Gen2, Is.Zero, "the rating core promoted objects to gen2 on a chart without long notes");
        }

        /// <summary>
        /// A chart where every row is a long note used to make the "next same-column note" lookup quadratic. Keeping
        /// this case timed makes a regression to a linear search visible as a step change in the printed number.
        /// </summary>
        [Test]
        public void HoldDenseChartStaysLinear()
        {
            ManiaBeatmap beatmap = createChart(4, holdHeavy: true, rows: 1200);

            SRCalculator.CalculateSRWithPatternSnapshot(beatmap);

            var sample = EzPerfMeasurer.Measure("sr hold-dense", runs, () => SRCalculator.CalculateSRWithPatternSnapshot(beatmap));

            EzPerfMeasurer.Report(new[] { sample });

            TestContext.Progress.WriteLine($"hold-dense corners promote {sample.Gen2} time(s) to gen2; target for a pooled rewrite is 0");

            Assert.That(sample.MedianMs, Is.LessThanOrEqualTo(sr_median_ms));
            Assert.That(sample.AverageKb, Is.LessThanOrEqualTo(sr_alloc_kb));
        }

        private static ManiaBeatmap createChart(int keyCount, bool holdHeavy, int rows = 600)
        {
            var beatmap = new ManiaBeatmap(new StageDefinition(keyCount))
            {
                BeatmapInfo = new BeatmapInfo { Metadata = new BeatmapMetadata { Title = "sr-perf" } },
                Difficulty = { OverallDifficulty = 8 },
            };

            int seed = 4321;

            for (int row = 0; row < rows; row++)
            {
                seed = seed * 1103515245 + 12345;
                int size = 1 + Math.Abs(seed >> 16) % Math.Min(3, keyCount);
                double time = 500 + row * 125.0;

                for (int note = 0; note < size; note++)
                {
                    seed = seed * 1103515245 + 12345;
                    int column = Math.Abs(seed >> 16) % keyCount;

                    if (holdHeavy)
                    {
                        beatmap.HitObjects.Add(new HoldNote
                        {
                            StartTime = time,
                            Column = column,
                            Duration = 200 + Math.Abs(seed >> 8) % 600,
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

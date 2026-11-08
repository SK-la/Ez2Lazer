// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using osu.Game.EzOsuGame.Skills.LeoBlack;
using osu.Game.EzOsuGame.Skills.Mina;
using osu.Game.EzOsuGame.Skills.SkillTag;

namespace osu.Game.Tests.EzOsuGame.Skills
{
    /// <summary>
    /// Reports per-chart cost of the analyzers an L3 pass runs, so a change can be compared against the previous
    /// build. Explicit because wall-clock numbers are not a CI assertion; correctness stays with the functional
    /// tests that pin the produced values.
    /// </summary>
    [TestFixture]
    [Explicit]
    public class EzAnalysisAnalyzerBenchmarkTest
    {
        private const int iterations = 20;

        [TestCase(4, 0.0)]
        [TestCase(4, 0.5)]
        [TestCase(7, 0.0)]
        [TestCase(7, 0.5)]
        public void MeasureChartAnalyzers(int keyCount, double holdRatio)
        {
            var chart = buildChart(keyCount, holdRatio);
            var notes = chart.Notes;

            // Warm the JIT and the shared wasm module so the first iteration is not reported as steady state.
            using (var engine = new EzMinaEngine())
                engine.CalculateMsd(EzMinaNoteConverter.Convert(notes), keyCount, 1f);

            EzChartSkillInfoComputer.Compute(chart);
            EzPatternAnalyzer.Analyze(chart);

            measure("CSI", iterations, () => EzChartSkillInfoComputer.Compute(chart));
            measure("patterns", iterations, () => EzPatternAnalyzer.Analyze(chart));
            measure("features", iterations, () => EzChartFeatureExtractor.Extract(chart));
            measure("leoblack", iterations, () => EzLeoBlackPatternAnalyzer.TryAnalyze(chart));
            measure("mina rows", iterations, () => EzMinaNoteConverter.Convert(notes));

            var rows = EzMinaNoteConverter.Convert(notes);

            measure("mina engine", iterations,
                () =>
                {
                    using var engine = new EzMinaEngine();
                    engine.CalculateMsd(rows, keyCount, 1f);
                });

            var info = EzChartSkillInfoComputer.Compute(chart);
            Assert.That(info.KeyCount, Is.EqualTo(keyCount));
            Assert.That(info.Patterns, Is.Not.Null);
        }

        private static void measure(string label, int runs, Action action)
        {
            // One untimed run keeps first-touch allocations out of the reported average.
            action();

            long allocated = 0;
            long elapsedTicks = 0;

            for (int i = 0; i < runs; i++)
            {
                long before = GC.GetAllocatedBytesForCurrentThread();
                long start = Stopwatch.GetTimestamp();

                action();

                elapsedTicks += Stopwatch.GetTimestamp() - start;
                allocated += GC.GetAllocatedBytesForCurrentThread() - before;
            }

            double averageMs = elapsedTicks * 1000.0 / Stopwatch.Frequency / runs;
            double averageKb = allocated / 1024.0 / runs;

            TestContext.Progress.WriteLine($"{label}: {averageMs:F2} ms, {averageKb:F1} KB per run");
        }

        private static EzManiaChartInput buildChart(int keyCount, double holdRatio)
        {
            var notes = new List<EzManiaNote>();
            var timingPoints = new[]
            {
                new EzManiaTimingPoint(0, 500),
                new EzManiaTimingPoint(60_000, 400),
                new EzManiaTimingPoint(120_000, 375),
            };

            const int rows = 3000;
            double time = 0;
            int seed = 17;

            for (int row = 0; row < rows; row++)
            {
                seed = seed * 1103515245 + 12345;
                int size = 1 + Math.Abs(seed >> 16) % Math.Min(3, keyCount);

                for (int note = 0; note < size; note++)
                {
                    seed = seed * 1103515245 + 12345;
                    int column = Math.Abs(seed >> 16) % keyCount;
                    bool isHold = (seed & 7) < holdRatio * 8;
                    double end = isHold ? time + 200 + (Math.Abs(seed >> 8) % 900) : time;

                    notes.Add(new EzManiaNote(time, column, isHold, end));
                }

                time += 100 + (row % 5) * 25;
            }

            return new EzManiaChartInput
            {
                KeyCount = keyCount,
                Bpm = 150,
                TotalLengthMs = time,
                Notes = notes,
                TimingPoints = timingPoints,
            };
        }
    }
}

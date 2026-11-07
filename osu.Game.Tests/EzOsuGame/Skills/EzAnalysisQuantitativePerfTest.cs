// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Metrics;
using osu.Game.EzOsuGame.Skills.Dan;
using osu.Game.EzOsuGame.Skills.LeoBlack;
using osu.Game.EzOsuGame.Skills.Mina;
using osu.Game.EzOsuGame.Skills.SkillTag;

namespace osu.Game.Tests.EzOsuGame.Skills
{
    /// <summary>
    /// Quantitative guard on the analyzers an L3 pass runs: wall clock, managed allocation per run, and GC counts.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The allocation ceilings exist to be lowered. They sit a little above today's numbers so ordinary churn does
    /// not fail the suite, but a change that doubles a stage's allocation does. A zero-allocation (span-based)
    /// rewrite is expected to move them down, not to leave them where they are.
    /// </para>
    /// <para>
    /// Wall-clock ceilings are deliberately loose: the same build on the same machine varies by 2x between runs for
    /// the heavier stages. They catch a collapse (a stage regressing by an order of magnitude), not a percentage.
    /// The measured distribution is printed either way.
    /// </para>
    /// </remarks>
    [TestFixture]
    public class EzAnalysisQuantitativePerfTest
    {
        private const int runs = 12;

        private const double features_alloc_kb = 5 * 1024;
        private const double patterns_alloc_kb = 9 * 1024;
        private const double csi_alloc_kb = 34 * 1024;
        private const double leoblack_alloc_kb = 26 * 1024;
        private const double mina_rows_alloc_kb = 128;
        private const double mina_engine_alloc_kb = 8;

        private const double features_median_ms = 60;
        private const double patterns_median_ms = 120;
        private const double csi_median_ms = 400;
        private const double leoblack_median_ms = 250;
        private const double mina_rows_median_ms = 30;
        private const double mina_engine_median_ms = 60;

        /// <summary>
        /// Gen1 collections per structure pass. Today's worst case (7K, hold-heavy) sits at 0.5; a pass that starts
        /// triggering one collection per run has either grown its allocation by an order of magnitude or started
        /// keeping objects alive across runs.
        /// </summary>
        private const double gen1_per_run_ceiling = 2;

        [TestCase(4, 0.0)]
        [TestCase(4, 0.5)]
        [TestCase(7, 0.0)]
        [TestCase(7, 0.5)]
        public void ReportAndGuardAnalyzerCost(int keyCount, double holdRatio)
        {
            var chart = buildChart(keyCount, holdRatio);
            var notes = chart.Notes;

            Assert.That(EzChartSkillInfoComputer.Compute(chart).KeyCount, Is.EqualTo(keyCount));

            // Pin the interpreter/wasm module before the first timed run.
            var rows = EzMinaNoteConverter.Convert(notes);
            using (var engine = new EzMinaEngine())
                engine.CalculateMsd(rows, keyCount, 1f);

            var features = EzPerfMeasurer.Measure("features", runs, () => EzChartFeatureExtractor.Extract(chart));
            var patterns = EzPerfMeasurer.Measure("patterns", runs, () => EzPatternAnalyzer.Analyze(chart));
            var csi = EzPerfMeasurer.Measure("csi", runs, () => EzChartSkillInfoComputer.Compute(chart));
            var leoblack = EzPerfMeasurer.Measure("leoblack", runs, () => EzLeoBlackPatternAnalyzer.TryAnalyze(chart));
            var minaRows = EzPerfMeasurer.Measure("mina rows", runs, () => EzMinaNoteConverter.Convert(notes));

            EzPerfSample minaEngine;

            using (var engine = new EzMinaEngine())
                minaEngine = EzPerfMeasurer.Measure("mina engine", runs, () => engine.CalculateMsd(rows, keyCount, 1f));

            TestContext.Progress.WriteLine($"{keyCount}K holdRatio={holdRatio:P0} rows={notes.Count}");
            EzPerfMeasurer.Report([features, patterns, csi, leoblack, minaRows, minaEngine]);

            assertAllocation(features, features_alloc_kb);
            assertAllocation(patterns, patterns_alloc_kb);
            assertAllocation(csi, csi_alloc_kb);
            assertAllocation(leoblack, leoblack_alloc_kb);
            assertAllocation(minaRows, mina_rows_alloc_kb);
            assertAllocation(minaEngine, mina_engine_alloc_kb);

            assertMedian(features, features_median_ms);
            assertMedian(patterns, patterns_median_ms);
            assertMedian(csi, csi_median_ms);
            assertMedian(leoblack, leoblack_median_ms);
            assertMedian(minaRows, mina_rows_median_ms);
            assertMedian(minaEngine, mina_engine_median_ms);
        }

        /// <summary>
        /// A whole structure pass must not promote anything to gen2, and must not trigger a collection per run on
        /// every pass either. While the analyzers still allocate eagerly these are the cheapest observable statements
        /// that nothing large survives a run; when the pass becomes allocation-free both should reach zero.
        /// </summary>
        [TestCase(4, 0.0)]
        [TestCase(4, 0.5)]
        [TestCase(7, 0.0)]
        [TestCase(7, 0.5)]
        public void StructurePassDoesNotPromoteToGen2(int keyCount, double holdRatio)
        {
            var chart = buildChart(keyCount, holdRatio);

            var sample = EzPerfMeasurer.Measure("structure pass", runs, () =>
            {
                var features = EzChartFeatureExtractor.Extract(chart);
                EzPatternAnalyzer.Analyze(chart, 1, features);
                EzLeoBlackPatternAnalyzer.TryAnalyze(chart);
            });

            EzPerfMeasurer.Report(new[] { sample });

            Assert.That(sample.Gen2, Is.Zero, "the structure pass promoted objects to gen2 (large-object or long-lived survival)");
            Assert.That(sample.Gen1PerRun, Is.LessThanOrEqualTo(gen1_per_run_ceiling),
                $"the structure pass collected gen1 {sample.Gen1PerRun:F2} times per run; the ceiling is {gen1_per_run_ceiling}");
        }

        /// <summary>
        /// Retained size of a full live cache. This is the L3 session store's steady-state footprint; it is all
        /// session memory, so it has to stay in the low megabyte range rather than track what the user scrolled past.
        /// </summary>
        [Test]
        public void LiveCacheRetainedMemoryIsBounded()
        {
            var sample = EzPerfMeasurer.MeasureRetainedKb(() => fillCache(512));

            TestContext.Progress.WriteLine($"live cache (512 entries): {sample:F1} KB retained, {sample / 512:F2} KB/entry");

            Assert.That(sample, Is.LessThan(16 * 1024), "live cache retained more than its entry budget allows");
        }

        private static void assertAllocation(in EzPerfSample sample, double ceilingKb)
        {
            Assert.That(sample.AverageKb, Is.LessThanOrEqualTo(ceilingKb),
                $"{sample.Label} allocated {sample.AverageKb:F1} KB/run, ceiling is {ceilingKb:F1} KB");
        }

        private static void assertMedian(in EzPerfSample sample, double ceilingMs)
        {
            Assert.That(sample.MedianMs, Is.LessThanOrEqualTo(ceilingMs),
                $"{sample.Label} median {sample.MedianMs:F2} ms, ceiling is {ceilingMs:F2} ms");
        }

        private static EzAnalysisLiveCache fillCache(int entries)
        {
            var cache = new EzAnalysisLiveCache();

            for (int i = 0; i < entries; i++)
                cache.Set(new EzAnalysisLiveCache.AnalysisKey($"retained-{i}", 3, 0), buildResult(i));

            return cache;
        }

        /// <summary>
        /// Shaped like a real mania L3 result: a 256-point KPS series, the column summary, the axis table, a dan set,
        /// the radar payload and the typed structure summary.
        /// </summary>
        private static EzAnalysisResult buildResult(int seed)
        {
            var kps = new double[OptimizedBeatmapCalculator.DEFAULT_KPS_GRAPH_POINTS];

            for (int i = 0; i < kps.Length; i++)
                kps[i] = (seed + i) % 17;

            var columns = new Dictionary<int, int>();
            var holds = new Dictionary<int, int>();

            for (int column = 0; column < 8; column++)
            {
                columns[column] = 100 + column;
                holds[column] = 10 + column;
            }

            var axes = new Dictionary<EzMetricProducerId, IReadOnlyDictionary<string, double>>();

            foreach (var producer in new[]
                     {
                         EzChartMetricProducer.Mina,
                         EzChartMetricProducer.Ppp,
                         EzChartMetricProducer.Csi,
                         EzChartMetricProducer.Ssr,
                         EzChartMetricProducer.Official,
                         EzChartMetricProducer.PppPerformance,
                         EzChartMetricProducer.SsrPerformance,
                         EzChartMetricProducer.OfficialPerformance,
                     })
            {
                var values = new Dictionary<string, double>(StringComparer.Ordinal);

                for (int axis = 0; axis < 10; axis++)
                    values[$"axis-{axis}"] = axis + (seed % 7);

                axes[producer.Id()] = values;
            }

            var sides = new Dictionary<EzDanSide, EzDanEntry>
            {
                [EzDanSide.Rc] = new EzDanEntry
                {
                    RawDan = 3.5,
                    Label = "11",
                    Ladder = EzDanLadderKind.Reform4K,
                    Value = EzDanValueSource.SunnyTable,
                    SkillsetLabels = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["Speed"] = "11",
                        ["Stamina"] = "11",
                    },
                },
                [EzDanSide.Ln] = new EzDanEntry
                {
                    RawDan = 2.5,
                    Label = "9",
                    Ladder = EzDanLadderKind.Ln4K,
                    Value = EzDanValueSource.SunnyTable,
                    SkillsetLabels = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["LN"] = "9",
                    },
                },
            };

            var dan = new EzDanSet
            {
                KeyCount = 4,
                HoldRatio = 0.2,
                XxySr = 12.5,
                OverallMsd = 10.5,
                DominantAxis = EzMinaSkillAxis.Stream,
                Sides = sides,
            };

            var radar = new EzRulesetSpecificRadarResult(
                EzRadarChartData<string>.Create(
                    new EzRadarAxisValue<string>("Bracket", 1, "0.00"),
                    new EzRadarAxisValue<string>("Chord", 2, "0.00"),
                    new EzRadarAxisValue<string>("LN%", 3, "0.00"),
                    new EzRadarAxisValue<string>("Jack", 4, "0.00"),
                    new EzRadarAxisValue<string>("Delay", 5, "0.00"),
                    new EzRadarAxisValue<string>("Dump", 6, "0.00")),
                EzRadarChartData<string>.Create(
                    new EzRadarAxisValue<string>("Bracket", 1, "0.00"),
                    new EzRadarAxisValue<string>("Jack", 2, "0.00"),
                    new EzRadarAxisValue<string>("Burst", 3, "0.00"),
                    new EzRadarAxisValue<string>("LN Hold", 4, "0.00"),
                    new EzRadarAxisValue<string>("LN Release", 5, "0.00"),
                    new EzRadarAxisValue<string>("Anchor", 6, "0.00")),
                12.5);

            var info = new EzChartSkillInfo
            {
                Patterns = ["jack", "chordjack", "tech", "stream"],
                JackShare = 0.4,
                StreamShare = 0.1,
                TechScore = 0.5,
                ChordjackScore = 0.7,
                JackScore = 0.6,
                LnRatio = 0.2,
                LengthSeconds = 120,
                KeyCount = 4,
            };

            return new EzAnalysisResult(
                new KpsSummary(6.5, 9.5, kps),
                new EzManiaSummary(columns, holds, 12.5),
                axes: axes,
                dan: dan,
                radar: radar,
                chartSkillInfo: info);
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

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Metrics;
using osu.Game.EzOsuGame.Skills.Dan;
using osu.Game.EzOsuGame.Skills.LeoBlack;
using osu.Game.EzOsuGame.Skills.Mina;
using osu.Game.EzOsuGame.Skills.SkillTag;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.EzMania.Analysis;
using osu.Game.Rulesets.Mania.Objects;

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
        private const double leoblack_alloc_kb = 12 * 1024;
        private const double mina_rows_alloc_kb = 128;
        private const double mina_engine_alloc_kb = 8;

        private const double features_median_ms = 60;
        private const double patterns_median_ms = 120;
        private const double csi_median_ms = 400;
        private const double leoblack_median_ms = 250;
        private const double mina_rows_median_ms = 30;
        private const double mina_engine_median_ms = 60;

        private const double dan_alloc_kb = 96;
        private const double dan_row_alloc_kb = 96;
        private const double dan_labels_alloc_kb = 16;
        private const double dan_median_ms = 20;
        private const double dan_row_median_ms = 20;

        private const double chain_alloc_kb = 20 * 1024;
        private const double chain_median_ms = 300;
        private const double radar_median_ms = 200;

        /// <summary>
        /// Gen1 collections per structure pass. Today's worst case (7K, hold-heavy) sits at 0.5; a pass that starts
        /// triggering one collection per run has either grown its allocation by an order of magnitude or started
        /// keeping objects alive across runs.
        /// </summary>
        private const double gen1_per_run_ceiling = 2;

        [TearDown]
        public void TearDown() => EzMinaEnginePool.Release();

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

            // LeoBlack is the largest single allocator in the structure pass; splitting it shows whether the cost is
            // the chart/row arrays or the pattern matchers, which decides where a pooled rewrite has to start.
            var leoChart = EzLeoBlackChartBuilder.TryBuild(chart)!;
            var leoBuild = EzPerfMeasurer.Measure("leoblack build", runs, () => EzLeoBlackChartBuilder.TryBuild(chart));
            var leoPrimitives = EzPerfMeasurer.Measure("leoblack primitives", runs, () => EzLeoBlackPrimitives.CalculatePrimitives(leoChart));

            EzPerfSample minaEngine;

            using (var engine = new EzMinaEngine())
                minaEngine = EzPerfMeasurer.Measure("mina engine", runs, () => engine.CalculateMsd(rows, keyCount, 1f));

            TestContext.Progress.WriteLine($"{keyCount}K holdRatio={holdRatio:P0} rows={notes.Count}");
            EzPerfMeasurer.Report([features, patterns, csi, leoblack, leoBuild, leoPrimitives, minaRows, minaEngine]);

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
        public void LiveCacheRetainedMemoryIsBounded()        {
            var sample = EzPerfMeasurer.MeasureRetainedKb(() => fillCache(512));

            TestContext.Progress.WriteLine($"live cache (512 entries): {sample:F1} KB retained, {sample / 512:F2} KB/entry");

            Assert.That(sample, Is.LessThan(16 * 1024), "live cache retained more than its entry budget allows");
        }

        /// <summary>
        /// The dan step of the chart chain: a Sunny/means table lookup plus per-side skillset filing. It is cheap in
        /// milliseconds but allocates for both sides on every claim, which is why allocation is the guarded number
        /// here rather than time.
        /// </summary>
        [TestCase(4, 0.0)]
        [TestCase(4, 0.5)]
        [TestCase(7, 0.0)]
        [TestCase(7, 0.5)]
        public void ReportAndGuardDanCost(int keyCount, double holdRatio)
        {
            var chart = buildChart(keyCount, holdRatio);
            var info = EzChartSkillInfoComputer.Compute(chart);
            var msd = computeMsd(chart, keyCount);
            int holdCount = countHolds(chart);

            Assert.That(EzDanSet.Create(msd, keyCount, holdRatio, 12.5, info, holdCount), Is.Not.Null,
                "the dan tables could not rate this fixture; the measurement would be of the empty path");

            var danSet = EzPerfMeasurer.Measure("dan set", runs, () => EzDanSet.Create(msd, keyCount, holdRatio, 12.5, info, holdCount));
            var danRow = EzPerfMeasurer.Measure("dan persisted row", runs,
                () => EzPersistedChartDan.TryComputeFromStored("dan-hash", Guid.Empty, msd, keyCount, holdRatio, 12.5, info, holdCount));
            var danLabels = EzPerfMeasurer.Measure("dan skillset filing", runs,
                () => EzDanSkillsetFiling.BucketsForValues(keyCount, EzDanSide.Rc, msd, info.LengthSeconds, 1, info));

            TestContext.Progress.WriteLine($"{keyCount}K holdRatio={holdRatio:P0} holds={holdCount}");
            EzPerfMeasurer.Report([danSet, danRow, danLabels]);

            assertAllocation(danSet, dan_alloc_kb);
            assertAllocation(danRow, dan_row_alloc_kb);
            assertAllocation(danLabels, dan_labels_alloc_kb);
            assertMedian(danSet, dan_median_ms);
            assertMedian(danRow, dan_row_median_ms);
        }

        /// <summary>
        /// One chart's whole chart chain, run on one thread: Mina rows, the engine, structure info, the dan row. This is
        /// the unit the background backfill processes.
        /// </summary>
        /// <remarks>
        /// Deliberately sequential. Per-chart work here is single-digit milliseconds, so splitting a chart across
        /// threads (or handing charts to a thread pool) spends more on scheduling and cache traffic than it saves;
        /// the fleet assertion below is what makes a regression to parallel-per-chart visible as gen2 pressure.
        /// </remarks>
        [TestCase(4, 0.0)]
        [TestCase(4, 0.5)]
        [TestCase(7, 0.0)]
        [TestCase(7, 0.5)]
        public void ReportAndGuardSequentialChartChain(int keyCount, double holdRatio)
        {
            var chart = buildChart(keyCount, holdRatio);
            int holdCount = countHolds(chart);

            // Resolve the module, the tables and the hot paths for this keymode before the first timed run.
            runChain(chart, keyCount, holdRatio, holdCount);

            var single = EzPerfMeasurer.Measure("chain 1 chart", runs, () => runChain(chart, keyCount, holdRatio, holdCount));
            var fleet = EzPerfMeasurer.Measure("chain 4 charts", runs, () =>
            {
                for (int i = 0; i < 4; i++)
                    runChain(chart, keyCount, holdRatio, holdCount);
            });

            // Reported, not compared: on mania the ruleset radar (xxy + its SR4 breakdown) is the largest single item
            // of a full pass, well above the structure, Mina and dan steps this chain covers. Surfacing it here keeps
            // that ordering visible instead of letting the smaller stages look like the budget.
            var beatmap = buildManiaBeatmap(chart, keyCount);
            var provider = new EzManiaAnalysisProvider();
            var radarRequest = new EzAnalysisRequest(beatmap, 1.0, EzAnalysisScope.RulesetSpecificRadarData);

            var radar = EzPerfMeasurer.Measure("radar (xxy + SR4)", runs, () =>
            {
                provider.TryCompute(radarRequest, CancellationToken.None, out _);
            });

            TestContext.Progress.WriteLine($"{keyCount}K holdRatio={holdRatio:P0} rows={chart.Notes.Count}");
            EzPerfMeasurer.Report([single, fleet, radar]);

            assertMedian(single, chain_median_ms);
            assertAllocation(single, chain_alloc_kb);
            assertMedian(radar, radar_median_ms);

            Assert.That(fleet.AverageKb, Is.LessThanOrEqualTo(single.AverageKb * 4 * 1.35),
                "chain allocation grew faster than linearly with chart count");

            // Hold-heavy charts promote today: the structure pass allocates enough per chart to turn over the
            // nursery. That is the concrete target for the zero-allocation work, so it is reported rather than
            // asserted away; RC charts must already stay clean.
            if (holdRatio == 0)
                Assert.That(fleet.Gen2, Is.Zero, "a four-chart sequential RC chain promoted objects to gen2");
            else
                TestContext.Progress.WriteLine($"hold-heavy chain promotes {fleet.Gen2} time(s) to gen2 over 4 charts; target is 0");
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

        /// <summary>Rebuilds a playable mania chart from the synthetic fixture, so the radar can run on a real beatmap.</summary>
        private static ManiaBeatmap buildManiaBeatmap(EzManiaChartInput chart, int keyCount)
        {
            var beatmap = new ManiaBeatmap(new StageDefinition(keyCount))
            {
                BeatmapInfo = new BeatmapInfo
                {
                    Metadata = new BeatmapMetadata { Title = "perf", Artist = "perf" },
                    Ruleset = new ManiaRuleset().RulesetInfo,
                    Difficulty = new BeatmapDifficulty { CircleSize = keyCount, OverallDifficulty = 8 },
                },
                Difficulty = { OverallDifficulty = 8 },
            };

            foreach (var note in chart.Notes)
            {
                if (note.IsHold)
                {
                    beatmap.HitObjects.Add(new HoldNote
                    {
                        StartTime = note.TimeMs,
                        Column = note.Column,
                        Duration = Math.Max(1, note.EndTimeMs - note.TimeMs),
                    });
                }
                else
                {
                    beatmap.HitObjects.Add(new Note { StartTime = note.TimeMs, Column = note.Column });
                }
            }

            beatmap.ControlPointInfo.Add(0, new osu.Game.Beatmaps.ControlPoints.TimingControlPoint { BeatLength = 500 });
            beatmap.ControlPointInfo.Add(30_000, new osu.Game.Beatmaps.ControlPoints.TimingControlPoint { BeatLength = 400 });
            return beatmap;
        }

        /// <summary>The Mina vector in the msd skill-id space the dan tables read.</summary>
        private static IReadOnlyDictionary<string, double> computeMsd(EzManiaChartInput chart, int keyCount)
        {
            var vector = EzMinaEnginePool.Rent().CalculateMsd(EzMinaNoteConverter.Convert(chart.Notes), keyCount, 1f);
            var msd = new Dictionary<string, double>(StringComparer.Ordinal);

            foreach (var (axis, value) in vector.Enumerate())
                msd[axis.ToMsdSkillId()] = value;

            return msd;
        }

        private static int countHolds(EzManiaChartInput chart)
        {
            int holds = 0;

            for (int i = 0; i < chart.Notes.Count; i++)
            {
                if (chart.Notes[i].IsHold)
                    holds++;
            }

            return holds;
        }

        private static void runChain(EzManiaChartInput chart, int keyCount, double holdRatio, int holdCount)
        {
            var msd = computeMsd(chart, keyCount);
            var info = EzChartSkillInfoComputer.Compute(chart);

            EzPersistedChartDan.TryComputeFromStored("chain-hash", Guid.Empty, msd, keyCount, holdRatio, 12.5, info, holdCount);
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

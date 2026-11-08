// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Metrics;
using osu.Game.EzOsuGame.Skills;
using osu.Game.EzOsuGame.Skills.Dan;
using osu.Game.EzOsuGame.Skills.Mina;
using osu.Game.EzOsuGame.Skills.SkillTag;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Tests.EzOsuGame.Analysis
{
    /// <summary>
    /// The dan estimate, the axes table and the typed structure summary must come from one analysis, reached through
    /// one entry point. These check that the estimator consumes the stored result instead of measuring its own.
    /// </summary>
    [TestFixture]
    public class EzChartAnalysisPipelineTest
    {
        [Test]
        public void Read_path_does_not_compute_when_the_analysis_is_absent()
        {
            var lookup = new FakeLookup();
            var estimator = new EzChartDanEstimator(lookup);

            Assert.That(estimator.TryEstimate(maniaBeatmap(), Array.Empty<Mod>()), Is.Null);
            Assert.That(lookup.computeCalls, Is.Zero);
        }

        [Test]
        public void Read_path_uses_the_shared_analysis_when_present()
        {
            var lookup = new FakeLookup { cached = analysis() };
            var estimator = new EzChartDanEstimator(lookup);

            var verdict = estimator.TryEstimate(maniaBeatmap(), Array.Empty<Mod>());

            Assert.That(verdict, Is.Not.Null);
            Assert.That(lookup.computeCalls, Is.Zero);
        }

        [Test]
        public void Batch_path_fills_the_store_and_reads_the_result_of_that_fill()
        {
            var lookup = new FakeLookup { computeResult = analysis() };
            var estimator = new EzChartDanEstimator(lookup);

            var verdict = estimator.TryEstimateOrCompute(maniaBeatmap(), Array.Empty<Mod>());

            Assert.That(verdict, Is.Not.Null);
            Assert.That(lookup.computeCalls, Is.EqualTo(1));
        }

        [Test]
        public void Verdict_is_derived_from_the_axis_table_of_the_analysis()
        {
            var lookup = new FakeLookup { cached = analysis() };
            var estimator = new EzChartDanEstimator(lookup);
            var beatmap = maniaBeatmap();

            var fromAnalysis = estimator.TryEstimate(beatmap, Array.Empty<Mod>());
            var fromSameAxes = EzChartDanEstimator.FromMsd(
                storedAxes(),
                (int)Math.Round(beatmap.Difficulty.CircleSize),
                EzHoldMetrics.HoldRatio(beatmap));

            Assert.That(fromAnalysis!.RawDan, Is.EqualTo(fromSameAxes!.RawDan).Within(1e-9));
            Assert.That(fromAnalysis.Side, Is.EqualTo(fromSameAxes.Side));
        }

        [Test]
        public void Estimator_without_a_lookup_has_no_live_source()
        {
            var estimator = new EzChartDanEstimator();
            Assert.That(estimator.TryEstimate(maniaBeatmap(), Array.Empty<Mod>()), Is.Null);
            Assert.That(estimator.TryEstimateOrCompute(maniaBeatmap(), Array.Empty<Mod>()), Is.Null);
        }

        [Test]
        public void Analysis_carries_the_typed_structure_summary_with_its_axes()
        {
            // Both halves of the structure pass travel together, so a consumer never re-runs the analysers.
            var withInfo = new EzAnalysisResult(new KpsSummary(1, 2, new[] { 1.0 }), chartSkillInfo: new EzChartSkillInfo { KeyCount = 4 });
            var withoutInfo = new EzAnalysisResult(new KpsSummary(1, 2, new[] { 1.0 }));

            Assert.That(withInfo.ChartSkillInfo, Is.Not.Null);
            Assert.That(withInfo.ChartSkillInfo!.KeyCount, Is.EqualTo(4));
            Assert.That(withoutInfo.ChartSkillInfo, Is.Null);

            // Projecting a tag summary must not drop it.
            Assert.That(withInfo.WithTagSummary(EzBeatmapTagSummary.EMPTY).ChartSkillInfo, Is.Not.Null);
        }

        private static BeatmapInfo maniaBeatmap()
        {
            var beatmap = new BeatmapInfo
            {
                ID = Guid.NewGuid(),
                Hash = Guid.NewGuid().ToString(),
                MD5Hash = Guid.NewGuid().ToString(),
                Ruleset = new ManiaRuleset().RulesetInfo,
                Difficulty = new BeatmapDifficulty { CircleSize = 4 },
                DifficultyName = "test",
            };

            return beatmap;
        }

        private static Dictionary<string, double> storedAxes()
        {
            var axes = new Dictionary<string, double>(StringComparer.Ordinal)
            {
                [EzMinaSkillAxis.Overall.ToMsdSkillId()] = 20,
                [EzMinaSkillAxis.Stream.ToMsdSkillId()] = 18,
                [EzMinaSkillAxis.JackSpeed.ToMsdSkillId()] = 16,
            };

            return axes;
        }

        private static EzAnalysisResult analysis()
        {
            var axes = new Dictionary<string, double>(StringComparer.Ordinal)
            {
                [EzMinaSkillAxis.Overall.ToId()] = 20,
                [EzMinaSkillAxis.Stream.ToId()] = 18,
                [EzMinaSkillAxis.JackSpeed.ToId()] = 16,
            };

            var table = new Dictionary<EzMetricProducerId, IReadOnlyDictionary<string, double>>
            {
                [EzMetricProducers.MINA_CHART] = axes,
            };

            // The pass resolves the sides once; the estimator is expected to read that, not derive its own.
            var dan = EzDanSet.Create(storedAxes(), keyCount: 4, holdRatio: 0.1, xxySr: null, chartInfo: null);

            return new EzAnalysisResult(new KpsSummary(1, 2, new[] { 1.0 }), axes: table,
                dan: dan,
                chartSkillInfo: new EzChartSkillInfo { KeyCount = 4 });
        }

        private sealed class FakeLookup : IEzChartAnalysisLookup
        {
            public EzAnalysisResult? cached;
            public EzAnalysisResult? computeResult;
            public int computeCalls;

            public EzAnalysisResult? TryGet(BeatmapInfo beatmap, IReadOnlyList<Mod>? mods) => cached;

            public EzAnalysisResult? GetOrCompute(BeatmapInfo beatmap, RulesetInfo ruleset, IReadOnlyList<Mod>? mods)
            {
                computeCalls++;
                cached = computeResult;
                return computeResult;
            }
        }
    }
}

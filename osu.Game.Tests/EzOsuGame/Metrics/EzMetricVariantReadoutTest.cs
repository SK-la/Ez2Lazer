// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Metrics;
using osu.Game.EzOsuGame.Skills.Mina;
using osu.Game.Rulesets.Mania;

namespace osu.Game.Tests.EzOsuGame.Metrics
{
    /// <summary>
    /// The Ez pill reads the selected system's own rating. A column that was never filled in must not read as a
    /// zero rating, and a measured axis must win over the column.
    /// </summary>
    [TestFixture]
    public class EzMetricVariantReadoutTest
    {
        [Test]
        public void Unfilled_column_reads_as_absent()
        {
            var beatmap = maniaBeatmap();
            beatmap.MinaRating = 0;

            Assert.That(EzMetricVariantReadout.SkillStar(beatmap, null), Is.Null);
        }

        [Test]
        public void Measured_axis_wins_over_the_column()
        {
            var beatmap = maniaBeatmap();
            beatmap.MinaRating = 12;

            var analysis = analysisWith(EzChartMetricProducer.Mina.Id(), EzMinaSkillAxis.Overall.ToId(), 21.5);

            Assert.That(EzMetricVariantReadout.SkillStar(beatmap, analysis), Is.EqualTo(21.5));
        }

        [Test]
        public void Mina_headline_is_the_overall_axis()
        {
            Assert.That(EzMetricVariantReadout.RatingAxisOf(EzChartMetricProducer.Mina.Id()), Is.EqualTo(EzMinaSkillAxis.Overall.ToId()));
            Assert.That(EzMetricVariantReadout.RatingAxisOf(EzChartMetricProducer.Ppp.Id()), Is.EqualTo(EzMetricAxisIds.STAR_RATING));
        }

        private static EzAnalysisResult analysisWith(EzMetricProducerId producer, string axisId, double value)
        {
            var table = new Dictionary<EzMetricProducerId, IReadOnlyDictionary<string, double>>
            {
                [producer] = new Dictionary<string, double>(StringComparer.Ordinal) { [axisId] = value },
            };

            return new EzAnalysisResult(new KpsSummary(1, 2, new[] { 1.0 }), axes: table);
        }

        private static BeatmapInfo maniaBeatmap() => new BeatmapInfo
        {
            ID = Guid.NewGuid(),
            Hash = Guid.NewGuid().ToString(),
            MD5Hash = Guid.NewGuid().ToString(),
            Ruleset = new ManiaRuleset().RulesetInfo,
            Difficulty = new BeatmapDifficulty { CircleSize = 4 },
            DifficultyName = "test",
        };
    }
}

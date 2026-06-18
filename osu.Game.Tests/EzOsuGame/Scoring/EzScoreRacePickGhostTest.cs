// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets.Osu;
using osu.Game.Scoring;

namespace osu.Game.Tests.EzOsuGame.Scoring
{
    [TestFixture]
    public class EzScoreRacePickGhostTest
    {
        [Test]
        public void TestPickBestSingleScoreReturnsSameForAllMetrics()
        {
            var score = createScore(totalScore: 1_000_000, accuracy: 0.97, maxCombo: 500);
            var scores = new List<ScoreInfo> { score };

            Assert.That(EzLocalScoreQueries.PickBest(scores, EzScoreRaceMetric.Accuracy)?.ID, Is.EqualTo(score.ID));
            Assert.That(EzLocalScoreQueries.PickBest(scores, EzScoreRaceMetric.TotalScore)?.ID, Is.EqualTo(score.ID));
            Assert.That(EzLocalScoreQueries.PickBest(scores, EzScoreRaceMetric.MaxCombo)?.ID, Is.EqualTo(score.ID));
        }

        [Test]
        public void TestPickBestTwoScoresIndependentByMetric()
        {
            var highAccuracy = createScore(totalScore: 900_000, accuracy: 0.99, maxCombo: 400);
            var highTotal = createScore(totalScore: 1_100_000, accuracy: 0.92, maxCombo: 600);
            var scores = new List<ScoreInfo> { highAccuracy, highTotal };

            Assert.That(EzLocalScoreQueries.PickBest(scores, EzScoreRaceMetric.Accuracy)?.ID, Is.EqualTo(highAccuracy.ID));
            Assert.That(EzLocalScoreQueries.PickBest(scores, EzScoreRaceMetric.TotalScore)?.ID, Is.EqualTo(highTotal.ID));
        }

        private static ScoreInfo createScore(long totalScore, double accuracy, int maxCombo)
        {
            return new ScoreInfo
            {
                Ruleset = new OsuRuleset().RulesetInfo,
                TotalScore = totalScore,
                Accuracy = accuracy,
                MaxCombo = maxCombo,
                Date = DateTimeOffset.UtcNow,
            };
        }
    }
}

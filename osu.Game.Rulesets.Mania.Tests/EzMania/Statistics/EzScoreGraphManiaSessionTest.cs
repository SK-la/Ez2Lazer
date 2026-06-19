// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge;
using osu.Game.Rulesets.Mania.EzMania.Statistics;
using osu.Game.Rulesets.Mania.Tests.EzMania.ReplayJudge;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.Statistics
{
    [TestFixture]
    public class EzScoreGraphManiaSessionTest
    {
        [Test]
        public void TestTimeRangeUsesOriginalFallbackWhenDisplayEmpty()
        {
            var (score, beatmap, _) = LazerTapReplayFixtures.CreateTwoNoteColumnTap();
            var sessionScore = ManiaReplaySession.Run(score.DeepClone(), beatmap, createSessionEnvironment(EzEnumHitMode.Lazer));
            var original = sessionScore.ScoreInfo.HitEvents.ToList();

            Assert.That(EzScoreGraphMania.ComputeTimeRangeForTesting(Array.Empty<HitEvent>(), original), Is.GreaterThan(0));
        }

        [Test]
        public void TestTimeRangeFromSessionEventsIsPositive()
        {
            var (score, beatmap, _) = LazerTapReplayFixtures.CreateTwoNoteColumnTap();
            var sessionScore = ManiaReplaySession.Run(score.DeepClone(), beatmap, createSessionEnvironment(EzEnumHitMode.Lazer));
            var display = sessionScore.ScoreInfo.HitEvents.ToList();

            Assert.That(EzScoreGraphMania.ComputeTimeRangeForTesting(display, display), Is.GreaterThan(0));
        }

        [Test]
        public void TestExtractDisplayCountsMatchesFilteredStatisticsTotal()
        {
            var (score, beatmap, _) = HitModeReplayFixtures.CreateEz2AcManyNoteTap();
            var sessionScore = ManiaReplaySession.Run(score.DeepClone(), beatmap, createSessionEnvironment(EzEnumHitMode.EZ2AC));
            var statistics = sessionScore.ScoreInfo.Statistics;

            var extracted = EzScoreGraphMania.ExtractDisplayCounts(statistics, EzEnumHitMode.EZ2AC);
            int extractedTotal = extracted.Values.Sum();

            int expectedTotal = statistics
                                  .Where(kvp => kvp.Value > 0)
                                  .Where(kvp => kvp.Key.IsBasic() || kvp.Key == HitResult.Poor || kvp.Key == HitResult.Miss)
                                  .Sum(kvp => kvp.Value);

            Assert.That(extractedTotal, Is.EqualTo(expectedTotal));
        }

        [Test]
        public void TestSessionStatisticsChangeBetweenHitModes()
        {
            var (score, beatmap, _) = HitModeReplayFixtures.CreateBmsEarlyBadWithPostBadKPoor();

            var lazerEnv = createSessionEnvironment(EzEnumHitMode.Lazer);
            var bmsEnv = createSessionEnvironment(EzEnumHitMode.IIDX_HD);

            var lazerRun = ManiaReplaySession.Run(score.DeepClone(), beatmap, lazerEnv);
            var bmsRun = ManiaReplaySession.Run(score.DeepClone(), beatmap, bmsEnv);

            var lazerCounts = EzScoreGraphMania.ExtractDisplayCounts(lazerRun.ScoreInfo.Statistics, EzEnumHitMode.Lazer);
            var bmsCounts = EzScoreGraphMania.ExtractDisplayCounts(bmsRun.ScoreInfo.Statistics, EzEnumHitMode.IIDX_HD);

            Assert.That(ManiaReplayParityHelper.AreStatisticsEquivalent(lazerRun.ScoreInfo.Statistics, bmsRun.ScoreInfo.Statistics), Is.False);
            Assert.That(lazerCounts, Is.Not.EqualTo(bmsCounts));
        }

        private static GameplayEnvironment createSessionEnvironment(EzEnumHitMode hitMode) => new GameplayEnvironment
        {
            ManiaHitMode = hitMode,
            ManiaHealthMode = EzEnumHealthMode.Lazer,
            JudgePrecedence = EzEnumJudgePrecedence.Earliest,
            OffsetPlusMania = 0,
        };
    }
}

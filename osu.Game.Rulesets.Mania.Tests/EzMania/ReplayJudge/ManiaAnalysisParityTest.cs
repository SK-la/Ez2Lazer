// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.ReplayJudge
{
    [TestFixture]
    public class ManiaAnalysisParityTest
    {
        [SetUp]
        public void SetUp() => ReplayJudgeTestConfig.ResetGlobalConfig();

        [TearDown]
        public void TearDown() => ReplayJudgeTestConfig.ResetGlobalConfig();

        [Test]
        public void TestRecalcWritebackPersistsStatisticsJson()
        {
            var sessionInfo = new ScoreInfo
            {
                Statistics =
                {
                    [HitResult.Perfect] = 17,
                    [HitResult.Good] = 3
                },
                MaximumStatistics =
                {
                    [HitResult.Perfect] = 20
                }
            };

            var stored = new ScoreInfo
            {
                StatisticsJson = JsonConvert.SerializeObject(new Dictionary<HitResult, int> { [HitResult.Perfect] = 999 }),
                MaximumStatisticsJson = JsonConvert.SerializeObject(new Dictionary<HitResult, int> { [HitResult.Perfect] = 999 }),
            };

            ScoreManager.ApplyEzSessionRecalculationToDetachedScoreInfo(
                stored,
                sessionInfo,
                ReplayRunPurpose.ForStored,
                ReplayJudgeTestConfig.Create(EzEnumHitMode.Lazer, EzEnumHealthMode.Lazer));

            var reloaded = new ScoreInfo { StatisticsJson = stored.StatisticsJson, MaximumStatisticsJson = stored.MaximumStatisticsJson };

            Assert.That(reloaded.Statistics.GetValueOrDefault(HitResult.Perfect), Is.EqualTo(17));
            Assert.That(reloaded.Statistics.GetValueOrDefault(HitResult.Good), Is.EqualTo(3));
            Assert.That(reloaded.MaximumStatistics.GetValueOrDefault(HitResult.Perfect), Is.EqualTo(20));
        }

        [Test]
        public void TestForLiveRecalcPersistsNonLazerModes()
        {
            var scoreInfo = new ScoreInfo();
            var environment = ReplayJudgeTestConfig.Create(EzEnumHitMode.EZ2AC, EzEnumHealthMode.Ez2Ac);

            ScoreManager.ApplyEzSessionRecalculationToDetachedScoreInfo(
                scoreInfo,
                new ScoreInfo(),
                ReplayRunPurpose.ForLive,
                environment);

            Assert.That(scoreInfo.ManiaHitMode, Is.EqualTo((int)EzEnumHitMode.EZ2AC));
            Assert.That(scoreInfo.ManiaHealthMode, Is.EqualTo((int)EzEnumHealthMode.Ez2Ac));
        }

        [TestCaseSource(nameof(same_environment_cases))]
        public async Task TestSameEnvThreeEntryListEqualsNowFinal(AnalysisFixtureCase testCase)
        {
            var nowScoreInfo = await runForLiveScoreInfo(testCase.CreateFixture, testCase.Environment).ConfigureAwait(true);
            var now = snapshot(nowScoreInfo);
            var stored = await runForStoredSnapshot(testCase.CreateFixture, testCase.Environment, testCase.Environment).ConfigureAwait(true);

            assertEquivalent(stored, now, $"{testCase.Name}: ForStored vs ForLive");

            var (score, beatmap, _) = testCase.CreateFixture();
            ReplayJudgeTestConfig.ApplyEmbeddedModes(score, testCase.Environment);

            var listAfterForLiveRecalc = score.ScoreInfo.DeepClone();
            ScoreManager.ApplyEzSessionRecalculationToDetachedScoreInfo(
                listAfterForLiveRecalc,
                nowScoreInfo,
                ReplayRunPurpose.ForLive,
                testCase.Environment);

            assertEquivalent(snapshot(listAfterForLiveRecalc), now, $"{testCase.Name}: a(list ForLive recalc) vs b(Now)");

            var forStoredSession = await new ManiaReplaySessionService().RunAsync(score.DeepClone(), beatmap, ReplayRunPurpose.ForStored).ConfigureAwait(true);
            var listAfterForStoredRecalc = score.ScoreInfo.DeepClone();
            ScoreManager.ApplyEzSessionRecalculationToDetachedScoreInfo(
                listAfterForStoredRecalc,
                forStoredSession.ScoreInfo,
                ReplayRunPurpose.ForStored,
                testCase.Environment);

            assertEquivalent(snapshot(listAfterForStoredRecalc), stored, $"{testCase.Name}: a(list ForStored recalc) vs ForStored Session");
            assertEquivalent(snapshot(listAfterForStoredRecalc), now, $"{testCase.Name}: a(list ForStored recalc) vs b(Now) same env");
        }

        [TestCaseSource(nameof(recalculation_cases))]
        public async Task TestRecalcLoopAbcdAllHitModePairs(RecalculationFixtureCase testCase)
        {
            var nowScoreInfo = await runForLiveScoreInfo(testCase.CreateFixture, testCase.SourceEnvironment, testCase.TargetEnvironment).ConfigureAwait(true);
            var now = snapshot(nowScoreInfo);

            var (staleScore, staleBeatmap, _) = testCase.CreateFixture();
            ReplayJudgeTestConfig.ApplyEmbeddedModes(staleScore, testCase.SourceEnvironment);
            ReplayJudgeTestConfig.ApplyToGlobalConfig(testCase.SourceEnvironment);
            var staleStored = await new ManiaReplaySessionService().RunAsync(staleScore.DeepClone(), staleBeatmap, ReplayRunPurpose.ForStored).ConfigureAwait(true);

            var (score, _, _) = testCase.CreateFixture();
            ReplayJudgeTestConfig.ApplyEmbeddedModes(score, testCase.SourceEnvironment);
            var recalculatedScoreInfo = score.ScoreInfo.DeepClone();
            recalculatedScoreInfo.HitEvents = staleStored.ScoreInfo.HitEvents.ToList();

            ScoreManager.ApplyEzSessionRecalculationToDetachedScoreInfo(
                recalculatedScoreInfo,
                nowScoreInfo,
                ReplayRunPurpose.ForLive,
                testCase.TargetEnvironment);

            var listAfterRecalc = snapshot(recalculatedScoreInfo);
            assertEquivalent(listAfterRecalc, now, $"{testCase.Name}: a(list after recalc) vs b(Now)");

            Assert.That(recalculatedScoreInfo.ManiaHitMode, Is.EqualTo(expectedPersistedHitMode(testCase.TargetEnvironment)), $"{testCase.Name}: recalculated hit mode");
            Assert.That(recalculatedScoreInfo.ManiaHealthMode, Is.EqualTo(expectedPersistedHealthMode(testCase.TargetEnvironment)), $"{testCase.Name}: recalculated health mode");

            var (storedScore, storedBeatmap, _) = testCase.CreateFixture();
            var forStoredAfterRecalc = await snapshotFromRunAsync(
                new ManiaReplaySessionService(),
                new Score { ScoreInfo = recalculatedScoreInfo.DeepClone(), Replay = storedScore.Replay },
                storedBeatmap,
                ReplayRunPurpose.ForStored).ConfigureAwait(true);

            var (liveScore, liveBeatmap, _) = testCase.CreateFixture();
            ReplayJudgeTestConfig.ApplyToGlobalConfig(testCase.TargetEnvironment);
            var nowAfterRecalc = snapshot(await scoreInfoFromRunRequestAsync(
                new ManiaReplaySessionService(),
                new Score { ScoreInfo = recalculatedScoreInfo.DeepClone(), Replay = liveScore.Replay },
                liveBeatmap).ConfigureAwait(true));

            assertEquivalent(forStoredAfterRecalc, nowAfterRecalc, $"{testCase.Name}: ForStored vs ForLive after recalc");
            assertEquivalent(nowAfterRecalc, now, $"{testCase.Name}: d(Now after recalc) vs b(Now)");

            if (testCase.SourceEnvironment.ManiaHitMode == testCase.TargetEnvironment.ManiaHitMode
                && testCase.SourceEnvironment.ManiaHealthMode == testCase.TargetEnvironment.ManiaHealthMode)
            {
                var beforeRecalc = snapshot(await runForLiveScoreInfo(testCase.CreateFixture, testCase.SourceEnvironment, testCase.TargetEnvironment).ConfigureAwait(true));
                assertEquivalent(listAfterRecalc, beforeRecalc, $"{testCase.Name}: X=Y recalc is idempotent");
            }
        }

        private static IEnumerable<AnalysisFixtureCase> same_environment_cases()
        {
            yield return new AnalysisFixtureCase("Lazer tap", LazerTapReplayFixtures.CreateTwoNoteColumnTap, ReplayJudgeTestConfig.Create(EzEnumHitMode.Lazer, EzEnumHealthMode.Lazer));
            yield return new AnalysisFixtureCase("IIDX tap", BmsTapReplayFixtures.CreateTwoNoteColumnTap, ReplayJudgeTestConfig.Create(EzEnumHitMode.IIDX_HD, EzEnumHealthMode.IIDX_HD));
            yield return new AnalysisFixtureCase("EZ2AC tap", () => HitModeReplayFixtures.CreateEz2AcManyNoteTap(), ReplayJudgeTestConfig.Create(EzEnumHitMode.EZ2AC, EzEnumHealthMode.Ez2Ac));
            yield return new AnalysisFixtureCase("O2Jam tap", HitModeReplayFixtures.CreateO2TwoNoteTap, ReplayJudgeTestConfig.Create(EzEnumHitMode.O2Jam, EzEnumHealthMode.O2JamNormal));
            yield return new AnalysisFixtureCase("Malody hold", () => HitModeReplayFixtures.CreateMalodyHoldPerfect(EzEnumHitMode.Malody_E), ReplayJudgeTestConfig.Create(EzEnumHitMode.Malody_E, EzEnumHealthMode.Lazer));
        }

        private static IEnumerable<RecalculationFixtureCase> recalculation_cases()
        {
            var lazer = ReplayJudgeTestConfig.Create(EzEnumHitMode.Lazer, EzEnumHealthMode.Lazer);
            var iidx = ReplayJudgeTestConfig.Create(EzEnumHitMode.IIDX_HD, EzEnumHealthMode.IIDX_HD, bmsPoorHitResultEnable: true);
            var ez2AC = ReplayJudgeTestConfig.Create(EzEnumHitMode.EZ2AC, EzEnumHealthMode.Ez2Ac);
            var o2Jam = ReplayJudgeTestConfig.Create(EzEnumHitMode.O2Jam, EzEnumHealthMode.O2JamNormal);
            var malody = ReplayJudgeTestConfig.Create(EzEnumHitMode.Malody_E, EzEnumHealthMode.Lazer);

            yield return new RecalculationFixtureCase("Lazer -> Lazer", LazerTapReplayFixtures.CreateTwoNoteColumnTap, lazer, lazer);
            yield return new RecalculationFixtureCase("IIDX -> IIDX", BmsTapReplayFixtures.CreateTwoNoteColumnTap, iidx, iidx);
            yield return new RecalculationFixtureCase("EZ2AC -> EZ2AC", () => HitModeReplayFixtures.CreateEz2AcManyNoteTap(), ez2AC, ez2AC);
            yield return new RecalculationFixtureCase("O2Jam -> O2Jam", HitModeReplayFixtures.CreateO2TwoNoteTap, o2Jam, o2Jam);
            yield return new RecalculationFixtureCase("Lazer -> IIDX", LazerTapReplayFixtures.CreateTwoNoteColumnTap, lazer, iidx);
            yield return new RecalculationFixtureCase("IIDX -> Lazer", BmsTapReplayFixtures.CreateTwoNoteColumnTap, iidx, lazer);
            yield return new RecalculationFixtureCase("Lazer -> EZ2AC", LazerTapReplayFixtures.CreateTwoNoteColumnTap, lazer, ez2AC);
            yield return new RecalculationFixtureCase("O2Jam -> Lazer", HitModeReplayFixtures.CreateO2TwoNoteTap, o2Jam, lazer);
            yield return new RecalculationFixtureCase("Malody -> IIDX", () => HitModeReplayFixtures.CreateMalodyHoldPerfect(EzEnumHitMode.Malody_E), malody, iidx);
        }

        private static async Task<ScoreInfo> scoreInfoFromRunRequestAsync(ManiaReplaySessionService service, Score score, IBeatmap beatmap)
        {
            var result = await service.RunRequestAsync(new ReplayRunRequest(score, beatmap, ReplayRunPurpose.ForLive)).ConfigureAwait(true);
            return result.Score.ScoreInfo;
        }

        private static async Task<ScoreInfo> runForLiveScoreInfo(
            Func<(Score score, IBeatmap beatmap, GameplayEnvironment environment)> createFixture,
            GameplayEnvironment environment)
            => await runForLiveScoreInfo(createFixture, environment, environment).ConfigureAwait(true);

        private static async Task<ScoreInfo> runForLiveScoreInfo(
            Func<(Score score, IBeatmap beatmap, GameplayEnvironment environment)> createFixture,
            GameplayEnvironment embeddedEnvironment,
            GameplayEnvironment liveEnvironment)
        {
            var (score, beatmap, _) = createFixture();
            ReplayJudgeTestConfig.ApplyEmbeddedModes(score, embeddedEnvironment);
            ReplayJudgeTestConfig.ApplyToGlobalConfig(liveEnvironment);
            var live = GlobalConfigStore.EzConfig.GetGameplayEnvironment();
            Assert.That(live.ManiaHitMode, Is.EqualTo(liveEnvironment.ManiaHitMode));
            Assert.That(live.ManiaHealthMode, Is.EqualTo(liveEnvironment.ManiaHealthMode));
            return await scoreInfoFromRunRequestAsync(new ManiaReplaySessionService(), score.DeepClone(), beatmap).ConfigureAwait(true);
        }

        private static async Task<ScoreSnapshot> runForStoredSnapshot(
            Func<(Score score, IBeatmap beatmap, GameplayEnvironment environment)> createFixture,
            GameplayEnvironment embeddedEnvironment,
            GameplayEnvironment liveEnvironment)
        {
            var (score, beatmap, _) = createFixture();
            ReplayJudgeTestConfig.ApplyEmbeddedModes(score, embeddedEnvironment);
            ReplayJudgeTestConfig.ApplyToGlobalConfig(liveEnvironment);
            return await snapshotFromRunAsync(new ManiaReplaySessionService(), score.DeepClone(), beatmap, ReplayRunPurpose.ForStored).ConfigureAwait(true);
        }

        private static async Task<ScoreSnapshot> snapshotFromRunAsync(ManiaReplaySessionService service, Score score, IBeatmap beatmap, ReplayRunPurpose purpose)
        {
            var result = await service.RunAsync(score, beatmap, purpose).ConfigureAwait(true);
            return snapshot(result.ScoreInfo);
        }

        private static ScoreSnapshot snapshot(ScoreInfo scoreInfo)
            => new ScoreSnapshot(
                scoreInfo.HitEvents.ToList(),
                scoreInfo.Statistics.ToDictionary(),
                scoreInfo.MaximumStatistics.ToDictionary(),
                scoreInfo.Accuracy,
                scoreInfo.TotalScore,
                scoreInfo.ManiaHitMode,
                scoreInfo.ManiaHealthMode);

        private static void assertEquivalent(ScoreSnapshot expected, ScoreSnapshot actual, string context)
        {
            Assert.That(actual.Accuracy, Is.EqualTo(expected.Accuracy),
                $"{context}: accuracy expected={describeSnapshot(expected)} actual={describeSnapshot(actual)}");
            Assert.That(actual.TotalScore, Is.EqualTo(expected.TotalScore),
                $"{context}: total score expected={describeSnapshot(expected)} actual={describeSnapshot(actual)}");
            Assert.That(ManiaReplayParityHelper.AreStatisticsEquivalent(expected.Statistics, actual.Statistics), Is.True,
                () => $"{context}: statistics expected=[{ManiaReplayParityHelper.DescribeStatistics(expected.Statistics)}] actual=[{ManiaReplayParityHelper.DescribeStatistics(actual.Statistics)}]");
            Assert.That(ManiaReplayParityHelper.AreStatisticsEquivalent(expected.MaximumStatistics, actual.MaximumStatistics), Is.True,
                () => $"{context}: max statistics expected=[{ManiaReplayParityHelper.DescribeStatistics(expected.MaximumStatistics)}] actual=[{ManiaReplayParityHelper.DescribeStatistics(actual.MaximumStatistics)}]");
            Assert.That(ManiaReplayParityHelper.AreHitEventsEquivalent(expected.HitEvents, actual.HitEvents), Is.True,
                () => $"{context}: hit events expected=[{ManiaReplayParityHelper.DescribeHitEvents(expected.HitEvents)}] actual=[{ManiaReplayParityHelper.DescribeHitEvents(actual.HitEvents)}]");
        }

        private static string describeSnapshot(ScoreSnapshot snapshot)
            => $"acc={snapshot.Accuracy:F8} score={snapshot.TotalScore} hitMode={snapshot.ManiaHitMode} health={snapshot.ManiaHealthMode} stats=[{ManiaReplayParityHelper.DescribeStatistics(snapshot.Statistics)}]";

        private static int expectedPersistedHitMode(GameplayEnvironment environment)
            => (int)environment.ManiaHitMode;

        private static int expectedPersistedHealthMode(GameplayEnvironment environment)
            => (int)environment.ManiaHealthMode;

        public sealed record AnalysisFixtureCase(
            string Name,
            Func<(Score score, IBeatmap beatmap, GameplayEnvironment environment)> CreateFixture,
            GameplayEnvironment Environment)
        {
            public override string ToString() => Name;
        }

        public sealed record RecalculationFixtureCase(
            string Name,
            Func<(Score score, IBeatmap beatmap, GameplayEnvironment environment)> CreateFixture,
            GameplayEnvironment SourceEnvironment,
            GameplayEnvironment TargetEnvironment)
        {
            public override string ToString() => Name;
        }

        private sealed record ScoreSnapshot(
            List<HitEvent> HitEvents,
            Dictionary<HitResult, int> Statistics,
            Dictionary<HitResult, int> MaximumStatistics,
            double Accuracy,
            long TotalScore,
            int ManiaHitMode,
            int ManiaHealthMode);
    }
}

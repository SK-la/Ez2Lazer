// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Osu.Tests.EzOsu.Scoring
{
    /// <summary>
    /// OSL-013：std 判定轨的 ForStored 解析与 mania 同口径 —— 只认成绩嵌入的
    /// <see cref="ScoreInfo.JudgeMode"/>，存量负值按 Lazer，不回读实时配置。
    /// 展示入口 <see cref="EzScoreInfoExtensions.TryGetStdGameplayMode"/> 必须给出同一档位，
    /// 否则排行榜标签与右键重算会各判一版。
    /// </summary>
    [TestFixture]
    public class OsuResolveEnvironmentTest
    {
        private Ez2ConfigManager config = null!;

        private EzEnumJudgeMode originalStdJudgeMode;

        [SetUp]
        public void SetUp()
        {
            config = GlobalConfigStore.EzConfig;
            originalStdJudgeMode = config.Get<EzEnumJudgeMode>(Ez2Setting.StdJudgeMode);
        }

        [TearDown]
        public void TearDown() => setLiveStdJudgeMode(originalStdJudgeMode);

        [Test]
        public void TestForStoredUsesEmbeddedStdJudgeMode()
        {
            setLiveStdJudgeMode(EzEnumJudgeMode.Lazer);
            var scoreInfo = createOsuScore((int)EzEnumJudgeMode.StableClassic);

            var env = config.ResolveEnvironment(ReplayRunPurpose.ForStored, scoreInfo);

            Assert.That(env.JudgeMode, Is.EqualTo(EzEnumJudgeMode.StableClassic));
            Assert.That(env.OffsetPlusNonMania, Is.EqualTo(0));
        }

        [Test]
        public void TestForStoredLegacyUnsetFallsBackToLazerNotLiveConfig()
        {
            setLiveStdJudgeMode(EzEnumJudgeMode.StableClassic);
            var scoreInfo = createOsuScore(-1);

            var env = config.ResolveEnvironment(ReplayRunPurpose.ForStored, scoreInfo);

            Assert.That(env.JudgeMode, Is.EqualTo(EzEnumJudgeMode.Lazer));

            Assert.That(scoreInfo.TryGetStdGameplayMode(out int displayMode), Is.True);
            Assert.That(displayMode, Is.EqualTo((int)env.JudgeMode), "展示入口与解析入口必须给出同一判定轨");
        }

        [Test]
        public void TestForStoredWithoutScoreDoesNotReadLiveStdJudgeMode()
        {
            setLiveStdJudgeMode(EzEnumJudgeMode.StableClassic);

            var env = config.ResolveEnvironment(ReplayRunPurpose.ForStored, score: null, ruleset: new OsuRuleset().RulesetInfo);

            Assert.That(env.JudgeMode, Is.EqualTo(EzEnumJudgeMode.Lazer));
        }

        [Test]
        public void TestForLiveReadsStdJudgeMode()
        {
            setLiveStdJudgeMode(EzEnumJudgeMode.StableClassic);

            var env = config.ResolveEnvironment(ReplayRunPurpose.ForLive, score: null, ruleset: new OsuRuleset().RulesetInfo);

            Assert.That(env.JudgeMode, Is.EqualTo(EzEnumJudgeMode.StableClassic));
        }

        [Test]
        public void TestStdJudgeModeKeyedByOsuOnlineID()
        {
            // 与 Ez2ConfigManager.resolveLiveJudgeMode / resolveStoredModes 同键（OnlineID），不按 ShortName。
            var unknownRuleset = new ScoreInfo { Ruleset = new RulesetInfo { OnlineID = -1 } };

            Assert.That(unknownRuleset.TryGetStdGameplayMode(out _), Is.False);

            var osuTrackScore = createOsuScore((int)EzEnumJudgeMode.StableClassic);

            Assert.That(osuTrackScore.TryGetStdGameplayMode(out int stdMode), Is.True);
            Assert.That(stdMode, Is.EqualTo((int)EzEnumJudgeMode.StableClassic));
        }

        private void setLiveStdJudgeMode(EzEnumJudgeMode mode) => config.SetValue(Ez2Setting.StdJudgeMode, mode);

        private static ScoreInfo createOsuScore(int judgeMode) => new ScoreInfo
        {
            Ruleset = new OsuRuleset().RulesetInfo,
            JudgeMode = judgeMode,
        };
    }
}

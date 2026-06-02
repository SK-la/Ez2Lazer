// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets.Mania;
using osu.Game.Scoring;

namespace osu.Game.Tests.EzOsuGame.Scoring
{
    [TestFixture]
    public class EzOnlineScoreSubmissionPolicyTest
    {
        [Test]
        public void TestAllowsLazerManiaWithDefaultSessionSettings()
        {
            var score = createManiaScore((int)EzEnumHitMode.Lazer, (int)EzEnumHealthMode.Lazer);
            score.SessionOffsetPlusMania = 0;
            score.SessionAccuracyCutoffA = EzOnlineScoreSubmissionPolicy.DEFAULT_ACCURACY_CUTOFF_A;
            score.SessionAccuracyCutoffS = EzOnlineScoreSubmissionPolicy.DEFAULT_ACCURACY_CUTOFF_S;

            Assert.That(EzOnlineScoreSubmissionPolicy.AllowsOfficialSubmission(score), Is.True);
        }

        [Test]
        public void TestBlocksNonLazerHitModeOnScoreSnapshot()
        {
            var score = createManiaScore((int)EzEnumHitMode.O2Jam, (int)EzEnumHealthMode.Lazer);

            Assert.That(EzOnlineScoreSubmissionPolicy.AllowsOfficialSubmission(score), Is.False);
        }

        [Test]
        public void TestBlocksNonLazerHealthModeOnScoreSnapshot()
        {
            var score = createManiaScore((int)EzEnumHitMode.Lazer, (int)EzEnumHealthMode.O2JamNormal);

            Assert.That(EzOnlineScoreSubmissionPolicy.AllowsOfficialSubmission(score), Is.False);
        }

        [Test]
        public void TestBlocksSessionOffsetEvenIfGlobalConfigWouldBeZero()
        {
            var score = createManiaScore((int)EzEnumHitMode.Lazer, (int)EzEnumHealthMode.Lazer);
            score.SessionOffsetPlusMania = 15;

            Assert.That(EzOnlineScoreSubmissionPolicy.AllowsOfficialSubmission(score), Is.False);
        }

        [Test]
        public void TestBlocksWhenBothAccuracyCutoffsAreCustom()
        {
            var score = createManiaScore((int)EzEnumHitMode.Lazer, (int)EzEnumHealthMode.Lazer);
            score.SessionAccuracyCutoffA = 0.85;
            score.SessionAccuracyCutoffS = 0.92;

            Assert.That(EzOnlineScoreSubmissionPolicy.AllowsOfficialSubmission(score), Is.False);
        }

        [Test]
        public void TestAllowsWhenOneAccuracyCutoffRemainsDefault()
        {
            var score = createManiaScore((int)EzEnumHitMode.Lazer, (int)EzEnumHealthMode.Lazer);
            score.SessionAccuracyCutoffA = 0.85;
            score.SessionAccuracyCutoffS = EzOnlineScoreSubmissionPolicy.DEFAULT_ACCURACY_CUTOFF_S;

            Assert.That(EzOnlineScoreSubmissionPolicy.AllowsOfficialSubmission(score), Is.True);
        }

        private static ScoreInfo createManiaScore(int hitMode, int healthMode) => new ScoreInfo
        {
            Ruleset = new ManiaRuleset().RulesetInfo,
            ManiaHitMode = hitMode,
            ManiaHealthMode = healthMode,
        };
    }
}

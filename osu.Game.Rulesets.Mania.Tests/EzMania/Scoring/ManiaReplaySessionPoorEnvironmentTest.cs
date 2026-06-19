// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge;
using osu.Game.Rulesets.Mania.Tests.EzMania.ReplayJudge;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.Scoring
{
    [TestFixture]
    public class ManiaReplaySessionPoorEnvironmentTest
    {
        [Test]
        public void TestSessionPoorEnabledFromEnvironmentWithoutGlobalConfig()
        {
            var (score, beatmap, environment) = HitModeReplayFixtures.CreateBmsEarlyBadWithPostBadKPoor();

            var config = GlobalConfigStore.EzConfig;
            GlobalConfigStore.EzConfig = config;
            config.SetValue(Ez2Setting.BmsPoorHitResultEnable, false);

            var run = ManiaReplaySession.Run(score, beatmap, environment);

            Assert.That(run.ScoreInfo.Statistics.TryGetValue(HitResult.Poor, out int poorCount) && poorCount > 0, Is.True);
        }
    }
}

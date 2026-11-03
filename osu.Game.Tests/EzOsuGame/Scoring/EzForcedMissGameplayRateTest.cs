// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Tests.EzOsuGame.Scoring
{
    /// <summary>
    /// 暂停 / 回放侧栏「强制结算」走 <see cref="ScoreProcessor.ApplyRemainingForcedMisses"/> 补齐剩余判定，
    /// 这些合成结果必须带 GameplayRate，否则结算页 UnstableRate 会对整份 HitEvents 断言失败。
    /// </summary>
    [TestFixture]
    public class EzForcedMissGameplayRateTest
    {
        [Test]
        public void TestForcedMissesCarryGameplayRate()
        {
            var beatmap = new ManiaBeatmap(new StageDefinition(4))
            {
                HitObjects = new List<ManiaHitObject>
                {
                    new Note { StartTime = 1000, Column = 0 },
                    new Note { StartTime = 2000, Column = 1 },
                }
            };

            var scoreProcessor = new ManiaRuleset().CreateScoreProcessor();
            scoreProcessor.ApplyBeatmap(beatmap);

            scoreProcessor.ApplyRemainingForcedMisses(null, 1.5);

            Assert.That(scoreProcessor.HitEvents, Is.Not.Empty);
            Assert.That(scoreProcessor.HitEvents, Has.All.Matches<HitEvent>(e => e.GameplayRate == 1.5));
        }
    }
}

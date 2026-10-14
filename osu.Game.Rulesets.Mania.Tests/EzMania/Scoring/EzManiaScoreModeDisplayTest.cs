// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Framework.Graphics.Containers;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.Scoring
{
    [TestFixture]
    public class EzManiaScoreModeDisplayTest
    {
        [TestCase(0, 0)]
        [TestCase(-1, -1)]
        public void TestDefaultAndLegacyModesReadAsLazer(int judgeMode, int healthMode)
        {
            var score = createScore(judgeMode, healthMode);

            Assert.That(score.TryGetManiaGameplayModes(out int resolvedJudgeMode, out int resolvedHealthMode), Is.True);
            Assert.That(resolvedJudgeMode, Is.EqualTo((int)EzEnumJudgeMode.Lazer));
            Assert.That(resolvedHealthMode, Is.EqualTo((int)EzEnumHealthMode.Lazer));
        }

        [TestCase(0, 0, false)]
        [TestCase(-1, -1, false)]
        [TestCase((int)EzEnumJudgeMode.EZ2AC, (int)EzEnumHealthMode.Ez2Ac, true)]
        [TestCase((int)EzEnumJudgeMode.EZ2AC, (int)EzEnumHealthMode.Lazer, true)]
        public void TestBadgeOnlyForEzPrivateModes(int judgeMode, int healthMode, bool expectsBadge)
        {
            var score = createScore(judgeMode, healthMode);

            Assert.That(EzScoreInfoExtensions.CreateDisplayDrawable(score) is FillFlowContainer, Is.EqualTo(expectsBadge));
        }

        private static ScoreInfo createScore(int judgeMode, int healthMode) => new ScoreInfo
        {
            Ruleset = new ManiaRuleset().RulesetInfo,
            ManiaHitMode = judgeMode,
            ManiaHealthMode = healthMode,
        };
    }
}

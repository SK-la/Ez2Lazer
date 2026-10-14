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

        /// <summary>
        /// Lazer 是默认档，不展示：默认档那一行必须被省略（而不是画成 "Lazer"）。
        /// </summary>
        [TestCase(0, 0, 0)]
        [TestCase(-1, -1, 0)]
        [TestCase((int)EzEnumJudgeMode.EZ2AC, (int)EzEnumHealthMode.Ez2Ac, 2)]
        [TestCase((int)EzEnumJudgeMode.EZ2AC, (int)EzEnumHealthMode.Lazer, 1)]
        [TestCase((int)EzEnumJudgeMode.Lazer, (int)EzEnumHealthMode.O2JamNormal, 1)]
        public void TestLazerRowsAreOmitted(int judgeMode, int healthMode, int expectedLines)
        {
            var score = createScore(judgeMode, healthMode);

            var block = EzScoreInfoExtensions.CreateDisplayDrawable(score) as FillFlowContainer;

            if (expectedLines == 0)
                Assert.That(block, Is.Null);
            else
            {
                Assert.That(block, Is.Not.Null);
                Assert.That(block!.Children.Count, Is.EqualTo(expectedLines));
            }
        }

        private static ScoreInfo createScore(int judgeMode, int healthMode) => new ScoreInfo
        {
            Ruleset = new ManiaRuleset().RulesetInfo,
            JudgeMode = judgeMode,
            HealthMode = healthMode,
        };
    }
}

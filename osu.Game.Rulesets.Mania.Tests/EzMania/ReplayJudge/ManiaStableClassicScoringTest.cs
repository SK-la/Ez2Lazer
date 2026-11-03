// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge.Mappings;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.ReplayJudge
{
    /// <summary>
    /// <see cref="StableManiaScoring"/> 的连击加成状态机回归：
    /// 增益 / 减益、384 重置（判定与持有增量两条路径）、Miss 不给分。
    /// </summary>
    [TestFixture]
    public class ManiaStableClassicScoringTest
    {
        private const double score_multiplier = StableManiaScoring.SCORE_MAX / 3.0;

        [Test]
        public void TestComboBonusResetsOnJudgement()
        {
            var scoring = new StableManiaScoring();
            scoring.Reset(score_multiplier);

            scoring.Apply(StableManiaJudgement.Good200, 0);
            Assert.That(scoring.ComboAddition, Is.EqualTo(StableManiaScoring.COMBO_MULTI - 8));

            scoring.Apply(StableManiaJudgement.Perfect300g, StableManiaScoring.COMBO_BONUS);
            Assert.That(scoring.ComboAddition, Is.EqualTo(StableManiaScoring.COMBO_MULTI));
        }

        [Test]
        public void TestComboBonusResetsOnHoldTick()
        {
            var scoring = new StableManiaScoring();
            scoring.Reset(score_multiplier);

            scoring.Apply(StableManiaJudgement.Good200, 0);
            double decreased = scoring.ComboAddition;

            scoring.ApplyHoldTick(StableManiaScoring.COMBO_BONUS - 1);
            Assert.That(scoring.ComboAddition, Is.EqualTo(decreased), "未到 384 不得重置");

            scoring.ApplyHoldTick(StableManiaScoring.COMBO_BONUS);
            Assert.That(scoring.ComboAddition, Is.EqualTo(StableManiaScoring.COMBO_MULTI), "持有增量跨过 384 同样重置");

            scoring.ApplyHoldTick(0);
            Assert.That(scoring.ComboAddition, Is.EqualTo(StableManiaScoring.COMBO_MULTI), "combo 0 不得触发重置");
        }

        [Test]
        public void TestHoldTickDoesNotCreateUndoEntry()
        {
            var scoring = new StableManiaScoring();
            scoring.Reset(score_multiplier);

            scoring.Apply(StableManiaJudgement.Good200, 0);
            double afterHit = scoring.ComboAddition;
            double totalAfterHit = scoring.TotalScoreDouble;

            scoring.ApplyHoldTick(StableManiaScoring.COMBO_BONUS);
            Assert.That(scoring.ComboAddition, Is.EqualTo(StableManiaScoring.COMBO_MULTI));
            Assert.That(scoring.TotalScoreDouble, Is.EqualTo(totalAfterHit), "持有增量不加分");

            scoring.Revert();

            Assert.That(scoring.ComboAddition, Is.EqualTo(StableManiaScoring.COMBO_MULTI), "Revert 只回退最近一次判定");
            Assert.That(scoring.TotalScoreDouble, Is.EqualTo(0).Within(1e-9));
        }

        [Test]
        public void TestTotalMatchesStableFormula()
        {
            var scoring = new StableManiaScoring();
            scoring.Reset(score_multiplier);

            double expected = 0;
            double comboAddition = StableManiaScoring.COMBO_MULTI;

            (StableManiaJudgement judgement, int comboWeight, int baseScore, int comboAtJudgement)[] sequence =
            {
                (StableManiaJudgement.Perfect300g, 32, 320, 0),
                (StableManiaJudgement.Great300, 32, 300, 1),
                (StableManiaJudgement.Good200, 16, 200, 2),
                (StableManiaJudgement.Ok100, 8, 100, 3),
                (StableManiaJudgement.Meh50, 4, 50, 4),
                (StableManiaJudgement.Miss, 0, 0, 0),
            };

            foreach (var (judgement, comboWeight, baseScore, comboAtJudgement) in sequence)
            {
                comboAddition = nextComboAddition(comboAddition, judgement, comboAtJudgement);
                expected += Math.Sqrt(comboAddition) * comboWeight * score_multiplier / StableManiaScoring.SCORE300G
                            + baseScore * score_multiplier / StableManiaScoring.SCORE300G;

                scoring.Apply(judgement, comboAtJudgement);

                Assert.That(scoring.ComboAddition, Is.EqualTo(comboAddition), () => $"{judgement} 后的 ComboAddition");
                Assert.That(scoring.TotalScoreDouble, Is.EqualTo(expected).Within(1e-9), () => $"{judgement} 后的累计分");
            }
        }

        [Test]
        public void TestMissScoresNothing()
        {
            var scoring = new StableManiaScoring();
            scoring.Reset(score_multiplier);

            scoring.Apply(StableManiaJudgement.Miss, 0);

            Assert.That(scoring.TotalScoreDouble, Is.EqualTo(0));
            Assert.That(scoring.Total, Is.EqualTo(0));
            Assert.That(scoring.ComboAddition, Is.EqualTo(0), "连击加成夹在 [0, 100]");
        }

        [Test]
        public void TestPenaltyDividedByScoreIncreaseMultiplier()
        {
            const double divisor = 1.06;

            var scoring = new StableManiaScoring();
            scoring.Reset(score_multiplier, divisor);

            scoring.Apply(StableManiaJudgement.Good200, 0);
            Assert.That(scoring.ComboAddition, Is.EqualTo(StableManiaScoring.COMBO_MULTI - 8 / divisor).Within(1e-9));

            // 增益不受除数影响。
            scoring.Apply(StableManiaJudgement.Perfect300g, 0);
            Assert.That(scoring.ComboAddition, Is.EqualTo(StableManiaScoring.COMBO_MULTI - 8 / divisor + 2).Within(1e-9));

            scoring.Apply(StableManiaJudgement.Miss, 0);
            Assert.That(scoring.ComboAddition, Is.EqualTo(0), "Miss 落回 -56/除数 后仍夹到 0");
        }

        [Test]
        public void TestScoreIncreaseMultiplierOnlyCountsBonusMods()
        {
            Assert.That(StableManiaScoring.ScoreIncreaseMultiplier(Array.Empty<Mod>()), Is.EqualTo(1));

            Assert.That(StableManiaScoring.ScoreIncreaseMultiplier(new Mod[] { new ManiaModHidden() }), Is.EqualTo(1.06).Within(1e-9));
            Assert.That(StableManiaScoring.ScoreIncreaseMultiplier(new Mod[] { new ManiaModFadeIn() }), Is.EqualTo(1.06).Within(1e-9));
            Assert.That(StableManiaScoring.ScoreIncreaseMultiplier(new Mod[] { new ManiaModFlashlight() }), Is.EqualTo(1.06).Within(1e-9));
            Assert.That(StableManiaScoring.ScoreIncreaseMultiplier(new Mod[] { new ManiaModHardRock() }), Is.EqualTo(1.08).Within(1e-9));
            Assert.That(StableManiaScoring.ScoreIncreaseMultiplier(new Mod[] { new ManiaModDoubleTime() }), Is.EqualTo(1.1).Within(1e-9));
            Assert.That(StableManiaScoring.ScoreIncreaseMultiplier(new Mod[] { new ManiaModNightcore() }), Is.EqualTo(1.1).Within(1e-9));

            Assert.That(
                StableManiaScoring.ScoreIncreaseMultiplier(new Mod[] { new ManiaModHidden(), new ManiaModHardRock(), new ManiaModDoubleTime() }),
                Is.EqualTo(1.06 * 1.08 * 1.1).Within(1e-9));

            // 减益 / 无分 mod 不计入除数（它们只影响成绩乘数）。
            Assert.That(StableManiaScoring.ScoreIncreaseMultiplier(new Mod[] { new ManiaModEasy(), new ManiaModNoFail(), new ManiaModHalfTime() }), Is.EqualTo(1));
        }

        [Test]
        public void TestFinalizeRoundsAndPromotes999999()
        {
            Assert.That(StableManiaScoring.Finalize(999999.4), Is.EqualTo(1000000), "999999 在 stable 里进位成 1000000");
            Assert.That(StableManiaScoring.Finalize(999998.6), Is.EqualTo(1000000));
            Assert.That(StableManiaScoring.Finalize(999998.4), Is.EqualTo(999998));
            Assert.That(StableManiaScoring.Finalize(1000.6), Is.EqualTo(1001));
            Assert.That(StableManiaScoring.Finalize(1000.49), Is.EqualTo(1000));
        }

        private static double nextComboAddition(double comboAddition, StableManiaJudgement judgement, int comboAtJudgement)
        {
            switch (judgement)
            {
                case StableManiaJudgement.Perfect300g:
                    comboAddition += 2;
                    break;

                case StableManiaJudgement.Great300:
                    comboAddition += 1;
                    break;

                case StableManiaJudgement.Good200:
                    comboAddition -= 8;
                    break;

                case StableManiaJudgement.Ok100:
                    comboAddition -= 24;
                    break;

                case StableManiaJudgement.Meh50:
                    comboAddition -= 44;
                    break;

                default:
                    comboAddition = -56;
                    break;
            }

            if (comboAtJudgement != 0 && comboAtJudgement % StableManiaScoring.COMBO_BONUS == 0)
                comboAddition = StableManiaScoring.COMBO_MULTI;

            return Math.Max(0, Math.Min(comboAddition, StableManiaScoring.COMBO_MULTI));
        }
    }
}

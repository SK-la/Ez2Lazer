// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Mania.Tests.EzMania.ReplayJudge;

namespace osu.Game.Rulesets.Mania.Tests
{
    [TestFixture]
    public class ManiaHealthProcessorTest
    {
        [Test]
        public void TestNoDrain()
        {
            var processor = new ManiaHealthProcessor(0);
            processor.ApplyBeatmap(new ManiaBeatmap(new StageDefinition(4))
            {
                HitObjects =
                {
                    new Note { StartTime = 0 },
                    new Note { StartTime = 1000 },
                }
            });

            // No matter what, mania doesn't have passive HP drain.
            Assert.That(processor.DrainRate, Is.Zero);
        }

        [Test]
        public void TestHealthModeFrozenPerInstanceNotShared()
        {
            try
            {
                ReplayJudgeTestConfig.ApplyToGlobalConfig(ReplayJudgeTestConfig.Create(EzEnumHitMode.Lazer, EzEnumHealthMode.IIDX_HD));
                var first = new ManiaHealthProcessor(0);
                first.ApplyEzGameplayEnvironment();

                // 第一局进行中时改设置：已开局的处理器必须保持开局冻结值。
                ReplayJudgeTestConfig.ResetGlobalConfig();

                var second = new ManiaHealthProcessor(0);
                second.ApplyEzGameplayEnvironment();

                Assert.Multiple(() =>
                {
                    Assert.That(first.ActiveHealthMode, Is.EqualTo(EzEnumHealthMode.IIDX_HD));
                    Assert.That(second.ActiveHealthMode, Is.EqualTo(EzEnumHealthMode.Lazer));
                });
            }
            finally
            {
                ReplayJudgeTestConfig.ResetGlobalConfig();
            }
        }

        private static readonly object[][] test_cases =
        [
            // hitobject, starting HP, fail expected after miss
            [new Note(), 0.01, true],
            [new HeadNote(), 0.01, true],
            [new TailNote(), 0.01, true],
            [new HoldNoteBody(), 0, true], // hold note break
            [new HoldNote(), 0, true],
        ];

        [TestCaseSource(nameof(test_cases))]
        public void TestFailAfterMinResult(ManiaHitObject hitObject, double startingHealth, bool failExpected)
        {
            var healthProcessor = new ManiaHealthProcessor(0);
            healthProcessor.ApplyBeatmap(new ManiaBeatmap(new StageDefinition(4))
            {
                HitObjects = { hitObject }
            });
            healthProcessor.Health.Value = startingHealth;

            var result = new JudgementResult(hitObject, hitObject.CreateJudgement());
            result.Type = result.Judgement.MinResult;
            healthProcessor.ApplyResult(result);

            Assert.That(healthProcessor.HasFailed, Is.EqualTo(failExpected));
        }

        [TestCaseSource(nameof(test_cases))]
        public void TestNoFailAfterMaxResult(ManiaHitObject hitObject, double startingHealth, bool _)
        {
            var healthProcessor = new ManiaHealthProcessor(0);
            healthProcessor.ApplyBeatmap(new ManiaBeatmap(new StageDefinition(4))
            {
                HitObjects = { hitObject }
            });
            healthProcessor.Health.Value = startingHealth;

            var result = new JudgementResult(hitObject, hitObject.CreateJudgement());
            result.Type = result.Judgement.MaxResult;
            healthProcessor.ApplyResult(result);

            Assert.That(healthProcessor.HasFailed, Is.False);
        }
    }
}

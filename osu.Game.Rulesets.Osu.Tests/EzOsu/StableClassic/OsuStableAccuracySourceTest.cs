// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Framework.IO.Stores;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.IO;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Scoring;
using osu.Game.Rulesets.Scoring;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Rulesets.Osu.Tests.EzOsu.StableClassic
{
    /// <summary>
    /// stable 准确率槽位在两条判定轨下都必须可用：非 StableClassic 轨按 stable 档位权重统计实际判定流，
    /// tick / 滑条尾 / bonus 不进分子分母，故每个物件恰好贡献一次基础判定。
    /// </summary>
    [TestFixture]
    public class OsuStableAccuracySourceTest
    {
        private static readonly DllResourceStore resources = new DllResourceStore(typeof(OsuStableAccuracySourceTest).Assembly);

        [OneTimeSetUp]
        public void OneTimeSetUp() => GlobalConfigStore.EnsureInitialized();

        [Test]
        public void TestSlotAvailableOnEveryTrack()
        {
            Assert.Multiple(() =>
            {
                Assert.That(createProcessor(EzEnumJudgeMode.Lazer).StableAvailable.Value, Is.True, "Lazer 轨");
                Assert.That(createProcessor(EzEnumJudgeMode.StableClassic).StableAvailable.Value, Is.True, "StableClassic 轨");
            });
        }

        [Test]
        public void TestLazerTrackUsesStableWeights()
        {
            var processor = createProcessor(EzEnumJudgeMode.Lazer);

            apply(processor, new HitCircle(), HitResult.Miss);
            Assert.That(processor.AccuracyClassic.Value, Is.EqualTo(0).Within(1e-9), "圆圈 Miss 占分母但不给分");

            apply(processor, new HitCircle(), HitResult.Great);
            Assert.That(processor.AccuracyClassic.Value, Is.EqualTo(0.5).Within(1e-9), "圆圈 Great 按 300 计");

            // Lazer 轨的滑条主体是 Ignore，tick 与尾判属 tick 系：都不入账，滑条头才是该物件的基础判定。
            apply(processor, new SliderTick(), HitResult.SmallTickHit);
            Assert.That(processor.AccuracyClassic.Value, Is.EqualTo(0.5).Within(1e-9), "tick 不改变准确率");

            apply(processor, new SliderHeadCircle(), HitResult.Ok);
            Assert.That(processor.AccuracyClassic.Value, Is.EqualTo(400.0 / 900).Within(1e-9), "滑条头按 stable 的 100 计");
        }

        [Test]
        public void TestRevertRestoresClassicAccuracy()
        {
            var processor = createProcessor(EzEnumJudgeMode.Lazer);
            JudgementResult miss = apply(processor, new HitCircle(), HitResult.Miss);

            processor.RevertResult(miss);

            Assert.That(processor.AccuracyClassic.Value, Is.EqualTo(1).Within(1e-9), "撤销判定后回到未判定的 100%");
        }

        private static JudgementResult apply(OsuScoreProcessor processor, HitObject hitObject, HitResult result)
        {
            var judgement = new JudgementResult(hitObject, hitObject.CreateJudgement()) { Type = result };

            processor.ApplyResult(judgement);
            return judgement;
        }

        private static OsuScoreProcessor createProcessor(EzEnumJudgeMode judgementTrack)
        {
            using var stream = resources.GetStream("Resources/Testing/Beatmaps/basic.osu");
            Assert.That(stream, Is.Not.Null, "缺少内嵌资源 basic.osu");

            IBeatmap decoded = new LegacyBeatmapDecoder().Decode(new LineBufferedReader(stream!));
            var ruleset = new OsuRuleset();

            IBeatmap playable = new TestWorkingBeatmap(decoded).GetPlayableBeatmap(ruleset.RulesetInfo, Array.Empty<Mod>());

            var processor = new OsuScoreProcessor { JudgementTrackOverride = judgementTrack };
            processor.Mods.Value = Array.Empty<Mod>();
            processor.ApplyBeatmap(playable);

            return processor;
        }
    }
}

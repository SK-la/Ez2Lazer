// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Replays;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Replays;
using osu.Game.Rulesets.Mania.Tests.EzMania.ReplayJudge;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Tests.Beatmaps;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Mania.Tests
{
    /// <summary>
    /// P2-B: offset 落定后 Session HitEvent 变化集成测试。
    /// 验证 OffsetPlusMania 在 Session 判定路径上影响 HitResult（与 Drawable 一致）。
    /// </summary>
    public partial class TestSceneOffsetIntegration : RateAdjustedBeatmapTestScene
    {
        protected override Ruleset CreateRuleset() => new ManiaRuleset();

        private IBeatmap playableBeatmap = null!;
        private Score replayScore = null!;
        private GameplayEnvironment baseEnvironment = null!;

        [TearDown]
        public void TearDown()
        {
            GlobalConfigStore.EzConfig.SetValue(Ez2Setting.OffsetPlusMania, 0.0);
            GlobalConfigStore.EzConfig.SetValue(Ez2Setting.ManiaJudgeMode, EzEnumJudgeMode.Lazer);
            GlobalConfigStore.EzConfig.SetValue(Ez2Setting.ManiaHealthMode, EzEnumHealthMode.Lazer);
        }

        [Test]
        public void TestStatisticsChangeAfterOffsetAdjustment()
        {
            const double late_ms = 64;

            var hitObjects = new List<ManiaHitObject>
            {
                new Note { StartTime = 1000, Column = 0 },
                new Note { StartTime = 2000, Column = 0 },
            };

            var frames = new List<ReplayFrame>
            {
                new ManiaReplayFrame(1000, ManiaAction.Key1),
                new ManiaReplayFrame(1100),
                new ManiaReplayFrame(2000 + late_ms, ManiaAction.Key1),
                new ManiaReplayFrame(2000 + late_ms + 100),
            };

            AddStep("setup base environment", () =>
            {
                baseEnvironment = ReplayJudgeTestConfig.Create(EzEnumJudgeMode.Lazer, EzEnumHealthMode.Lazer);
                ReplayJudgeTestConfig.ApplyToGlobalConfig(baseEnvironment);
            });

            AddStep("create beatmap and score", () => createBeatmapAndScore(hitObjects, frames));

            AddAssert("baseline late tap is Good", () =>
            {
                var lateHit = getLateTapHitEvent(ManiaReplaySession.Run(replayScore.DeepClone(), playableBeatmap, baseEnvironment));
                Assert.That(lateHit.Result, Is.EqualTo(HitResult.Good));
                return true;
            });

            AddStep("set offset to compensate late tap", () =>
            {
                GlobalConfigStore.EzConfig.SetValue(Ez2Setting.OffsetPlusMania, -late_ms);
            });

            AddAssert("offset promotes late tap to Perfect", () =>
            {
                var environmentWithOffset = baseEnvironment with { OffsetPlusMania = -late_ms };
                var lateHit = getLateTapHitEvent(ManiaReplaySession.Run(replayScore.DeepClone(), playableBeatmap, environmentWithOffset));
                Assert.That(lateHit.Result, Is.EqualTo(HitResult.Perfect));
                return true;
            });
        }

        [Test]
        public void TestAccuracyChangeAfterOffsetAdjustment()
        {
            const double late_ms = 80;

            var hitObjects = new List<ManiaHitObject>
            {
                new Note { StartTime = 1000, Column = 0 },
                new Note { StartTime = 2000, Column = 0 },
            };

            var frames = new List<ReplayFrame>
            {
                new ManiaReplayFrame(1000, ManiaAction.Key1),
                new ManiaReplayFrame(1100),
                new ManiaReplayFrame(2000 + late_ms, ManiaAction.Key1),
                new ManiaReplayFrame(2000 + late_ms + 100),
            };

            AddStep("setup environment", () =>
            {
                baseEnvironment = ReplayJudgeTestConfig.Create(EzEnumJudgeMode.Lazer, EzEnumHealthMode.Lazer);
                ReplayJudgeTestConfig.ApplyToGlobalConfig(baseEnvironment);
            });

            AddStep("create beatmap and score", () => createBeatmapAndScore(hitObjects, frames));

            AddAssert("offset improves late tap judgement", () =>
            {
                var baseHit = getLateTapHitEvent(ManiaReplaySession.Run(replayScore.DeepClone(), playableBeatmap, baseEnvironment));
                var offsetHit = getLateTapHitEvent(ManiaReplaySession.Run(
                    replayScore.DeepClone(),
                    playableBeatmap,
                    baseEnvironment with { OffsetPlusMania = -late_ms }));

                Assert.That(baseHit.Result, Is.EqualTo(HitResult.Ok));
                Assert.That(offsetHit.Result, Is.EqualTo(HitResult.Perfect));
                Assert.That(offsetHit.Result, Is.GreaterThan(baseHit.Result));
                return true;
            });
        }

        private void createBeatmapAndScore(List<ManiaHitObject> hitObjects, List<ReplayFrame> frames)
        {
            var ruleset = new ManiaRuleset();
            var beatmap = new TestBeatmap(ruleset.RulesetInfo)
            {
                HitObjects = hitObjects.Cast<HitObject>().ToList(),
            };

            foreach (var obj in beatmap.HitObjects)
                obj.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);

            playableBeatmap = beatmap;
            replayScore = new Score
            {
                ScoreInfo = new ScoreInfo { Ruleset = ruleset.RulesetInfo },
                Replay = new Replay { Frames = frames },
            };
        }

        private static HitEvent getLateTapHitEvent(Score result)
            => result.ScoreInfo.HitEvents.Single(e => e.HitObject.StartTime == 2000);
    }
}

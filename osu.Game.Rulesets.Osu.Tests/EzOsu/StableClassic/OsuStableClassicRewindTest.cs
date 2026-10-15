// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.IO.Stores;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.IO;
using osu.Game.Online.Spectator;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Rulesets.Osu.Scoring;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.Scoring;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Rulesets.Osu.Tests.EzOsu.StableClassic
{
    /// <summary>
    /// 回放后退（seek back / 跳帧）后，classic 累加器必须按目标帧重建。
    /// 否则 <see cref="ScoreProcessor.ResetFromReplayFrame"/> 末尾的 <c>updateScore</c> 会把
    /// 陈旧的 <see cref="StableStdScoring.TotalScore"/> 当成当前总分写回，连击与 classic 准确率同样停在跳帧前的值。
    /// </summary>
    [TestFixture]
    public class OsuStableClassicRewindTest
    {
        private static readonly DllResourceStore resources = new DllResourceStore(typeof(OsuStableClassicRewindTest).Assembly);

        private static readonly HitResult[] result_pattern =
        {
            HitResult.Great, HitResult.Ok, HitResult.Meh, HitResult.Miss,
            HitResult.Great, HitResult.Great, HitResult.Ok, HitResult.Great,
        };

        [OneTimeSetUp]
        public void OneTimeSetUp() => GlobalConfigStore.EnsureInitialized();

        [Test]
        public void TestSeekBackRebuildsClassicAccumulators()
        {
            IBeatmap playable = loadPlayableBeatmap();
            List<HitObject> hitObjects = playable.HitObjects.ToList();
            int cut = Math.Max(1, hitObjects.Count / 2);

            // 目标帧：只判定前 cut 个物件，即为后退的目标状态。
            var target = createProcessor(playable);
            var targetStatistics = new Dictionary<HitResult, int>();
            applyRange(target, hitObjects, 0, cut, targetStatistics);

            // 完整一局。
            var full = createProcessor(playable);
            applyRange(full, hitObjects, 0, hitObjects.Count, new Dictionary<HitResult, int>());

            Assert.That(full.TotalScore.Value, Is.Not.EqualTo(target.TotalScore.Value), "测试前提：目标帧与末帧的总分不同");

            // 判定到末帧后后退到目标帧。
            var rewound = createProcessor(playable);
            applyRange(rewound, hitObjects, 0, hitObjects.Count, new Dictionary<HitResult, int>());
            rewound.ResetFromReplayFrame(createFrame(target, targetStatistics));

            Assert.Multiple(() =>
            {
                Assert.That(rewound.TotalScore.Value, Is.EqualTo(target.TotalScore.Value), "后退到目标帧后的总分");
                Assert.That(rewound.Combo.Value, Is.EqualTo(target.Combo.Value), "后退到目标帧后的连击");
                Assert.That(rewound.HighestCombo.Value, Is.EqualTo(target.HighestCombo.Value), "后退到目标帧后的最大连击");
                Assert.That(rewound.AccuracyClassic.Value, Is.EqualTo(target.AccuracyClassic.Value).Within(1e-9), "后退到目标帧后的 classic 准确率");
                Assert.That(rewound.JudgedHits, Is.EqualTo(target.JudgedHits), "后退到目标帧后的判定数");
            });

            // 从目标帧继续判定，必须与完整一局逐项一致（验证重建出的累加器是完备的，而非仅显示值对齐）。
            applyRange(rewound, hitObjects, cut, hitObjects.Count, new Dictionary<HitResult, int>());

            Assert.Multiple(() =>
            {
                Assert.That(rewound.TotalScore.Value, Is.EqualTo(full.TotalScore.Value), "后退后继续判定到末帧的总分");
                Assert.That(rewound.Combo.Value, Is.EqualTo(full.Combo.Value), "后退后继续判定到末帧的连击");
                Assert.That(rewound.HighestCombo.Value, Is.EqualTo(full.HighestCombo.Value), "后退后继续判定到末帧的最大连击");
                Assert.That(rewound.AccuracyClassic.Value, Is.EqualTo(full.AccuracyClassic.Value).Within(1e-9), "后退后继续判定到末帧的 classic 准确率");
            });
        }

        private static ReplayFrame createFrame(OsuScoreProcessor source, Dictionary<HitResult, int> statistics) => new OsuReplayFrame
        {
            Header = new FrameHeader(
                source.TotalScore.Value,
                source.Accuracy.Value,
                source.Combo.Value,
                source.HighestCombo.Value,
                new Dictionary<HitResult, int>(statistics),
                source.GetScoreProcessorStatistics(),
                DateTimeOffset.Now, [], 0, [])
        };

        private static IBeatmap loadPlayableBeatmap()
        {
            using var stream = resources.GetStream("Resources/Testing/Beatmaps/basic.osu");
            Assert.That(stream, Is.Not.Null, "缺少内嵌资源 basic.osu");

            IBeatmap decoded = new LegacyBeatmapDecoder().Decode(new LineBufferedReader(stream!));
            var ruleset = new OsuRuleset();

            return new TestWorkingBeatmap(decoded).GetPlayableBeatmap(ruleset.RulesetInfo, Array.Empty<Mod>());
        }

        private static OsuScoreProcessor createProcessor(IBeatmap playable)
        {
            var processor = new OsuScoreProcessor { JudgementTrackOverride = EzEnumJudgeMode.StableClassic };
            processor.Mods.Value = Array.Empty<Mod>();
            processor.ApplyBeatmap(playable);

            return processor;
        }

        private static void applyRange(OsuScoreProcessor processor, IReadOnlyList<HitObject> hitObjects, int from, int to, Dictionary<HitResult, int> statistics)
        {
            for (int i = from; i < to; i++)
            {
                HitResult result = result_pattern[i % result_pattern.Length];
                processor.ApplyResult(new JudgementResult(hitObjects[i], hitObjects[i].CreateJudgement()) { Type = result });
                statistics[result] = statistics.GetValueOrDefault(result) + 1;
            }
        }
    }
}

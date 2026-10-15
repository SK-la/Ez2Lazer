// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Online.Spectator;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Replays;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.ReplayJudge
{
    /// <summary>
    /// 回放后退（seek back / 跳帧）后，classic 累加器必须按目标帧重建。
    /// 否则 <see cref="ScoreProcessor.ResetFromReplayFrame"/> 末尾的 <c>updateScore</c> 会把
    /// 陈旧的 <c>StableManiaScoring</c> 总分写回，classic 准确率同样停在跳帧前的值。
    /// </summary>
    [TestFixture]
    public class ManiaStableClassicRewindTest
    {
        private const int note_count = 24;
        private const int total_columns = 4;

        [OneTimeSetUp]
        public void OneTimeSetUp() => GlobalConfigStore.EnsureInitialized();

        [Test]
        public void TestSeekBackRebuildsClassicAccumulators()
        {
            ManiaBeatmap beatmap = createBeatmap();
            List<ManiaHitObject> hitObjects = beatmap.HitObjects.ToList();
            int cut = hitObjects.Count / 2;

            // 目标帧：只判定前 cut 个音符，即为后退的目标状态。
            var target = createProcessor(beatmap);
            var targetStatistics = new Dictionary<HitResult, int>();
            applyRange(target, hitObjects, 0, cut, targetStatistics);

            // 完整一局。
            var full = createProcessor(beatmap);
            applyRange(full, hitObjects, 0, hitObjects.Count, new Dictionary<HitResult, int>());

            Assert.That(full.TotalScore.Value, Is.Not.EqualTo(target.TotalScore.Value), "测试前提：目标帧与末帧的总分不同");

            // 判定到末帧后后退到目标帧。
            var rewound = createProcessor(beatmap);
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

            // 从目标帧继续判定，必须与完整一局一致（验证重建出的累加器是完备的，而非仅显示值对齐）。
            applyRange(rewound, hitObjects, cut, hitObjects.Count, new Dictionary<HitResult, int>());

            Assert.Multiple(() =>
            {
                Assert.That(rewound.TotalScore.Value, Is.EqualTo(full.TotalScore.Value), "后退后继续判定到末帧的总分");
                Assert.That(rewound.Combo.Value, Is.EqualTo(full.Combo.Value), "后退后继续判定到末帧的连击");
                Assert.That(rewound.HighestCombo.Value, Is.EqualTo(full.HighestCombo.Value), "后退后继续判定到末帧的最大连击");
                Assert.That(rewound.AccuracyClassic.Value, Is.EqualTo(full.AccuracyClassic.Value).Within(1e-9), "后退后继续判定到末帧的 classic 准确率");
            });
        }

        private static ManiaBeatmap createBeatmap()
        {
            var beatmap = new ManiaBeatmap(new StageDefinition(total_columns))
            {
                BeatmapInfo = new BeatmapInfo
                {
                    Ruleset = new ManiaRuleset().RulesetInfo,
                    Difficulty = new BeatmapDifficulty { OverallDifficulty = 8 },
                },
            };

            for (int i = 0; i < note_count; i++)
                beatmap.HitObjects.Add(new Note { Column = i % total_columns, StartTime = 1000 + i * 200 });

            return beatmap;
        }

        private static ManiaScoreProcessor createProcessor(ManiaBeatmap beatmap)
        {
            var processor = new ManiaScoreProcessor { JudgeModeOverride = EzEnumJudgeMode.StableClassic };
            processor.Mods.Value = Array.Empty<Mod>();
            processor.ApplyBeatmap(beatmap);

            return processor;
        }

        private static ReplayFrame createFrame(ManiaScoreProcessor source, Dictionary<HitResult, int> statistics) => new ManiaReplayFrame
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

        private static void applyRange(ManiaScoreProcessor processor, IReadOnlyList<ManiaHitObject> hitObjects, int from, int to, Dictionary<HitResult, int> statistics)
        {
            for (int i = from; i < to; i++)
            {
                var result = HitResult.Perfect;
                processor.ApplyResult(new JudgementResult(hitObjects[i], hitObjects[i].CreateJudgement()) { Type = result });
                statistics[result] = statistics.GetValueOrDefault(result) + 1;
            }
        }
    }
}

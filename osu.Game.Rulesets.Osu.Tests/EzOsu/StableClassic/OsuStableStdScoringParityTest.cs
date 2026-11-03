// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using osu.Framework.IO.Stores;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.IO;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.EzOsu.StableClassic;
using osu.Game.Rulesets.Osu.Scoring;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Scoring.Legacy;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Rulesets.Osu.Tests.EzOsu.StableClassic
{
    /// <summary>
    /// std stable classic 双实现对账：EZ 的 <see cref="StableStdScoring"/> 与 ppy 官方
    /// <c>OsuLegacyScoreSimulator</c>（stable 反编译口径）在无 Mod 全 Perfect 下必须给出同一个 stable 总分与最大连击。
    /// </summary>
    /// <remarks>
    /// 转盘 bonus 不入账：stable 用旋转采样估 bonus，lazer 由谱面自带 tick 生成，两边口径本就不同。
    /// 故只对「准确率分 + combo 乘区」，即从 <see cref="ScoreProcessor.MaximumTotalScore"/> 里扣掉 bonus。
    /// 无 Mod 时两边都不含浮点误差（peppy-stars 与 mod 倍率都是整数），断言可以取严格相等。
    /// </remarks>
    [TestFixture]
    public class OsuStableStdScoringParityTest
    {
        private static readonly DllResourceStore resources = new DllResourceStore(typeof(OsuStableStdScoringParityTest).Assembly);

        [TestCase("basic.osu")]
        [TestCase("repeat-slider.osu")]
        [TestCase("uneven-repeat-slider.osu")]
        [TestCase("slider-ticks.osu")]
        [TestCase("slider-ticks-edge-case.osu")]
        [TestCase("multi-segment-slider.osu")]
        [TestCase("slider-paths-edge-case.osu")]
        [TestCase("very-fast-slider.osu")]
        [TestCase("old-stacking.osu")]
        [TestCase("diffcalc-test.osu")]
        [TestCase("801165.osu")]
        [TestCase("1124896.osu")]
        [TestCase("1341554.osu")]
        [TestCase("2593923.osu")]
        [TestCase("OsuAudit-SaikouNoKataomoi.osu")]
        public void TestPerfectRunMatchesLegacySimulator(string beatmapFile)
        {
            string resource = $"Resources/Testing/Beatmaps/{beatmapFile}";

            using var stream = resources.GetStream(resource);

            if (stream == null)
                Assert.Ignore($"缺少内嵌资源：{resource}");

            IBeatmap decoded = new LegacyBeatmapDecoder().Decode(new LineBufferedReader(stream));
            var ruleset = new OsuRuleset();
            var workingBeatmap = new TestWorkingBeatmap(decoded);
            IBeatmap playable = workingBeatmap.GetPlayableBeatmap(ruleset.RulesetInfo, Array.Empty<Mod>());

            LegacyScoreAttributes attributes = ruleset.CreateLegacyScoreSimulator().Simulate(workingBeatmap, playable);

            var scoreProcessor = new OsuScoreProcessor { JudgementTrackOverride = EzEnumJudgeMode.StableClassic };
            scoreProcessor.Mods.Value = Array.Empty<Mod>();
            scoreProcessor.ApplyBeatmap(playable);

            long bonus = scoreProcessor.MaximumStatistics.GetValueOrDefault(HitResult.SmallBonus) * 100
                         + scoreProcessor.MaximumStatistics.GetValueOrDefault(HitResult.LargeBonus) * 1100;
            long expectedComboScore = attributes.AccuracyScore + (long)attributes.ComboScore;

            Assert.Multiple(() =>
            {
                Assert.That(scoreProcessor.MaximumCombo, Is.EqualTo(attributes.MaxCombo), "最大连击");
                Assert.That(scoreProcessor.MaximumTotalScore - bonus, Is.EqualTo(expectedComboScore), "准确率分 + combo 乘区");
            });
        }
    }
}

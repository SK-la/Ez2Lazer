// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Mania.Tests.EzMania.ReplayJudge;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.Scoring
{
    /// <summary>
    /// KPoor 展示门槛必须与判定同源（BMS 系列血量模式 + BmsPoor 开关），不能只看开关。
    /// </summary>
    [TestFixture]
    public class ManiaKPoorDisplayGateTest
    {
        [TearDown]
        public void TearDown() => ReplayJudgeTestConfig.ResetGlobalConfig();

        [TestCase(EzEnumHealthMode.Lazer, true, false)]
        [TestCase(EzEnumHealthMode.Lazer, false, false)]
        [TestCase(EzEnumHealthMode.IIDX_HD, false, false)]
        [TestCase(EzEnumHealthMode.IIDX_HD, true, true)]
        public void TestPoorWindowFollowsHealthModeAndToggle(EzEnumHealthMode healthMode, bool poorToggle, bool expected)
        {
            // hitmode 固定为 BMS 系列，用血量模式做唯一区分。
            ReplayJudgeTestConfig.ApplyToGlobalConfig(ReplayJudgeTestConfig.Create(EzEnumHitMode.IIDX_HD, healthMode, bmsPoorHitResultEnable: poorToggle));

            var hitWindows = new ManiaHitWindows();

            Assert.That(hitWindows.IsHitResultAllowed(HitResult.Poor), Is.EqualTo(expected));
            Assert.That(hitWindows.GetAllAvailableWindows().Any(w => w.result == HitResult.Poor), Is.EqualTo(expected));
        }

        [TestCase(EzEnumHealthMode.Lazer, true, false)]
        [TestCase(EzEnumHealthMode.IIDX_HD, false, false)]
        [TestCase(EzEnumHealthMode.IIDX_HD, true, true)]
        public void TestWedgeMetricsFollowHealthMode(EzEnumHealthMode healthMode, bool poorToggle, bool expectedKPoor)
        {
            ReplayJudgeTestConfig.ApplyToGlobalConfig(ReplayJudgeTestConfig.Create(EzEnumHitMode.IIDX_HD, healthMode, bmsPoorHitResultEnable: poorToggle));

            var ruleset = new ManiaRuleset();
            var beatmapInfo = new BeatmapInfo
            {
                Ruleset = ruleset.RulesetInfo,
                Difficulty = new BeatmapDifficulty { OverallDifficulty = 8, CircleSize = 4 },
                BPM = 120,
            };

            string[] names = ruleset.GetBeatmapAttributesForDisplay(beatmapInfo, Array.Empty<Mod>())
                                    .SelectMany(a => a.AdditionalMetrics)
                                    .Select(m => m.Name.ToString())
                                    .ToArray();

            // BMS 系列命名下 Miss 也叫 "Poor"，KPoor 的名字是 "KPoor"。
            Assert.That(names.Any(n => n.Contains("KPOOR", StringComparison.OrdinalIgnoreCase)), Is.EqualTo(expectedKPoor));
        }

        [Test]
        public void TestBakedWindowsFollowFrozenEnvironment()
        {
            // 本机当前设置：非 BMS 血量模式（+ 开关开），只看设置会得出「KPoor 不可用」。
            ReplayJudgeTestConfig.ApplyToGlobalConfig(ReplayJudgeTestConfig.Create(EzEnumHitMode.Lazer, EzEnumHealthMode.Lazer, bmsPoorHitResultEnable: true));

            var beatmap = createBeatmap();

            var environment = ReplayJudgeTestConfig.Create(EzEnumHitMode.IIDX_HD, EzEnumHealthMode.IIDX_HD, bmsPoorHitResultEnable: true);
            ManiaWindowBaker.AlignForLive(beatmap, environment);

            var windows = (ManiaHitWindows)beatmap.HitObjects[0].HitWindows;

            Assert.That(windows.PoorEnabled, Is.True, "当局环境为 BMS 血量模式 + 开关开，窗口应可用 KPoor");

            // 局内改设置不影响已烘焙的窗口（判定用冻结值）。
            ReplayJudgeTestConfig.ResetGlobalConfig();

            Assert.That(windows.PoorEnabled, Is.True);
            Assert.That(windows.IsHitResultAllowed(HitResult.Poor), Is.True);
        }

        [Test]
        public void TestSimulationBakeKeepsPoorOnCurrentSettings()
        {
            ReplayJudgeTestConfig.ApplyToGlobalConfig(ReplayJudgeTestConfig.Create(EzEnumHitMode.Lazer, EzEnumHealthMode.Lazer, bmsPoorHitResultEnable: true));

            var beatmap = createBeatmap();

            ManiaWindowBaker.Align(beatmap, EzEnumHitMode.Lazer);

            var windows = (ManiaHitWindows)beatmap.HitObjects[0].HitWindows;

            Assert.That(windows.PoorEnabled, Is.False, "没有当局环境时按本机当前设置解析（Lazer 血量模式无 KPoor）");
        }

        private static TestBeatmap createBeatmap()
        {
            var beatmap = new TestBeatmap(new ManiaRuleset().RulesetInfo, withHitObjects: false);
            beatmap.HitObjects.Add(new Note { StartTime = 1000, Column = 0 });

            foreach (var hitObject in beatmap.HitObjects)
                hitObject.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);

            return beatmap;
        }
    }
}

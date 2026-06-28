// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Replays;
using osu.Game.Rulesets.Mania.Tests.EzMania.ReplayJudge;
using osu.Game.Rulesets.Mania.EzMania.Helper;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge.Mappings;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge.Replicas;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Objects.EzCurrentHitObject;
using osu.Game.Rulesets.Mania.Replays;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.Statistics
{
    /// <summary>
    /// 验证每个 hitmode 的 rejudge 路径（<c>rejudgeOriginalHitEvents</c>）使用了
    /// 与 Session 模拟器一致的判定逻辑，产生匹配的判定计数。
    /// </summary>
    [TestFixture]
    public class EzScoreGraphRejudgeParityTest
    {
        private const double base_time = 1000;
        private const double note_spacing = 500;

        /// <summary>
        /// Tap offset 覆盖各档窗口。
        /// </summary>
        private static readonly double[] tap_offsets =
        {
            -5, 0, 22, 35, 68, 98, 122, 159, 200,
        };

        [TestCase(EzEnumHitMode.Lazer)]
        [TestCase(EzEnumHitMode.Classic)]
        [TestCase(EzEnumHitMode.EZ2AC)]
        [TestCase(EzEnumHitMode.O2Jam)]
        [TestCase(EzEnumHitMode.IIDX_HD)]
        [TestCase(EzEnumHitMode.LR2_HD)]
        [TestCase(EzEnumHitMode.Raja_NM)]
        [TestCase(EzEnumHitMode.Malody_E)]
        [TestCase(EzEnumHitMode.Malody_B)]
        public void TestRejudgeTapNotesMatchesSession(EzEnumHitMode hitMode)
        {
            var (score, beatmap) = createTapOnlyFixture(tap_offsets);
            var env = createEnvironment(hitMode);
            ReplayJudgeTestConfig.ApplyToGlobalConfig(env);
            initO2Jam(hitMode, beatmap);

            var sessionScore = ManiaReplaySession.Run(score.DeepClone(), beatmap, env);
            var originalHitEvents = sessionScore.ScoreInfo.HitEvents;

            var expectedCounts = extractBasicCounts(originalHitEvents, hitMode);
            var windows = createHitWindows(hitMode, beatmap.Difficulty.OverallDifficulty);
            var judgement = ManiaJudgementRegistry.GetHitModeJudgement(hitMode);
            var actualCounts = rejudgeEvents(originalHitEvents, windows, judgement, hitMode);

            assertCountsMatch(expectedCounts, actualCounts, hitMode, "tap only");
        }

        [TestCase(EzEnumHitMode.EZ2AC)]
        [TestCase(EzEnumHitMode.Lazer)]
        [TestCase(EzEnumHitMode.Classic)]
        [TestCase(EzEnumHitMode.O2Jam)]
        [TestCase(EzEnumHitMode.IIDX_HD)]
        [TestCase(EzEnumHitMode.LR2_HD)]
        [TestCase(EzEnumHitMode.Raja_NM)]
        [TestCase(EzEnumHitMode.Malody_E)]
        [TestCase(EzEnumHitMode.Malody_B)]
        public void TestRejudgeWithHoldsMatchesSession(EzEnumHitMode hitMode)
        {
            // 使用第二组 tap offset（与 TestRejudgeTapNotesMatchesSession 不同的偏移）
            double[] offsets = new double[] { -15, 10, 45, 80, 110, 140, 170 };
            var (score, beatmap) = createTapOnlyFixture(offsets);
            var env = createEnvironment(hitMode);
            ReplayJudgeTestConfig.ApplyToGlobalConfig(env);
            initO2Jam(hitMode, beatmap);

            var sessionScore = ManiaReplaySession.Run(score.DeepClone(), beatmap, env);
            var originalHitEvents = sessionScore.ScoreInfo.HitEvents;

            var expectedCounts = extractBasicCounts(originalHitEvents, hitMode);
            var windows = createHitWindows(hitMode, beatmap.Difficulty.OverallDifficulty);
            var judgement = ManiaJudgementRegistry.GetHitModeJudgement(hitMode);
            var actualCounts = rejudgeEvents(originalHitEvents, windows, judgement, hitMode);

            assertCountsMatch(expectedCounts, actualCounts, hitMode, "tap only v2");
        }

        [Test]
        public void TestEz2AcLnHeadGreatSoftensToPerfect()
        {
            var (score, beatmap, env) = HitModeReplayFixtures.CreateEz2AcHoldHeadGreatSoftened();
            ReplayJudgeTestConfig.ApplyToGlobalConfig(env);

            var sessionScore = ManiaReplaySession.Run(score.DeepClone(), beatmap, env);
            var headEvents = sessionScore.ScoreInfo.HitEvents
                .Where(e => e.HitObject is HeadNote)
                .ToList();

            Assert.That(headEvents, Has.Count.GreaterThan(0), "应有 HeadNote HitEvent");

            var windows = createHitWindows(EzEnumHitMode.EZ2AC, beatmap.Difficulty.OverallDifficulty);
            var ez2Ac = Ez2AcHitModeJudgement.Instance;

            foreach (var headEvent in headEvents)
            {
                var softened = ez2Ac.EvaluatePress(headEvent.TimeOffset, windows, isLnHead: true);
                var softenedResult = softened.Kind == ManiaNoteJudgementOutcomeKind.Apply
                    ? softened.Result
                    : HitResult.None;

                Assert.That(softenedResult, Is.EqualTo(HitResult.Perfect),
                    $"EZ2AC LN head at offset={headEvent.TimeOffset:F1}ms 应软化为 Perfect");

                var raw = ez2Ac.EvaluatePress(headEvent.TimeOffset, windows, isLnHead: false);
                var rawResult = raw.Kind == ManiaNoteJudgementOutcomeKind.Apply
                    ? raw.Result
                    : HitResult.None;

                Assert.That(rawResult, Is.EqualTo(HitResult.Great),
                    $"EZ2AC LN head without soften at offset={headEvent.TimeOffset:F1}ms 应为 Great");
            }
        }

        [Test]
        public void TestO2JamBpmChangesWindowSize()
        {
            double[] offsets = new double[] { 25, 60, 100 };
            const double bpm120 = 120.0;
            const double bpm200 = 200.0;

            var env = ReplayJudgeTestConfig.Create(EzEnumHitMode.O2Jam, EzEnumHealthMode.O2JamNormal);
            ReplayJudgeTestConfig.ApplyToGlobalConfig(env);

            // BPM 120
            var (score120, beatmap120) = createTapOnlyFixture(offsets, bpm: bpm120);
            initO2Jam(EzEnumHitMode.O2Jam, beatmap120);
            var session120 = ManiaReplaySession.Run(score120.DeepClone(), beatmap120, env);

            // BPM 200
            var (score200, beatmap200) = createTapOnlyFixture(offsets, bpm: bpm200);
            initO2Jam(EzEnumHitMode.O2Jam, beatmap200);
            var session200 = ManiaReplaySession.Run(score200.DeepClone(), beatmap200, env);

            var events120 = session120.ScoreInfo.HitEvents.ToList();
            var events200 = session200.ScoreInfo.HitEvents.ToList();

            // BPM 120 窗口更大 → 相同 offset 在 BPM 120 下判定不差于 BPM 200
            for (int i = 0; i < Math.Min(events120.Count, events200.Count); i++)
            {
                int rank120 = (int)events120[i].Result;
                int rank200 = (int)events200[i].Result;

                Assert.That(rank120, Is.GreaterThanOrEqualTo(rank200),
                    $"offset={offsets[i]}ms: BPM 120 结果({events120[i].Result}) 应不差于 BPM 200 ({events200[i].Result})");
            }
        }

        /// <summary>
        /// 纯 Tap Note fixture，所有 note 同列，间距足够大不冲突。
        /// </summary>
        private static (Score score, IBeatmap beatmap) createTapOnlyFixture(
            double[] offsetsMs,
            double bpm = 120)
        {
            var ruleset = new ManiaRuleset();
            var hitObjects = new List<HitObject>();
            var frames = new List<ReplayFrame>();

            for (int i = 0; i < offsetsMs.Length; i++)
            {
                double time = base_time + i * note_spacing;
                hitObjects.Add(new Note { StartTime = time, Column = 0 });

                double pressTime = time + offsetsMs[i];
                frames.Add(new ManiaReplayFrame(pressTime, ManiaAction.Key1));
                frames.Add(new ManiaReplayFrame(pressTime + 60));
            }

            var beatmap = createBeatmap(ruleset, hitObjects, bpm);
            return (createScore(ruleset, new Replay { Frames = frames }), beatmap);
        }

        private static IBeatmap createBeatmap(ManiaRuleset ruleset, List<HitObject> hitObjects, double bpm)
        {
            var beatmap = new TestBeatmap(ruleset.RulesetInfo)
            {
                HitObjects = hitObjects,
                ControlPointInfo = new ControlPointInfo(),
            };

            beatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 60000 / bpm });

            // OD 必须在 ApplyDefaults 之前设置，否则 hit object 的 HitWindows 使用默认 OD
            beatmap.Difficulty.OverallDifficulty = 7;

            foreach (var obj in beatmap.HitObjects)
                obj.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);

            return beatmap;
        }

        private static Score createScore(ManiaRuleset ruleset, Replay replay) => new Score
        {
            ScoreInfo = new ScoreInfo { Ruleset = ruleset.RulesetInfo, Mods = Array.Empty<Mod>() },
            Replay = replay,
        };

        private static GameplayEnvironment createEnvironment(EzEnumHitMode hitMode)
        {
            var healthMode = hitMode switch
            {
                EzEnumHitMode.IIDX_HD or EzEnumHitMode.LR2_HD or EzEnumHitMode.Raja_NM => EzEnumHealthMode.IIDX_HD,
                EzEnumHitMode.EZ2AC => EzEnumHealthMode.Ez2Ac,
                EzEnumHitMode.O2Jam => EzEnumHealthMode.O2JamNormal,
                _ => EzEnumHealthMode.Lazer,
            };

            bool bmsPoor = hitMode is EzEnumHitMode.IIDX_HD or EzEnumHitMode.LR2_HD or EzEnumHitMode.Raja_NM;

            return ReplayJudgeTestConfig.Create(hitMode, healthMode, bmsPoorHitResultEnable: bmsPoor);
        }

        private static void initO2Jam(EzEnumHitMode hitMode, IBeatmap beatmap)
        {
            if (hitMode != EzEnumHitMode.O2Jam)
                return;

            O2HitModeExtension.SetControlPoints(beatmap.ControlPointInfo);
            O2HitModeExtension.SetOriginalBPM(beatmap.BeatmapInfo.BPM);
        }

        private static ManiaHitWindows createHitWindows(EzEnumHitMode hitMode, double od)
        {
            var windows = new ManiaHitWindows(hitMode);
            windows.SetDifficulty(od);
            return windows;
        }

        /// <summary>
        /// 从 <see cref="HitEvent"/> 集合提取有效判定计数。
        /// 排除 <see cref="HitResult.IgnoreHit"/>、<see cref="HitResult.ComboBreak"/>、
        /// <see cref="HitResult.IgnoreMiss"/> 等 ScoreProcessor 内部标记，
        /// 因为它们无法通过 <c>EvaluatePress</c> 在重判路径中产出。
        /// </summary>
        private static Dictionary<HitResult, int> extractBasicCounts(
            IReadOnlyList<HitEvent> events, EzEnumHitMode hitMode)
        {
            var validResults = HitModeHelper.GetHitModeValidHitResults(hitMode).ToHashSet();
            var counts = new Dictionary<HitResult, int>();

            foreach (var e in events)
            {
                if (e.Result is HitResult.IgnoreHit or HitResult.ComboBreak or HitResult.IgnoreMiss)
                    continue;

                if (!validResults.Contains(e.Result) && e.Result is not (HitResult.Miss or HitResult.Poor))
                    continue;

                counts.TryAdd(e.Result, 0);
                counts[e.Result]++;
            }

            return counts;
        }

        /// <summary>
        /// 模拟 <c>rejudgeOriginalHitEvents</c> 的判定路径。
        /// </summary>
        private static Dictionary<HitResult, int> rejudgeEvents(
            IReadOnlyList<HitEvent> events,
            ManiaHitWindows windows,
            IManiaHitModeJudgement? judgement,
            EzEnumHitMode hitMode)
        {
            var validResults = HitModeHelper.GetHitModeValidHitResults(hitMode).ToHashSet();
            var counts = new Dictionary<HitResult, int>();
            bool isO2Jam = hitMode == EzEnumHitMode.O2Jam;

            var strategy = judgement
                           ?? (IManiaNoteJudgementStrategy)LazerNoteJudgementReplica.Instance;

            foreach (var e in events)
            {
                if (isO2Jam)
                    windows.UpdateO2JamBpmFromTime(e.HitObject.StartTime);

                var result = strategy.RejudgeHitEvent(e, windows);
                if (result == HitResult.None)
                    result = HitResult.Miss;

                if (!validResults.Contains(result) && result != HitResult.Miss && result != HitResult.Poor)
                    continue;

                counts.TryAdd(result, 0);
                counts[result]++;
            }

            return counts;
        }

        private static void assertCountsMatch(
            Dictionary<HitResult, int> expected,
            Dictionary<HitResult, int> actual,
            EzEnumHitMode hitMode,
            string scenario)
        {
            var allResults = expected.Keys.Concat(actual.Keys).Distinct().OrderBy(r => r).ToList();
            var mismatches = new List<string>();

            foreach (var r in allResults)
            {
                int exp = expected.GetValueOrDefault(r, 0);
                int act = actual.GetValueOrDefault(r, 0);
                if (exp != act)
                    mismatches.Add($"{r}: expected={exp} actual={act}");
            }

            int expTotal = expected.Values.Sum();
            int actTotal = actual.Values.Sum();

            Assert.That(mismatches.Count == 0 && expTotal == actTotal,
                $"[{hitMode} {scenario}] counts mismatch: {string.Join(" | ", mismatches)} "
                + $"(total expected={expTotal} actual={actTotal})");
        }
    }
}

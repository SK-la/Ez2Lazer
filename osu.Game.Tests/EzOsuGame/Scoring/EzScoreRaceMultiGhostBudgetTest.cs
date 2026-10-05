// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Replays;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Catch;
using osu.Game.Rulesets.Catch.Objects;
using osu.Game.Rulesets.Catch.Replays;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Replays;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.Taiko;
using osu.Game.Rulesets.Taiko.Objects;
using osu.Game.Rulesets.Taiko.Replays;
using osu.Game.Scoring;
using osu.Game.Tests.Beatmaps;
using osuTK;

namespace osu.Game.Tests.EzOsuGame.Scoring
{
    /// <summary>
    /// 四模式 Session 接线 + MaxEntries 契约；cold/warm 软预算为 <b>non-gating</b> 附属
    ///（P6 关闭条件见 REGISTRY：TryBuild / AttachBeatmaps / EzPlayableBeatmapCache）。
    /// </summary>
    [TestFixture]
    public class EzScoreRaceMultiGhostBudgetTest
    {
        private const int ghost_count = 10;

        /// <summary>单模式 10× 微型谱 cold TimelineDirect 软上限（ms）；non-gating。</summary>
        private const int cold_budget_ms = 5000;

        /// <summary>10× 48-circle Osu denser 冷路径软上限（ms）；non-gating。</summary>
        private const int dense_osu_cold_budget_ms = 8000;

        [OneTimeSetUp]
        public void OneTimeSetUp() => GlobalConfigStore.EnsureInitialized();

        [Test]
        public void MaxEntriesBoundsMatchHudContract()
        {
            var service = new EzScoreRaceService();

            Assert.That(service.MaxEntries.MinValue, Is.EqualTo(1));
            Assert.That(service.MaxEntries.MaxValue, Is.EqualTo(ghost_count));
            Assert.That(service.MaxEntries.Default, Is.EqualTo(5));
        }

        [TestCase(typeof(OsuRuleset), EzScoreRaceGhostTimelineMode.OsuSession)]
        [TestCase(typeof(ManiaRuleset), EzScoreRaceGhostTimelineMode.ManiaSession)]
        [TestCase(typeof(TaikoRuleset), EzScoreRaceGhostTimelineMode.TaikoSession)]
        [TestCase(typeof(CatchRuleset), EzScoreRaceGhostTimelineMode.CatchSession)]
        public void CreateEzReplaySessionWiredForRace(Type rulesetType, EzScoreRaceGhostTimelineMode expectedMode)
        {
            var ruleset = (Ruleset)Activator.CreateInstance(rulesetType)!;

            Assert.That(EzScoreRaceRulesetSupport.SupportsGhostRace(ruleset.RulesetInfo), Is.True);
            Assert.That(EzScoreRaceRulesetSupport.GetGhostTimelineMode(ruleset.RulesetInfo), Is.EqualTo(expectedMode));
            Assert.That(ruleset.CreateEzReplaySession(), Is.Not.Null);
        }

        [TestCase(typeof(OsuRuleset))]
        [TestCase(typeof(ManiaRuleset))]
        [TestCase(typeof(TaikoRuleset))]
        [TestCase(typeof(CatchRuleset))]
        public void TimelineDirectMatchesRunAsyncOnTinyFixture(Type rulesetType)
        {
            var ruleset = (Ruleset)Activator.CreateInstance(rulesetType)!;
            var session = ruleset.CreateEzReplaySession()!;
            var (score, beatmap) = createTinyFixture(ruleset);

            var run = session.RunAsync(score.DeepClone(), beatmap, ReplayRunPurpose.ForLive).GetAwaiter().GetResult();
            var timeline = session.RunTimelineDirect(score.DeepClone(), beatmap, ReplayRunPurpose.ForLive);
            var final = timeline.QueryAtTime(double.MaxValue);

            Assert.That(timeline.FinalTotalScore, Is.EqualTo(run.ScoreInfo.TotalScore));
            Assert.That(final.HighestCombo, Is.EqualTo(run.ScoreInfo.MaxCombo));
            Assert.That(final.Accuracy, Is.EqualTo(run.ScoreInfo.Accuracy).Within(1e-6));
            Assert.That(timeline.FinalTotalScore, Is.GreaterThan(0));
        }

        [TestCase(typeof(OsuRuleset))]
        [TestCase(typeof(ManiaRuleset))]
        [TestCase(typeof(TaikoRuleset))]
        [TestCase(typeof(CatchRuleset))]
        public void TenGhostTimelineDirectStaysWithinColdBudget(Type rulesetType)
        {
            var ruleset = (Ruleset)Activator.CreateInstance(rulesetType)!;
            var session = ruleset.CreateEzReplaySession()!;
            var (template, beatmap) = createTinyFixture(ruleset);

            // warmup JIT
            session.RunTimelineDirect(template.DeepClone(), beatmap, ReplayRunPurpose.ForLive);

            var sw = Stopwatch.StartNew();

            for (int i = 0; i < ghost_count; i++)
            {
                var score = template.DeepClone();
                score.ScoreInfo.ID = Guid.NewGuid();
                score.ScoreInfo.Hash = $"p6-{rulesetType.Name}-{i}";

                var timeline = session.RunTimelineDirect(score, beatmap, ReplayRunPurpose.ForLive);
                Assert.That(timeline.FinalTotalScore, Is.GreaterThan(0));
            }

            sw.Stop();
            Assert.That(sw.ElapsedMilliseconds, Is.LessThan(cold_budget_ms),
                $"{rulesetType.Name}: {ghost_count}× TimelineDirect took {sw.ElapsedMilliseconds}ms (budget {cold_budget_ms}ms)");
        }

        [Test]
        public void SessionTimelineCacheWarmPathIsMuchFasterThanCold()
        {
            var ruleset = new OsuRuleset();
            var session = ruleset.CreateEzReplaySession()!;
            var (template, beatmap) = createTinyFixture(ruleset);
            var cache = EzScoreTimelineBuilder.CreateSessionCache();

            var coldKeys = new List<string>(ghost_count);
            var coldTimelines = new List<EzScoreTimeline>(ghost_count);

            session.RunTimelineDirect(template.DeepClone(), beatmap, ReplayRunPurpose.ForLive);

            var coldSw = Stopwatch.StartNew();

            for (int i = 0; i < ghost_count; i++)
            {
                var score = template.DeepClone();
                score.ScoreInfo.ID = Guid.NewGuid();
                score.ScoreInfo.Hash = $"p6-cache-{i}";
                string key = $"hash:{score.ScoreInfo.Hash}|osu";

                var timeline = session.RunTimelineDirect(score, beatmap, ReplayRunPurpose.ForLive);
                cache.Store(key, timeline);
                coldKeys.Add(key);
                coldTimelines.Add(timeline);
            }

            coldSw.Stop();

            var warmSw = Stopwatch.StartNew();

            for (int i = 0; i < ghost_count; i++)
            {
                Assert.That(cache.TryGet(coldKeys[i], out var cached), Is.True);
                Assert.That(cached!.FinalTotalScore, Is.EqualTo(coldTimelines[i].FinalTotalScore));
            }

            warmSw.Stop();

            Assert.That(warmSw.ElapsedMilliseconds, Is.LessThan(Math.Max(50, coldSw.ElapsedMilliseconds / 10)),
                $"cache warm {warmSw.ElapsedMilliseconds}ms vs cold {coldSw.ElapsedMilliseconds}ms");
        }

        [Test]
        public void TenDenseOsuGhostsStayWithinRaceColdBudget()
        {
            var ruleset = new OsuRuleset();
            var session = ruleset.CreateEzReplaySession()!;
            var (template, beatmap) = createOsuDenseCircles(circle_count: 48);

            session.RunTimelineDirect(template.DeepClone(), beatmap, ReplayRunPurpose.ForLive);

            var sw = Stopwatch.StartNew();

            for (int i = 0; i < ghost_count; i++)
            {
                var score = template.DeepClone();
                score.ScoreInfo.ID = Guid.NewGuid();
                score.ScoreInfo.Hash = $"p6-dense-{i}";

                var timeline = session.RunTimelineDirect(score, beatmap, ReplayRunPurpose.ForLive);
                Assert.That(timeline.FinalTotalScore, Is.GreaterThan(0));
            }

            sw.Stop();
            Assert.That(sw.ElapsedMilliseconds, Is.LessThan(dense_osu_cold_budget_ms),
                $"10×48-circle Osu TimelineDirect took {sw.ElapsedMilliseconds}ms (budget {dense_osu_cold_budget_ms}ms)");
        }

        private static (Score score, IBeatmap beatmap) createTinyFixture(Ruleset ruleset) => ruleset switch
        {
            OsuRuleset => createOsuTwoCircle(),
            ManiaRuleset => createManiaTwoNote(),
            TaikoRuleset => createTaikoTwoHit(),
            CatchRuleset => createCatchTwoFruit(),
            _ => throw new ArgumentOutOfRangeException(nameof(ruleset)),
        };

        private static (Score score, IBeatmap beatmap) createOsuTwoCircle() => createOsuDenseCircles(2);

        private static (Score score, IBeatmap beatmap) createOsuDenseCircles(int circle_count)
        {
            var ruleset = new OsuRuleset();
            var hitObjects = new List<HitObject>(circle_count);

            for (int i = 0; i < circle_count; i++)
                hitObjects.Add(new HitCircle { StartTime = 1000 + i * 120, Position = new Vector2(200 + (i % 8) * 20, 160 + (i % 5) * 16) });

            var testBeatmap = new TestBeatmap(ruleset.RulesetInfo) { HitObjects = hitObjects };
            var beatmap = prepareConverted(ruleset, testBeatmap);

            var frames = new List<ReplayFrame>(circle_count * 2);

            foreach (var obj in beatmap.HitObjects.OfType<HitCircle>())
            {
                frames.Add(new OsuReplayFrame(obj.StartTime, obj.StackedPosition, OsuAction.LeftButton));
                frames.Add(new OsuReplayFrame(obj.StartTime + 40, obj.StackedPosition));
            }

            return (createScore(ruleset, beatmap, new Replay { Frames = frames }), beatmap);
        }

        private static (Score score, IBeatmap beatmap) createManiaTwoNote()
        {
            var ruleset = new ManiaRuleset();
            var beatmap = new TestBeatmap(ruleset.RulesetInfo)
            {
                HitObjects = new List<HitObject>
                {
                    new Note { StartTime = 1000, Column = 0 },
                    new Note { StartTime = 2000, Column = 0 },
                },
                ControlPointInfo = new ControlPointInfo(),
            };

            foreach (var obj in beatmap.HitObjects)
                obj.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);

            var replay = new Replay
            {
                Frames = new List<ReplayFrame>
                {
                    new ManiaReplayFrame(1000, ManiaAction.Key1),
                    new ManiaReplayFrame(1100),
                    new ManiaReplayFrame(2000, ManiaAction.Key1),
                    new ManiaReplayFrame(2100),
                },
            };

            return (createScore(ruleset, beatmap, replay), beatmap);
        }

        private static (Score score, IBeatmap beatmap) createTaikoTwoHit()
        {
            var ruleset = new TaikoRuleset();
            var testBeatmap = new TestBeatmap(ruleset.RulesetInfo)
            {
                HitObjects = new List<HitObject>
                {
                    new Hit { StartTime = 1000, Type = HitType.Centre },
                    new Hit { StartTime = 2000, Type = HitType.Centre },
                },
            };

            var beatmap = prepareConverted(ruleset, testBeatmap);

            var replay = new Replay
            {
                Frames = new List<ReplayFrame>
                {
                    new TaikoReplayFrame(1000, TaikoAction.LeftCentre),
                    new TaikoReplayFrame(1100),
                    new TaikoReplayFrame(2000, TaikoAction.LeftCentre),
                    new TaikoReplayFrame(2100),
                },
            };

            return (createScore(ruleset, beatmap, replay), beatmap);
        }

        private static (Score score, IBeatmap beatmap) createCatchTwoFruit()
        {
            var ruleset = new CatchRuleset();
            var testBeatmap = new TestBeatmap(ruleset.RulesetInfo)
            {
                HitObjects = new List<HitObject>
                {
                    new Fruit { StartTime = 1000, X = 200 },
                    new Fruit { StartTime = 2000, X = 200 },
                },
            };

            var beatmap = prepareConverted(ruleset, testBeatmap);

            var replay = new Replay
            {
                Frames = new List<ReplayFrame>
                {
                    new CatchReplayFrame(999, 200),
                    new CatchReplayFrame(1000, 200),
                    new CatchReplayFrame(1999, 200),
                    new CatchReplayFrame(2000, 200),
                },
            };

            return (createScore(ruleset, beatmap, replay), beatmap);
        }

        private static IBeatmap prepareConverted(Ruleset ruleset, TestBeatmap testBeatmap)
        {
            var beatmap = ruleset.CreateBeatmapConverter(testBeatmap).Convert();
            var processor = ruleset.CreateBeatmapProcessor(beatmap);
            processor?.PreProcess();

            foreach (var obj in beatmap.HitObjects)
                obj.ApplyDefaults(beatmap.ControlPointInfo, beatmap.BeatmapInfo.Difficulty);

            processor?.PostProcess();
            return beatmap;
        }

        private static Score createScore(Ruleset ruleset, IBeatmap beatmap, Replay replay) => new Score
        {
            ScoreInfo = new ScoreInfo
            {
                Ruleset = ruleset.RulesetInfo,
                BeatmapInfo = beatmap.BeatmapInfo,
                ID = Guid.NewGuid(),
                Hash = Guid.NewGuid().ToString("N"),
            },
            Replay = replay,
        };
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
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
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Taiko;
using osu.Game.Rulesets.Taiko.Objects;
using osu.Game.Rulesets.Taiko.Replays;
using osu.Game.Scoring;
using osu.Game.Screens.Play.Leaderboards;
using osu.Game.Tests.Beatmaps;
using osuTK;

namespace osu.Game.Tests.EzOsuGame.Scoring
{
    /// <summary>
    /// Race 时间线 / 排行榜显示必须吃到 ScoreProcessor 的 mod 分数系数
    ///（NoFail 等 flat 系数；Easy/HR 会改难度窗，不适合做纯比例门禁）。
    /// </summary>
    [TestFixture]
    public class EzScoreRaceScoreMultiplierTest
    {
        [OneTimeSetUp]
        public void OneTimeSetUp() => GlobalConfigStore.EnsureInitialized();

        [TestCase(typeof(OsuRuleset))]
        [TestCase(typeof(ManiaRuleset))]
        [TestCase(typeof(TaikoRuleset))]
        [TestCase(typeof(CatchRuleset))]
        public void TimelineDirectAppliesNoFailScoreMultiplier(Type rulesetType)
        {
            var ruleset = (Ruleset)Activator.CreateInstance(rulesetType)!;
            var session = ruleset.CreateEzReplaySession()!;
            var (template, beatmap) = createTinyFixture(ruleset);
            var noFail = ruleset.CreateMod<ModNoFail>();
            Assert.That(noFail, Is.Not.Null);

            double expectedMultiplier = ruleset
                                        .CreateScoreMultiplierCalculator(new ScoreMultiplierContext(beatmap.Difficulty))
                                        .CalculateFor(new[] { noFail! });
            Assert.That(expectedMultiplier, Is.EqualTo(0.5).Within(1e-9));

            var nmScore = template.DeepClone();
            var nfScore = template.DeepClone();
            nfScore.ScoreInfo.Mods = new Mod[] { noFail! };
            nfScore.ScoreInfo.ID = Guid.NewGuid();
            nfScore.ScoreInfo.Hash = $"nf-{rulesetType.Name}";

            var nmTimeline = session.RunTimelineDirect(nmScore, beatmap, ReplayRunPurpose.ForLive);
            var nfTimeline = session.RunTimelineDirect(nfScore, beatmap, ReplayRunPurpose.ForLive);
            var nfRun = session.RunAsync(nfScore.DeepClone(), beatmap, ReplayRunPurpose.ForLive).GetAwaiter().GetResult();

            Assert.That(nmTimeline.FinalTotalScore, Is.GreaterThan(0));
            Assert.That(nfTimeline.FinalTotalScore, Is.GreaterThan(0));
            Assert.That(nfTimeline.FinalTotalScore, Is.EqualTo(nfRun.ScoreInfo.TotalScore));

            // TotalScore = Round(WithoutMods * multiplier)；同判定下应约为 NM 的一半。
            long expectedNf = (long)Math.Round(nmTimeline.FinalTotalScore * expectedMultiplier);
            Assert.That(nfTimeline.FinalTotalScore, Is.EqualTo(expectedNf).Within(1));

            assertClassicDisplayScalesWithMultiplier(nmTimeline.FinalTotalScore, nfTimeline.FinalTotalScore, nfRun.ScoreInfo, expectedMultiplier);
        }

        private static void assertClassicDisplayScalesWithMultiplier(long nmStandardised, long nfStandardised, ScoreInfo scoreInfoWithMaxStats, double expectedMultiplier)
        {
            var nmLb = new GameplayLeaderboardScore(scoreInfoWithMaxStats, false, GameplayLeaderboardScore.ComboDisplayMode.Highest);
            var nfLb = new GameplayLeaderboardScore(scoreInfoWithMaxStats, false, GameplayLeaderboardScore.ComboDisplayMode.Highest);
            nmLb.TotalScore.Value = nmStandardised;
            nfLb.TotalScore.Value = nfStandardised;

            Assert.That(EzScoreRaceDisplayScore.ForLeaderboardScore(nmLb, scoreInfoWithMaxStats, ScoringMode.Standardised), Is.EqualTo(nmStandardised));
            Assert.That(EzScoreRaceDisplayScore.ForLeaderboardScore(nfLb, scoreInfoWithMaxStats, ScoringMode.Standardised), Is.EqualTo(nfStandardised));

            long nmClassic = EzScoreRaceDisplayScore.ForLeaderboardScore(nmLb, scoreInfoWithMaxStats, ScoringMode.Classic);
            long nfClassic = EzScoreRaceDisplayScore.ForLeaderboardScore(nfLb, scoreInfoWithMaxStats, ScoringMode.Classic);

            // Classic 换算以「已含系数的 standardised」为输入。Catch 公式含平方项，classic 比值不必等于 expectedMultiplier。
            Assert.That(nfClassic, Is.LessThan(nmClassic));
            Assert.That(nfClassic, Is.GreaterThan(0));

            // Osu/Taiko/Mania：classic 对 standardised 线性（或恒等），比值应贴近系数。
            if (scoreInfoWithMaxStats.Ruleset.OnlineID != 2)
            {
                long expectedNfClassic = (long)Math.Round(nmClassic * expectedMultiplier);
                Assert.That(nfClassic, Is.EqualTo(expectedNfClassic).Within(1));
            }
        }

        private static (Score score, IBeatmap beatmap) createTinyFixture(Ruleset ruleset) => ruleset switch
        {
            OsuRuleset => createOsu(),
            ManiaRuleset => createMania(),
            TaikoRuleset => createTaiko(),
            CatchRuleset => createCatch(),
            _ => throw new ArgumentOutOfRangeException(nameof(ruleset)),
        };

        private static (Score score, IBeatmap beatmap) createOsu()
        {
            var ruleset = new OsuRuleset();
            var test = new TestBeatmap(ruleset.RulesetInfo)
            {
                HitObjects = new List<HitObject>
                {
                    new HitCircle { StartTime = 1000, Position = new Vector2(256, 192) },
                    new HitCircle { StartTime = 2000, Position = new Vector2(300, 200) },
                },
            };
            var beatmap = prepareConverted(ruleset, test);
            var c1 = (HitCircle)beatmap.HitObjects[0];
            var c2 = (HitCircle)beatmap.HitObjects[1];
            var replay = new Replay
            {
                Frames = new List<ReplayFrame>
                {
                    new OsuReplayFrame(1000, c1.StackedPosition, OsuAction.LeftButton),
                    new OsuReplayFrame(1100, c1.StackedPosition),
                    new OsuReplayFrame(2000, c2.StackedPosition, OsuAction.LeftButton),
                    new OsuReplayFrame(2100, c2.StackedPosition),
                },
            };
            return (createScore(ruleset, beatmap, replay), beatmap);
        }

        private static (Score score, IBeatmap beatmap) createMania()
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

        private static (Score score, IBeatmap beatmap) createTaiko()
        {
            var ruleset = new TaikoRuleset();
            var test = new TestBeatmap(ruleset.RulesetInfo)
            {
                HitObjects = new List<HitObject>
                {
                    new Hit { StartTime = 1000, Type = HitType.Centre },
                    new Hit { StartTime = 2000, Type = HitType.Centre },
                },
            };
            var beatmap = prepareConverted(ruleset, test);
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

        private static (Score score, IBeatmap beatmap) createCatch()
        {
            var ruleset = new CatchRuleset();
            var test = new TestBeatmap(ruleset.RulesetInfo)
            {
                HitObjects = new List<HitObject>
                {
                    new Fruit { StartTime = 1000, X = 200 },
                    new Fruit { StartTime = 2000, X = 200 },
                },
            };
            var beatmap = prepareConverted(ruleset, test);
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

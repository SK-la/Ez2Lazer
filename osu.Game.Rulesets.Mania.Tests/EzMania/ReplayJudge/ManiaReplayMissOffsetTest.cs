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
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Replays;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.ReplayJudge
{
    [TestFixture]
    public class ManiaReplayMissOffsetTest
    {
        [SetUp]
        public void SetUp() => ReplayJudgeTestConfig.ApplyToGlobalConfig(LazerTapReplayFixtures.CreateTwoNoteColumnTap().environment);

        [Test]
        public void TestForceMissEarlierMissesHaveDistinctStoredOffsets()
        {
            var (score, beatmap, environment) = createSkipFirstNoteTap();
            var events = ManiaReplaySession.RunHitEvents(score, beatmap, environment);

            var missEvents = events.Where(e => e.Result == HitResult.Miss).ToList();
            Assert.That(missEvents, Has.Count.EqualTo(1));

            var hitEvent = events.Single(e => e.Result.IsHit());
            Assert.That(hitEvent.HitObject.StartTime, Is.EqualTo(2000));
            Assert.That(missEvents[0].HitObject.StartTime, Is.EqualTo(1000));
            Assert.That(missEvents[0].TimeOffset, Is.Not.EqualTo(hitEvent.TimeOffset));
        }

        [Test]
        public void TestEndSweepMissesUseNearestPressWithDistinctOffsets()
        {
            var (score, beatmap, environment) = createTwoNotesEndSweepOnly();
            var events = ManiaReplaySession.RunHitEvents(score, beatmap, environment);

            var missEvents = events.Where(e => e.Result == HitResult.Miss).OrderBy(e => e.HitObject.StartTime).ToList();
            Assert.That(missEvents, Has.Count.EqualTo(2));
            Assert.That(missEvents[0].TimeOffset, Is.Not.EqualTo(missEvents[1].TimeOffset));
        }

        [Test]
        public void TestUnjudgedColumnWithNoPressUsesZeroStoredOffset()
        {
            var (score, beatmap, environment) = createSingleNoteWrongColumnInput();
            var events = ManiaReplaySession.RunHitEvents(score, beatmap, environment);

            var miss = events.Single(e => e.Result == HitResult.Miss);
            Assert.That(miss.TimeOffset, Is.EqualTo(0));
        }

        private static (Score score, IBeatmap beatmap, GameplayEnvironment environment) createSkipFirstNoteTap()
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
                    new ManiaReplayFrame(1900, ManiaAction.Key1),
                    new ManiaReplayFrame(2100),
                },
            };

            return (createScore(ruleset, replay), beatmap, createEnvironment());
        }

        private static (Score score, IBeatmap beatmap, GameplayEnvironment environment) createTwoNotesEndSweepOnly()
        {
            var ruleset = new ManiaRuleset();
            var beatmap = new TestBeatmap(ruleset.RulesetInfo)
            {
                HitObjects = new List<HitObject>
                {
                    new Note { StartTime = 1000, Column = 0 },
                    new Note { StartTime = 10000, Column = 0 },
                },
                ControlPointInfo = new ControlPointInfo(),
            };

            foreach (var obj in beatmap.HitObjects)
                obj.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);

            // Press between both notes but outside either miss window — only end sweep marks misses.
            var replay = new Replay
            {
                Frames = new List<ReplayFrame>
                {
                    new ManiaReplayFrame(5000, ManiaAction.Key1),
                    new ManiaReplayFrame(5100),
                },
            };

            return (createScore(ruleset, replay), beatmap, createEnvironment());
        }

        private static (Score score, IBeatmap beatmap, GameplayEnvironment environment) createSingleNoteWrongColumnInput()
        {
            var ruleset = new ManiaRuleset();
            var beatmap = new TestBeatmap(ruleset.RulesetInfo)
            {
                HitObjects = new List<HitObject>
                {
                    new Note { StartTime = 1000, Column = 0 },
                },
                ControlPointInfo = new ControlPointInfo(),
            };

            foreach (var obj in beatmap.HitObjects)
                obj.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);

            var replay = new Replay
            {
                Frames = new List<ReplayFrame>
                {
                    new ManiaReplayFrame(900, ManiaAction.Key2),
                    new ManiaReplayFrame(1100),
                },
            };

            return (createScore(ruleset, replay), beatmap, createEnvironment());
        }

        private static Score createScore(ManiaRuleset ruleset, Replay replay)
        {
            return new Score
            {
                ScoreInfo =
                {
                    Ruleset = ruleset.RulesetInfo,
                    Mods = Array.Empty<Mod>(),
                },
                Replay = replay,
            };
        }

        private static GameplayEnvironment createEnvironment() => new GameplayEnvironment
        {
            ManiaHitMode = EzEnumHitMode.Lazer,
            ManiaHealthMode = EzEnumHealthMode.Lazer,
            JudgePrecedence = EzEnumJudgePrecedence.Earliest,
            OffsetPlusMania = 0,
        };
    }
}

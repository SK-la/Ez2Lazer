// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Tests.EzOsuGame.Scoring
{
    [TestFixture]
    public class EzScoreTimelineTest
    {
        [Test]
        public void TestQueryAtTimeReturnsLatestSnapshotNotExceedingClock()
        {
            var timeline = new EzScoreTimeline(new List<EzScoreTimelineSnapshot>
            {
                new EzScoreTimelineSnapshot { ClockTime = 0, TotalScore = 0 },
                new EzScoreTimelineSnapshot { ClockTime = 1000, TotalScore = 100 },
                new EzScoreTimelineSnapshot { ClockTime = 2000, TotalScore = 250 },
                new EzScoreTimelineSnapshot { ClockTime = 3000, TotalScore = 400 },
            });

            Assert.That(timeline.QueryAtTime(-100).TotalScore, Is.EqualTo(0));
            Assert.That(timeline.QueryAtTime(0).TotalScore, Is.EqualTo(0));
            Assert.That(timeline.QueryAtTime(1500).TotalScore, Is.EqualTo(100));
            Assert.That(timeline.QueryAtTime(2000).TotalScore, Is.EqualTo(250));
            Assert.That(timeline.QueryAtTime(9999).TotalScore, Is.EqualTo(400));
            Assert.That(timeline.FinalTotalScore, Is.EqualTo(400));
        }

        [Test]
        public void TestEmptyTimelineReturnsZeroScore()
        {
            var timeline = new EzScoreTimeline(Array.Empty<EzScoreTimelineSnapshot>());
            Assert.That(timeline.QueryAtTime(1000).TotalScore, Is.EqualTo(0));
            Assert.That(timeline.FinalTotalScore, Is.EqualTo(0));
        }

        [Test]
        public void TestJudgementTimeClampsEarlyOffsetsToObjectStart()
        {
            var circle = new HitCircle { StartTime = 10_000 };
            var hitEvent = new HitEvent(-500, 1.0, HitResult.Great, circle, null, null);

            Assert.That(EzScoreTimelineJudgementTime.Get(hitEvent, offsetsRelativeToEnd: true), Is.EqualTo(9_500));
            Assert.That(EzScoreTimelineJudgementTime.Get(hitEvent, offsetsRelativeToEnd: false), Is.EqualTo(9_500));
        }

        [Test]
        public void TestTimelineDoesNotInflateScoreAtClockZeroForLateHits()
        {
            var ruleset = new OsuRuleset();
            var testBeatmap = new TestBeatmap(ruleset.RulesetInfo)
            {
                HitObjects = new List<HitObject> { new HitCircle { StartTime = 10_000 } }
            };
            var beatmap = ruleset.CreateBeatmapConverter(testBeatmap).Convert();
            var circle = (HitCircle)beatmap.HitObjects[0];
            var scoreInfo = new ScoreInfo { Ruleset = ruleset.RulesetInfo };

            var hitEvents = new List<HitEvent>
            {
                new HitEvent(0, 1.0, HitResult.Great, circle, null, null),
            };

            var timeline = EzScoreTimelineBuilder.BuildFromHitEventsForTesting(ruleset, beatmap, scoreInfo, hitEvents, offsetsRelativeToEnd: false);

            Assert.That(timeline.QueryAtTime(0).TotalScore, Is.EqualTo(0));
            Assert.That(timeline.QueryAtTime(9_999).TotalScore, Is.EqualTo(0));
            Assert.That(timeline.QueryAtTime(10_000).TotalScore, Is.GreaterThan(0));
        }

        [Test]
        public void TestTimelineDoesNotInflateScoreWhenOffsetMisinterpretedAsStartRelative()
        {
            var ruleset = new OsuRuleset();
            var testBeatmap = new TestBeatmap(ruleset.RulesetInfo)
            {
                HitObjects = new List<HitObject> { new HitCircle { StartTime = 10_000 } }
            };
            var beatmap = ruleset.CreateBeatmapConverter(testBeatmap).Convert();
            var circle = (HitCircle)beatmap.HitObjects[0];
            var scoreInfo = new ScoreInfo { Ruleset = ruleset.RulesetInfo };

            var hitEvents = new List<HitEvent>
            {
                new HitEvent(-20_000, 1.0, HitResult.Great, circle, null, null),
            };

            var timeline = EzScoreTimelineBuilder.BuildFromHitEventsForTesting(ruleset, beatmap, scoreInfo, hitEvents, offsetsRelativeToEnd: true);

            Assert.That(timeline.QueryAtTime(0).TotalScore, Is.EqualTo(0));
            Assert.That(timeline.QueryAtTime(9_599).TotalScore, Is.EqualTo(0));
            Assert.That(timeline.QueryAtTime(9_600).TotalScore, Is.GreaterThan(0));
        }

        [Test]
        public void TestManiaTimelineUsesScoreHitModeNotGlobalConfig()
        {
            var ruleset = new ManiaRuleset();
            var beatmap = new TestBeatmap(ruleset.RulesetInfo)
            {
                HitObjects = new List<HitObject> { new Note { StartTime = 1000, Column = 0 } },
                ControlPointInfo = new ControlPointInfo(),
            };

            foreach (var obj in beatmap.HitObjects)
                obj.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);

            var note1 = (Note)beatmap.HitObjects[0];
            beatmap.HitObjects.Add(new Note { StartTime = 2000, Column = 0 });
            foreach (var obj in beatmap.HitObjects)
                obj.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);

            var note2 = (Note)beatmap.HitObjects[1];
            var hitEvents = new List<HitEvent>
            {
                new HitEvent(0, 1.0, HitResult.Perfect, note1, null, null),
                new HitEvent(0, 1.0, HitResult.Miss, note2, note1, null),
            };

            var lazerScoreInfo = new ScoreInfo
            {
                Ruleset = ruleset.RulesetInfo,
                ManiaHitMode = (int)EzEnumHitMode.Lazer,
                ManiaHealthMode = (int)EzEnumHealthMode.Lazer,
            };

            var bmsScoreInfo = new ScoreInfo
            {
                Ruleset = ruleset.RulesetInfo,
                ManiaHitMode = (int)EzEnumHitMode.IIDX_HD,
                ManiaHealthMode = (int)EzEnumHealthMode.IIDX_HD,
            };

            var lazerTimeline = EzScoreTimelineBuilder.BuildFromHitEventsForTesting(ruleset, beatmap, lazerScoreInfo, hitEvents);
            var bmsTimeline = EzScoreTimelineBuilder.BuildFromHitEventsForTesting(ruleset, beatmap, bmsScoreInfo, hitEvents);

            Assert.That(lazerTimeline.FinalTotalScore, Is.Not.EqualTo(bmsTimeline.FinalTotalScore));
        }

        [Test]
        public void TestOsuTimelineFinalScoreMatchesScoreProcessorFeed()
        {
            var ruleset = new OsuRuleset();
            var testBeatmap = new TestBeatmap(ruleset.RulesetInfo)
            {
                HitObjects = new List<HitObject>
                {
                    new HitCircle { StartTime = 1000 },
                    new HitCircle { StartTime = 2000 },
                },
            };
            var beatmap = ruleset.CreateBeatmapConverter(testBeatmap).Convert();
            var circle1 = (HitCircle)beatmap.HitObjects[0];
            var circle2 = (HitCircle)beatmap.HitObjects[1];
            var scoreInfo = new ScoreInfo { Ruleset = ruleset.RulesetInfo };

            var hitEvents = new List<HitEvent>
            {
                new HitEvent(0, 1.0, HitResult.Great, circle1, null, null),
                new HitEvent(0, 1.0, HitResult.Great, circle2, circle1, null),
            };

            var timeline = EzScoreTimelineBuilder.BuildFromHitEventsForTesting(ruleset, beatmap, scoreInfo, hitEvents);

            var scoreProcessor = ruleset.CreateScoreProcessor();
            scoreProcessor.ApplyBeatmap(beatmap);
            scoreProcessor.ApplyResult(new JudgementResult(circle1, circle1.CreateJudgement()) { Type = HitResult.Great });
            scoreProcessor.ApplyResult(new JudgementResult(circle2, circle2.CreateJudgement()) { Type = HitResult.Great });

            Assert.That(timeline.FinalTotalScore, Is.EqualTo(scoreProcessor.TotalScore.Value));
        }
    }
}

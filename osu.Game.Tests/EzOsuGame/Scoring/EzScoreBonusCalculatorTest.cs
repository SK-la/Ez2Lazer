// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.EzOsuGame.Scoring.Bonus;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Tests.EzOsuGame.Scoring
{
    [TestFixture]
    public class EzScoreBonusCalculatorTest
    {
        [Test]
        public void TestCotangentAnchors()
        {
            Assert.That(EzScoreBonusFormula.OffsetQuality(0, cotangent: true), Is.EqualTo(1).Within(1e-9));
            Assert.That(EzScoreBonusFormula.OffsetQuality(6, cotangent: true), Is.EqualTo(1).Within(1e-9));
            Assert.That(EzScoreBonusFormula.OffsetQuality(16, cotangent: true), Is.EqualTo(0.30).Within(0.005));
            Assert.That(EzScoreBonusFormula.OffsetQuality(11, cotangent: true), Is.EqualTo(0.57).Within(0.02));
            Assert.That(EzScoreBonusFormula.OffsetQuality(44, cotangent: true), Is.EqualTo(0).Within(1e-9));
            Assert.That(EzScoreBonusFormula.OffsetQuality(200, cotangent: true), Is.EqualTo(-1).Within(1e-9));
            Assert.That(EzScoreBonusFormula.OffsetQuality(11, cotangent: true), Is.GreaterThan(EzScoreBonusFormula.OffsetQuality(16, cotangent: true)));
        }

        [Test]
        public void TestInverseCotangentIsHalfTurn()
        {
            Assert.That(EzScoreBonusFormula.OffsetQuality(6), Is.EqualTo(1).Within(1e-9));
            Assert.That(EzScoreBonusFormula.OffsetQuality(44), Is.EqualTo(0).Within(1e-9));
            Assert.That(EzScoreBonusFormula.OffsetQuality(200), Is.EqualTo(-1).Within(1e-9));

            // 6–44：绕 (25, 0.5) 转 180°，11ms 仍高于 16ms。
            Assert.That(EzScoreBonusFormula.OffsetQuality(16), Is.GreaterThan(0.9));
            Assert.That(EzScoreBonusFormula.OffsetQuality(11), Is.GreaterThan(EzScoreBonusFormula.OffsetQuality(16)));

            for (double ms = 6; ms <= 44; ms += 1)
            {
                double rotated = 1 - EzScoreBonusFormula.OffsetQuality(50 - ms, cotangent: true);
                Assert.That(EzScoreBonusFormula.OffsetQuality(ms), Is.EqualTo(rotated).Within(1e-9));
            }
        }

        [Test]
        public void TestPerfectPlayReachesCapOnInverseCotangent()
        {
            var beatmap = createChart(200, chordSize: 4, quarterBeats: 64);
            var result = EzScoreBonusCalculator.Calculate(beatmap, createEvents(beatmap, sigma: 0, missEvery: 0), 1, new FixedKps(40));

            Assert.That(result.MissToJudge.JudgeBonus, Is.EqualTo(EzScoreBonusFormula.JUDGE_BONUS_MAX));
            Assert.That(result.MissToJudge.ErrorPenalty, Is.EqualTo(0));
            Assert.That(result.JudgeToMiss.JudgeBonus, Is.EqualTo(EzScoreBonusFormula.JUDGE_BONUS_MAX));
        }

        [Test]
        public void TestMissCountsAsTwoHundredMilliseconds()
        {
            var beatmap = createChart(200, chordSize: 4, quarterBeats: 64);
            var result = EzScoreBonusCalculator.Calculate(beatmap, createEvents(beatmap, sigma: 0, missEvery: 1), 1, new FixedKps(40));

            Assert.That(result.MissToJudge.JudgeBonus, Is.EqualTo(0));
            Assert.That(result.MissToJudge.ErrorPenalty, Is.EqualTo(-EzScoreBonusFormula.JUDGE_BONUS_MAX));
            Assert.That(result.JudgeToMiss.ErrorPenalty, Is.EqualTo(-EzScoreBonusFormula.JUDGE_BONUS_MAX));
        }

        [Test]
        public void TestHoldNoteTailMissIsNegativeValue()
        {
            var beatmap = new Beatmap();
            beatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 250 });

            var hold = new HoldNote { StartTime = 1000, Duration = 500, Column = 0 };
            hold.ApplyDefaults(beatmap.ControlPointInfo, new BeatmapDifficulty());
            beatmap.HitObjects.Add(hold);

            var events = new List<HitEvent>
            {
                new HitEvent(0, 1, HitResult.Perfect, hold.Head, null, null),
                new HitEvent(0, 1, HitResult.Miss, hold.Tail, null, null),
            };

            var result = EzScoreBonusCalculator.Calculate(beatmap, events, 1, new FixedKps(40)).MissToJudge;

            Assert.That(result.CountedNotes, Is.EqualTo(2));
            Assert.That(result.JudgeBonus, Is.EqualTo(EzScoreBonusFormula.JUDGE_BONUS_MAX / 2));
            Assert.That(result.ErrorPenalty, Is.EqualTo(-EzScoreBonusFormula.JUDGE_BONUS_MAX / 2));
            Assert.That(result.Total, Is.EqualTo(0));
        }

        [Test]
        public void TestOffsetsBeyondFortyFourMillisecondsAreErrors()
        {
            var beatmap = new Beatmap();
            var onTime = new Note { StartTime = 1000, Column = 0 };
            var late = new Note { StartTime = 2000, Column = 1 };
            beatmap.HitObjects.Add(onTime);
            beatmap.HitObjects.Add(late);

            var result = EzScoreBonusCalculator.Calculate(beatmap, new[]
            {
                new HitEvent(20, 1, HitResult.Perfect, onTime, null, null),
                new HitEvent(50, 1, HitResult.Ok, late, null, null),
            }, 1, new FixedKps(40)).MissToJudge;

            Assert.That(result.JudgeBonus, Is.GreaterThan(0));
            Assert.That(result.ErrorPenalty, Is.LessThan(0));
            Assert.That(result.ErrorPenalty, Is.GreaterThan(-EzScoreBonusFormula.JUDGE_BONUS_MAX));
        }

        [Test]
        public void TestZoneScoreUsesItsShareOfAllNotes()
        {
            var beatmap = createChart(200, chordSize: 1, quarterBeats: 10);
            var events = new List<HitEvent>();

            for (int i = 0; i < 9; i++)
                events.Add(new HitEvent(0, 1, HitResult.Perfect, beatmap.HitObjects[i], null, null));

            events.Add(new HitEvent(0, 1, HitResult.Miss, beatmap.HitObjects[9], null, null));

            var result = EzScoreBonusCalculator.Calculate(beatmap, events, 1, new FixedKps(40)).MissToJudge;

            Assert.That(result.JudgeBonus, Is.EqualTo(EzScoreBonusFormula.JUDGE_BONUS_MAX * 9 / 10));
            Assert.That(result.ErrorPenalty, Is.EqualTo(-EzScoreBonusFormula.JUDGE_BONUS_MAX / 10));
            Assert.That(result.Total, Is.EqualTo(result.JudgeBonus + result.ErrorPenalty));
        }

        [Test]
        public void TestMissBoundaryComesFromHitWindows()
        {
            const double miss_window = 80;

            var beatmap = new Beatmap();
            var note = new Note { StartTime = 1000 };
            note.HitWindows = new FixedMissWindows(miss_window);
            beatmap.HitObjects.Add(note);

            var atWindow = EzScoreBonusCalculator.Calculate(beatmap, new[]
            {
                new HitEvent(miss_window, 1, HitResult.Ok, note, null, null),
            }, 1, new FixedKps(40)).MissToJudge;

            Assert.That(atWindow.JudgeBonus, Is.EqualTo(0));
            Assert.That(atWindow.ErrorPenalty, Is.EqualTo(-EzScoreBonusFormula.JUDGE_BONUS_MAX));

            var withoutWindow = new Note { StartTime = 1000 };
            var fallback = EzScoreBonusCalculator.Calculate(new Beatmap { HitObjects = { withoutWindow } }, new[]
            {
                new HitEvent(miss_window, 1, HitResult.Ok, withoutWindow, null, null),
            }, 1, new FixedKps(40)).MissToJudge;

            Assert.That(fallback.ErrorPenalty, Is.Not.EqualTo(-EzScoreBonusFormula.JUDGE_BONUS_MAX));
        }

        [Test]
        public void TestMissBoundaryScalesWithRate()
        {
            const double miss_window = 120;
            const double rate = 1.5;

            var beatmap = new Beatmap();
            var note = new Note { StartTime = 1000 };
            note.HitWindows = new FixedMissWindows(miss_window);
            beatmap.HitObjects.Add(note);

            var result = EzScoreBonusCalculator.Calculate(beatmap, new[]
            {
                new HitEvent(miss_window, rate, HitResult.Ok, note, null, null),
            }, rate, new FixedKps(40)).MissToJudge;

            Assert.That(result.ErrorPenalty, Is.EqualTo(-EzScoreBonusFormula.JUDGE_BONUS_MAX));
        }

        [Test]
        public void TestOsuHitCircleUsesTheSameValueCurve()
        {
            var beatmap = new Beatmap();
            var circle = new HitCircle { StartTime = 1000 };
            beatmap.HitObjects.Add(circle);

            var events = new List<HitEvent>
            {
                new HitEvent(6, 1, HitResult.Great, circle, null, null),
            };

            var result = EzScoreBonusCalculator.Calculate(beatmap, events, 1, new FixedKps(40)).MissToJudge;

            Assert.That(result.JudgeBonus, Is.EqualTo(EzScoreBonusFormula.JUDGE_BONUS_MAX));
            Assert.That(result.ErrorPenalty, Is.EqualTo(0));
        }

        private static Beatmap createChart(double bpm, int chordSize, int quarterBeats)
        {
            var beatmap = new Beatmap();
            double beatLength = 60000.0 / bpm;
            beatmap.BeatmapInfo.BPM = bpm;
            beatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = beatLength });

            for (int i = 0; i < quarterBeats; i++)
            {
                double time = i * beatLength / 4;

                for (int c = 0; c < chordSize; c++)
                    beatmap.HitObjects.Add(new Note { StartTime = time, Column = (c + i) % 4 });
            }

            return beatmap;
        }

        private static List<HitEvent> createEvents(IBeatmap beatmap, double sigma, int missEvery, double? fixedOffset = null, double gameplayRate = 1)
        {
            var random = new Random(1234);
            var events = new List<HitEvent>(beatmap.HitObjects.Count);

            for (int i = 0; i < beatmap.HitObjects.Count; i++)
            {
                HitObject hitObject = beatmap.HitObjects[i];

                if (missEvery > 0 && i % missEvery == 0)
                {
                    events.Add(new HitEvent(-150, 1, HitResult.Miss, hitObject, null, null));
                    continue;
                }

                double offset = fixedOffset ?? sigma * gaussian(random);
                events.Add(new HitEvent(offset, gameplayRate, HitResult.Perfect, hitObject, null, null));
            }

            return events;
        }

        private static double gaussian(Random random)
        {
            double u1 = 1 - random.NextDouble();
            double u2 = random.NextDouble();
            return Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
        }

        private class FixedKps : IEzKpsSectionLookup
        {
            private readonly double kps;

            public FixedKps(double kps)
            {
                this.kps = kps;
            }

            public double KpsAt(double time) => kps;
        }

        private class FixedMissWindows : HitWindows
        {
            private readonly double miss;

            public FixedMissWindows(double miss)
            {
                this.miss = miss;
            }

            public override void SetDifficulty(double difficulty)
            {
            }

            public override double WindowFor(HitResult result) => result == HitResult.Miss ? miss : 16;
        }
    }
}

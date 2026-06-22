// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using NUnit.Framework;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets.Osu;
using osu.Game.Scoring;

namespace osu.Game.Tests.EzOsuGame.Scoring
{
    [TestFixture]
    public class EzScoreRaceEntryTimelineVisibilityTest
    {
        [Test]
        public void TestVolatileWriteReadOnSameThread()
        {
            var entry = new EzScoreRaceEntry(createScore());
            var timeline = new EzScoreTimeline(Array.Empty<EzScoreTimelineSnapshot>());

            Assert.That(entry.Timeline, Is.Null);
            entry.Timeline = timeline;
            // 主线程写入后立即读取必然看到 — 这是 Volatile 语义的强保证。
            Assert.That(entry.Timeline, Is.SameAs(timeline));
        }

        [Test]
        public void TestIsTimelinePendingStartsFalse()
        {
            var entry = new EzScoreRaceEntry(createScore());
            Assert.That(entry.IsTimelinePending, Is.False);
        }

        [Test]
        public void TestIsTimelinePendingCanBeToggled()
        {
            var entry = new EzScoreRaceEntry(createScore())
            {
                IsTimelinePending = true
            };

            Assert.That(entry.IsTimelinePending, Is.True);

            entry.IsTimelinePending = false;
            Assert.That(entry.IsTimelinePending, Is.False);
        }

        [Test]
        public void TestTimelineWriteVisibleAcrossVolatileReadBarrier()
        {
            var entry = new EzScoreRaceEntry(createScore());
            var timeline = new EzScoreTimeline(new[]
            {
                new EzScoreTimelineSnapshot { ClockTime = 0, TotalScore = 1234 },
            });

            // 写后再加 MemoryBarrier，模拟最弱内存模型顺序，
            // Volatile 写入保证后续读必能看到最新值。
            entry.Timeline = timeline;
            Thread.MemoryBarrier();

            Assert.That(entry.Timeline, Is.Not.Null);
            Assert.That(entry.Timeline!.FinalTotalScore, Is.EqualTo(1234));
        }

        private static ScoreInfo createScore()
        {
            return new ScoreInfo
            {
                Ruleset = new OsuRuleset().RulesetInfo,
                Date = DateTimeOffset.UtcNow,
            };
        }
    }
}

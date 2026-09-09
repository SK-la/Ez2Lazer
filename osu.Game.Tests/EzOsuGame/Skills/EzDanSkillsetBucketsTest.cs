// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.EzOsuGame.Skills;

namespace osu.Game.Tests.EzOsuGame.Skills
{
    [TestFixture]
    public class EzDanSkillsetBucketsTest
    {
        [TestCase(4, EzDanSide.Rc, 4, new[] { "jack", "tech", "speed", "stamina" })]
        [TestCase(6, EzDanSide.Rc, 4, new[] { "jack", "tech", "speed", "stream" })]
        [TestCase(7, EzDanSide.Rc, 4, new[] { "jack", "tech", "speed", "stream" })]
        [TestCase(4, EzDanSide.Ln, 0, new string[0])]
        [TestCase(6, EzDanSide.Ln, 0, new string[0])]
        [TestCase(7, EzDanSide.Ln, 4, new[] { "lngeneral", "lntech", "lninverse", "lnrelease" })]
        public void SlotsMatchHubTable(int keyCount, EzDanSide side, int expectedCount, string[] expectedIds)
        {
            var slots = EzDanSkillsetBuckets.Slots(keyCount, side);
            Assert.That(slots.Count, Is.EqualTo(expectedCount));
            Assert.That(slots.Select(s => s.Id).ToArray(), Is.EqualTo(expectedIds));
        }

        [Test]
        public void FourKeyRcFilingAveragesDominantAxisBuckets()
        {
            var clears = new List<EzDanClearEvidenceRow>();

            // 5 jack clears (Chordjack) + 4 tech (Technical) — both above CLEAR_QUORUM.
            for (int i = 0; i < 5; i++)
            {
                clears.Add(clear($"jack-{i}", 8.0 + i * 0.1));
            }

            for (int i = 0; i < 4; i++)
            {
                clears.Add(clear($"tech-{i}", 7.0 + i * 0.2));
            }

            // Under quorum for speed — must not appear.
            clears.Add(clear("speed-0", 9.0));
            clears.Add(clear("speed-1", 9.1));

            EzMinaSkillAxis? resolve(string hash) => hash switch
            {
                var h when h.StartsWith("jack-", StringComparison.Ordinal) => EzMinaSkillAxis.Chordjack,
                var h when h.StartsWith("tech-", StringComparison.Ordinal) => EzMinaSkillAxis.Technical,
                var h when h.StartsWith("speed-", StringComparison.Ordinal) => EzMinaSkillAxis.Stream,
                _ => null,
            };

            var verdicts = EzDanSkillsetBuckets.ComputeFromClears(4, EzDanSide.Rc, clears, resolve);

            Assert.That(verdicts.ContainsKey(EzDanSkillsetBuckets.JACK), Is.True);
            Assert.That(verdicts.ContainsKey(EzDanSkillsetBuckets.TECH), Is.True);
            Assert.That(verdicts.ContainsKey(EzDanSkillsetBuckets.SPEED), Is.False);
            Assert.That(verdicts[EzDanSkillsetBuckets.JACK].Clears, Is.EqualTo(5));
            Assert.That(verdicts[EzDanSkillsetBuckets.TECH].Clears, Is.EqualTo(4));
            Assert.That(verdicts[EzDanSkillsetBuckets.JACK].Label, Is.Not.Empty);
            Assert.That(verdicts[EzDanSkillsetBuckets.JACK].RawDan, Is.GreaterThan(0));
        }

        [Test]
        public void NonFourKeyOrLnReturnsEmptyVerdicts()
        {
            var clears = Enumerable.Range(0, 8)
                                   .Select(i => clear($"m-{i}", 8.0, keyCount: 7))
                                   .ToList();

            var empty7 = EzDanSkillsetBuckets.ComputeFromClears(7, EzDanSide.Rc, clears, _ => EzMinaSkillAxis.JackSpeed);
            Assert.That(empty7, Is.Empty);

            var emptyLn = EzDanSkillsetBuckets.ComputeFromClears(7, EzDanSide.Ln, clears, _ => EzMinaSkillAxis.JackSpeed);
            Assert.That(emptyLn, Is.Empty);
        }

        [Test]
        public void MinaAxisMapsToFourKeyBuckets()
        {
            Assert.That(EzDanSkillsetBuckets.TryMapMinaAxisToSkillset(EzMinaSkillAxis.JackSpeed), Is.EqualTo("jack"));
            Assert.That(EzDanSkillsetBuckets.TryMapMinaAxisToSkillset(EzMinaSkillAxis.Technical), Is.EqualTo("tech"));
            Assert.That(EzDanSkillsetBuckets.TryMapMinaAxisToSkillset(EzMinaSkillAxis.Stream), Is.EqualTo("speed"));
            Assert.That(EzDanSkillsetBuckets.TryMapMinaAxisToSkillset(EzMinaSkillAxis.Stamina), Is.EqualTo("stamina"));
        }

        private static EzDanClearEvidenceRow clear(string hash, double credited, int keyCount = 4, string side = "rc")
            => new EzDanClearEvidenceRow
            {
                Username = "tester",
                KeyCount = keyCount,
                Side = side,
                BeatmapHash = hash,
                CreditedDan = credited,
                Accuracy = 0.98,
                Rate = 1,
                ScoredAt = DateTimeOffset.UtcNow,
                AlgorithmVersion = EzDanAlgorithm.VERSION,
            };
    }
}

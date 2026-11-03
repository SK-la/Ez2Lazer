// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.EzOsuGame.Skills;

namespace osu.Game.Tests.EzOsuGame.Skills
{
    [TestFixture]
    public class EzDanPlaySsrIndexTest
    {
        [Test]
        public void RebuildsPlayValuesFromPlayerSsrAxisEvidence()
        {
            var scoredAt = DateTimeOffset.UnixEpoch.AddHours(1);
            var rows = new List<EzAxisPlayEvidenceRow>
            {
                axis("player_ssr.chordjack", 20, scoredAt),
                axis("player_ssr.stream", 5, scoredAt),
                axis("player_ssr.tech", 5, scoredAt),
                // Pattern ratings must not pollute Mina play.values.
                axis("player_pattern.jack", 99, scoredAt),
            };

            var index = EzDanPlaySsrIndex.FromAxisPlays(rows);
            var clear = clearRow("hash-a", scoredAt, accuracy: 0.98);
            var values = index.Resolve(clear);

            Assert.That(values, Is.Not.Null);
            Assert.That(values!["Chordjack"], Is.EqualTo(20));
            Assert.That(values["Stream"], Is.EqualTo(5));
            Assert.That(values.ContainsKey("jack"), Is.False);
        }

        [Test]
        public void MatchesClearByHashRateAndNearestScoredAt()
        {
            var t0 = DateTimeOffset.UnixEpoch.AddHours(1);
            var t1 = DateTimeOffset.UnixEpoch.AddHours(2);

            var rows = new List<EzAxisPlayEvidenceRow>
            {
                axis("player_ssr.stream", 10, t0, hash: "same"),
                axis("player_ssr.tech", 3, t0, hash: "same"),
                axis("player_ssr.stream", 30, t1, hash: "same"),
                axis("player_ssr.tech", 4, t1, hash: "same"),
            };

            var index = EzDanPlaySsrIndex.FromAxisPlays(rows);
            var values = index.Resolve(clearRow("same", t1.AddSeconds(2), accuracy: 0.97));

            Assert.That(values, Is.Not.Null);
            Assert.That(values!["Stream"], Is.EqualTo(30));
        }

        [Test]
        public void FourKeyFilingViaPlaySsrOpensJackUnderQuorum()
        {
            var scoredAt = DateTimeOffset.UnixEpoch.AddDays(1);
            var clears = new List<EzDanClearEvidenceRow>();
            var axisRows = new List<EzAxisPlayEvidenceRow>();

            for (int i = 0; i < 4; i++)
            {
                string hash = $"jack-{i}";
                var at = scoredAt.AddMinutes(i);
                clears.Add(clearRow(hash, at, credited: 8 + i * 0.1));
                axisRows.Add(axis("player_ssr.chordjack", 20, at, hash));
                axisRows.Add(axis("player_ssr.stream", 5, at, hash));
                axisRows.Add(axis("player_ssr.tech", 5, at, hash));
            }

            // Under quorum for tech.
            for (int i = 0; i < 2; i++)
            {
                string hash = $"tech-{i}";
                var at = scoredAt.AddHours(1).AddMinutes(i);
                clears.Add(clearRow(hash, at, credited: 7));
                axisRows.Add(axis("player_ssr.tech", 20, at, hash));
                axisRows.Add(axis("player_ssr.stream", 5, at, hash));
            }

            var playSsr = EzDanPlaySsrIndex.FromAxisPlays(axisRows);
            var verdicts = EzDanSkillsetBuckets.ComputeFromClears(4, EzDanSide.Rc, clears, playSsr.Resolve);
            var stats = EzDanSkillsetFiling.CollectFilingStats(4, EzDanSide.Rc, clears, playSsr.Resolve, _ => null);

            Assert.That(verdicts.ContainsKey(EzDanSkillsetBuckets.JACK), Is.True);
            Assert.That(verdicts.ContainsKey(EzDanSkillsetBuckets.TECH), Is.False);
            Assert.That(stats.ClearsConsidered, Is.EqualTo(6));
            Assert.That(stats.ClearsFiled, Is.EqualTo(6));
            Assert.That(stats.PrimaryCounts[EzDanSkillsetBuckets.JACK], Is.EqualTo(4));
            Assert.That(stats.PrimaryCounts[EzDanSkillsetBuckets.TECH], Is.EqualTo(2));
            Assert.That(stats.PrimaryCounts[EzDanSkillsetBuckets.TECH] < EzDanAlgorithm.CLEAR_QUORUM, Is.True);
        }

        private static EzAxisPlayEvidenceRow axis(string skillId, double value, DateTimeOffset scoredAt, string hash = "hash-a")
            => new EzAxisPlayEvidenceRow
            {
                Username = "tester",
                KeyCount = 4,
                SkillId = skillId,
                BeatmapHash = hash,
                AxisValue = value,
                Accuracy = 0.98,
                Rate = 1,
                ScoredAt = scoredAt,
                AlgorithmVersion = EzManiaSkillAlgorithm.VERSION,
            };

        private static EzDanClearEvidenceRow clearRow(string hash, DateTimeOffset scoredAt, double accuracy = 0.98, double credited = 8)
            => new EzDanClearEvidenceRow
            {
                Username = "tester",
                KeyCount = 4,
                Side = "rc",
                BeatmapHash = hash,
                CreditedDan = credited,
                Accuracy = accuracy,
                Rate = 1,
                ScoredAt = scoredAt,
                AlgorithmVersion = EzDanAlgorithm.VERSION,
            };
    }
}

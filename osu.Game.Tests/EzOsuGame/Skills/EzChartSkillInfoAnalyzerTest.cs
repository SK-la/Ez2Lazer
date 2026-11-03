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
    public class EzChartSkillInfoAnalyzerTest
    {
        [Test]
        public void PatternTagThresholdsMatchHub()
        {
            Assert.That(EzDanSkillsetFiling.PatternTagMinScore("chordjack"), Is.EqualTo(0.8));
            Assert.That(EzDanSkillsetFiling.PatternTagMinScore("delay"), Is.EqualTo(0.25));

            foreach (string id in new[] { "tech", "chordstream", "bracket", "ln", "speedjack", "jack" })
                Assert.That(EzDanSkillsetFiling.PatternTagMinScore(id), Is.EqualTo(0.5));
        }

        [Test]
        public void SpeedjackOverrideMovesJumpstreamMisreadToJack()
        {
            var misread = mina(
                stream: 22.26, jumpstream: 33.95, handstream: 32.29, stamina: 33.14,
                jackSpeed: 17.94, chordjack: 31.32, technical: 32.53);

            Assert.That(EzDanSkillsetFiling.BucketsForValues(4, EzDanSide.Rc, misread, null, 1, null),
                Is.EqualTo(new[] { EzDanSkillsetBuckets.TECH }));

            var chart = new EzChartSkillInfo
            {
                Patterns = new[] { "speedjack" },
                TechScore = 0,
                ChordjackScore = 0,
                LnRatio = 0,
                DanEligible = true,
            };

            Assert.That(EzDanSkillsetFiling.BucketsForValues(4, EzDanSide.Rc, misread, null, 1, chart),
                Is.EqualTo(new[] { EzDanSkillsetBuckets.JACK }));
        }

        [Test]
        public void SevenKeyTagBucketsUsePatterns()
        {
            var delayChart = new EzChartSkillInfo
            {
                Patterns = new[] { "delay" },
                JackShare = 0,
                StreamShare = 1,
                TechCategory = true,
                TechScore = 0,
                ChordjackScore = 0,
                LnRatio = 0,
                DanEligible = true,
            };

            // Hub danTagBucketsForTest: membership of delay → speed tile.
            var delayTags = EzDanSkillsetFiling.Buckets(7, EzDanSide.Rc)
                                               .Where(b => EzDanSkillsetFiling.ChartBelongsToTagBucket(b, delayChart))
                                               .Select(b => b.Id)
                                               .ToArray();
            Assert.That(delayTags, Does.Contain(EzDanSkillsetBuckets.SPEED));

            // MSD-only filing still empty for 7K; with chart, delay participates among tag tiles.
            Assert.That(EzDanSkillsetFiling.BucketsForValues(7, EzDanSide.Rc, mina(jackSpeed: 20), null, 1, delayChart),
                Does.Contain(EzDanSkillsetBuckets.SPEED));

            var lnChart = new EzChartSkillInfo
            {
                Patterns = new[] { "lntech", "lngeneral" },
                LnRatio = 0.8,
                TechScore = 0,
                ChordjackScore = 0,
                DanEligible = true,
                KeyCount = 7,
            };

            var lnBuckets = EzDanSkillsetFiling.BucketsForValues(7, EzDanSide.Ln, mina(), null, 1, lnChart);
            Assert.That(lnBuckets, Does.Contain(EzDanSkillsetBuckets.LN_TECH));
        }

        [Test]
        public void SevenKeyMsdOnlyReturnsEmptyBuckets()
        {
            var msd = mina(stream: 32.1, jumpstream: 26.7, handstream: 20.2, stamina: 30, jackSpeed: 16.7, chordjack: 24.3, technical: 29.3);
            Assert.That(EzDanSkillsetFiling.BucketsForValues(7, EzDanSide.Rc, msd, null, 1, null), Is.Empty);
            Assert.That(EzDanSkillsetFiling.BucketsForValues(7, EzDanSide.Ln, msd, null, 1, null), Is.Empty);
        }

        [Test]
        public void MotionFeaturesNullOffFourKey()
        {
            var notes = Enumerable.Range(0, 40)
                                  .Select(i => new EzManiaNote(i * 50, i % 7, false, i * 50))
                                  .ToList();
            Assert.That(EzMotionFeaturesComputer.Compute(notes, 7), Is.Null);
        }

        [Test]
        public void MotionFeaturesReturnsSharesOnDenseFourKeyStream()
        {
            var notes = new List<EzManiaNote>();
            double t = 0;

            // Alternating cross-hand singles at ~100ms — enough rows for measurement.
            for (int i = 0; i < 80; i++)
            {
                int column = i % 4;
                notes.Add(new EzManiaNote(t, column, false, t));
                t += 80;
            }

            var motion = EzMotionFeaturesComputer.Compute(notes, 4);
            Assert.That(motion, Is.Not.Null);
            Assert.That(motion!.RhythmBreak, Is.InRange(0, 1));
            Assert.That(motion.SameHand, Is.InRange(0, 1));
            Assert.That(motion.MiniJack, Is.InRange(0, 1));
            Assert.That(motion.CrossHandTrill, Is.InRange(0, 1));
        }

        [Test]
        public void JackDemandRequiresFourKey()
        {
            var verdict = EzFourKeyJackDemand.Classify(new EzFourKeyJackDemand.Input
            {
                KeyCount = 7,
                ChordRatio = 1,
                ChordColumnOverlapRatio = 1,
                TwoBackColumnRehitExcess = 1,
                JackPressure = 200,
            });
            Assert.That(verdict.Detected, Is.False);
        }

        [Test]
        public void JackDemandDenseAlternatingChords()
        {
            var verdict = EzFourKeyJackDemand.Classify(new EzFourKeyJackDemand.Input
            {
                KeyCount = 4,
                DurationMs = 60_000,
                ChordRatio = 0.75,
                ChordColumnOverlapRatio = 0.65,
                TwoBackColumnRehitExcess = 0.35,
                JackPressure = 100,
            });
            Assert.That(verdict.Detected, Is.True);
            Assert.That(verdict.Reasons, Does.Contain("dense_alternating_chords"));
        }

        [Test]
        public void ChartComputerProducesFilingTagsOnSyntheticJackChart()
        {
            // Repeated two-note chords on columns 0+1 → chordjack-ish.
            var notes = new List<EzManiaNote>();
            double t = 0;

            for (int i = 0; i < 200; i++)
            {
                notes.Add(new EzManiaNote(t, 0, false, t));
                notes.Add(new EzManiaNote(t, 1, false, t));
                t += 100;
            }

            var chart = new EzManiaChartInput
            {
                KeyCount = 4,
                Bpm = 150,
                TotalLengthMs = t,
                Notes = notes,
                TimingPoints = new[] { new EzManiaTimingPoint(0, 400) },
            };

            var info = EzChartSkillInfoComputer.Compute(chart);
            Assert.That(info.KeyCount, Is.EqualTo(4));
            Assert.That(info.DanEligible, Is.True);
            Assert.That(info.LnRatio, Is.EqualTo(0));
            Assert.That(info.ChordjackScore, Is.GreaterThanOrEqualTo(0));
            Assert.That(info.Motion, Is.Not.Null);
        }

        private static IReadOnlyDictionary<string, double> mina(
            double stream = 0,
            double jumpstream = 0,
            double handstream = 0,
            double stamina = 0,
            double jackSpeed = 0,
            double chordjack = 0,
            double technical = 0)
        {
            var d = new Dictionary<string, double>(StringComparer.Ordinal);
            if (stream > 0) d["Stream"] = stream;
            if (jumpstream > 0) d["Jumpstream"] = jumpstream;
            if (handstream > 0) d["Handstream"] = handstream;
            if (stamina > 0) d["Stamina"] = stamina;
            if (jackSpeed > 0) d["JackSpeed"] = jackSpeed;
            if (chordjack > 0) d["Chordjack"] = chordjack;
            if (technical > 0) d["Technical"] = technical;
            return d;
        }
    }
}

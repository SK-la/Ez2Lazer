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

            IReadOnlyDictionary<string, double>? resolveMsd(string hash)
            {
                if (hash.StartsWith("jack-", StringComparison.Ordinal))
                    return mina(chordjack: 20, stream: 5, technical: 5);
                if (hash.StartsWith("tech-", StringComparison.Ordinal))
                    return mina(technical: 20, stream: 5, jumpstream: 8);
                if (hash.StartsWith("speed-", StringComparison.Ordinal))
                    return mina(stream: 20, technical: 5);
                return null;
            }

            var verdicts = EzDanSkillsetBuckets.ComputeFromClears(4, EzDanSide.Rc, clears, resolveMsd);

            Assert.That(verdicts.ContainsKey(EzDanSkillsetBuckets.JACK), Is.True);
            Assert.That(verdicts.ContainsKey(EzDanSkillsetBuckets.TECH), Is.True);
            Assert.That(verdicts.ContainsKey(EzDanSkillsetBuckets.SPEED), Is.False);
            Assert.That(verdicts[EzDanSkillsetBuckets.JACK].Clears, Is.EqualTo(5));
            Assert.That(verdicts[EzDanSkillsetBuckets.TECH].Clears, Is.EqualTo(4));
            Assert.That(verdicts[EzDanSkillsetBuckets.JACK].Label, Is.Not.Empty);
            Assert.That(verdicts[EzDanSkillsetBuckets.JACK].RawDan, Is.GreaterThan(0));
        }

        [Test]
        public void BeatmapMsdIdsNormalizeInsideFiling()
        {
            var msd = new Dictionary<string, double>(StringComparer.Ordinal)
            {
                [EzMinaSkillAxis.Stream.ToMsdSkillId()] = 18,
                [EzMinaSkillAxis.Technical.ToMsdSkillId()] = 10,
            };

            var buckets = EzDanSkillsetFiling.BucketsForValues(4, EzDanSide.Rc, msd, lengthSeconds: 60, rate: 1, chart: null);
            Assert.That(buckets, Is.EqualTo(new[] { EzDanSkillsetBuckets.SPEED }));
        }

        [Test]
        public void NonFourKeyWithoutChartReturnsEmptyVerdicts()
        {
            var clears = Enumerable.Range(0, 8)
                                   .Select(i => clear($"m-{i}", 8.0, keyCount: 7))
                                   .ToList();

            var empty7 = EzDanSkillsetBuckets.ComputeFromClears(7, EzDanSide.Rc, clears, _ => mina(jackSpeed: 20));
            Assert.That(empty7, Is.Empty);

            var emptyLn = EzDanSkillsetBuckets.ComputeFromClears(7, EzDanSide.Ln, clears, _ => mina(jackSpeed: 20));
            Assert.That(emptyLn, Is.Empty);
        }

        [Test]
        public void SevenKeyRcPatternTagsFileWithoutClusters()
        {
            // LeoBlack clusters null; pattern tags alone must open DualPanel tiles (hub tag fallback).
            var clears = new List<EzDanClearEvidenceRow>();

            for (int i = 0; i < 5; i++)
                clears.Add(clear($"jack-{i}", 10.0 + i * 0.1, keyCount: 7));

            for (int i = 0; i < 4; i++)
                clears.Add(clear($"tech-{i}", 9.0 + i * 0.2, keyCount: 7));

            EzChartSkillInfo? resolveChart(string hash)
            {
                string[] patterns = hash.StartsWith("jack-", StringComparison.Ordinal)
                    ? new[] { "jack" }
                    : new[] { "tech" };

                return new EzChartSkillInfo
                {
                    Patterns = patterns,
                    LnRatio = 0.1,
                    DanEligible = true,
                    LengthSeconds = 90,
                    KeyCount = 7,
                };
            }

            var verdicts = EzDanSkillsetBuckets.ComputeFromClears(
                7,
                EzDanSide.Rc,
                clears,
                _ => null,
                resolveChart);

            Assert.That(verdicts.ContainsKey(EzDanSkillsetBuckets.JACK), Is.True);
            Assert.That(verdicts.ContainsKey(EzDanSkillsetBuckets.TECH), Is.True);
            Assert.That(verdicts[EzDanSkillsetBuckets.JACK].Clears, Is.EqualTo(5));
            Assert.That(verdicts[EzDanSkillsetBuckets.TECH].Clears, Is.EqualTo(4));

            var clearDans = clears.Select(c => c.CreditedDan).ToList();
            var headline = EzDanSideHeadline.FromSkillsets(7, EzDanSide.Rc, verdicts, clearDans);
            Assert.That(headline, Is.Not.Null);
            Assert.That(headline!.Value.RawDan, Is.EqualTo(
                Math.Round(verdicts.Values.Average(v => v.RawDan) * 100) / 100).Within(1e-6));
        }

        [Test]
        public void SevenKeyLnPatternTagsFileAndAnchorHeadline()
        {
            var clears = new List<EzDanClearEvidenceRow>();

            for (int i = 0; i < 5; i++)
                clears.Add(clear($"gen-{i}", 13.0, keyCount: 7, side: "ln"));

            for (int i = 0; i < 4; i++)
                clears.Add(clear($"rel-{i}", 9.0, keyCount: 7, side: "ln"));

            EzChartSkillInfo? resolveChart(string hash)
            {
                string[] patterns = hash.StartsWith("gen-", StringComparison.Ordinal)
                    ? new[] { "lngeneral", "ln" }
                    : new[] { "lnrelease" };

                return new EzChartSkillInfo
                {
                    Patterns = patterns,
                    LnRatio = 0.5,
                    DanEligible = true,
                    LengthSeconds = 120,
                    KeyCount = 7,
                };
            }

            var verdicts = EzDanSkillsetBuckets.ComputeFromClears(
                7,
                EzDanSide.Ln,
                clears,
                _ => null,
                resolveChart);

            Assert.That(verdicts.ContainsKey(EzDanSkillsetBuckets.LN_GENERAL), Is.True);
            Assert.That(verdicts.ContainsKey(EzDanSkillsetBuckets.LN_RELEASE), Is.True);

            var clearDans = clears.Select(c => c.CreditedDan).ToList();
            var headline = EzDanSideHeadline.FromSkillsets(7, EzDanSide.Ln, verdicts, clearDans);
            Assert.That(headline, Is.Not.Null);
            // General 13 + Release 9 → anchor pull → 12
            Assert.That(headline!.Value.RawDan, Is.EqualTo(12).Within(1e-6));
        }

        [Test]
        public void BucketsForValuesUsesPatternTagsWhenClustersNull()
        {
            var chart = new EzChartSkillInfo
            {
                Patterns = new[] { "delay" },
                DanEligible = true,
                KeyCount = 7,
            };

            var buckets = EzDanSkillsetFiling.BucketsForValues(
                7,
                EzDanSide.Rc,
                new Dictionary<string, double>(),
                lengthSeconds: 60,
                rate: 1,
                chart);

            Assert.That(buckets, Is.EqualTo(new[] { EzDanSkillsetBuckets.SPEED }));
        }

        [Test]
        public void MinaAxisMapsToFourKeyBuckets()
        {
            Assert.That(EzDanSkillsetBuckets.TryMapMinaAxisToSkillset(EzMinaSkillAxis.JackSpeed), Is.EqualTo("jack"));
            Assert.That(EzDanSkillsetBuckets.TryMapMinaAxisToSkillset(EzMinaSkillAxis.Technical), Is.EqualTo("tech"));
            Assert.That(EzDanSkillsetBuckets.TryMapMinaAxisToSkillset(EzMinaSkillAxis.Stream), Is.EqualTo("speed"));
            Assert.That(EzDanSkillsetBuckets.TryMapMinaAxisToSkillset(EzMinaSkillAxis.Stamina), Is.EqualTo("stamina"));
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

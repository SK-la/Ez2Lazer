// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.EzOsuGame.Skills;
using osu.Game.EzOsuGame.Skills.LeoBlack;

namespace osu.Game.Tests.EzOsuGame.Skills
{
    [TestFixture]
    public class EzLeoBlackClustersTest
    {
        [Test]
        public void ClusterMetricsMatchHubShareAndCategoryRegex()
        {
            var clusters = new[]
            {
                new EzLeoBlackCluster { Pattern = "Jacks", Importance = 80, Bpm = 150, Amount = 10 },
                new EzLeoBlackCluster { Pattern = "Chordstream", Importance = 20, Bpm = 200, Amount = 5 },
            };

            Assert.That(EzLeoBlackClusterMetrics.JackShare(clusters), Is.EqualTo(0.8).Within(1e-9));
            Assert.That(EzLeoBlackClusterMetrics.StreamShare(clusters), Is.EqualTo(0.2).Within(1e-9));
            Assert.That(EzLeoBlackClusterMetrics.ClusterShare(clusters, _ => false), Is.EqualTo(0).Within(1e-9));
            Assert.That(EzLeoBlackClusterMetrics.ClusterShare([], _ => true), Is.Null);

            Assert.That(EzLeoBlackClusterMetrics.TechCategory("Jumpstream Tech"), Is.True);
            Assert.That(EzLeoBlackClusterMetrics.ClusterTrill("Trill"), Is.True);
            Assert.That(EzLeoBlackClusterMetrics.HandstreamCluster("Handstream"), Is.True);
            Assert.That(EzLeoBlackClusterMetrics.TechCategory("Stream"), Is.False);
        }

        [Test]
        public void JackDemandClusterDominantArmFiresWithJacksShare()
        {
            var clusters = new List<EzFourKeyJackDemand.Cluster>
            {
                new EzFourKeyJackDemand.Cluster { Pattern = "Jacks", Bpm = 160, Importance = 90, Label = "Jacks" },
                new EzFourKeyJackDemand.Cluster { Pattern = "Chordstream", Bpm = 200, Importance = 10, Label = "Chordstream" },
            };

            var verdict = EzFourKeyJackDemand.Classify(new EzFourKeyJackDemand.Input
            {
                KeyCount = 4,
                DurationMs = 90_000,
                ChordRatio = 0.2,
                ChordColumnOverlapRatio = 0.2,
                TwoBackColumnRehitExcess = 0,
                JackPressure = 100,
                Clusters = clusters,
            });

            Assert.That(verdict.Detected, Is.True);
            Assert.That(verdict.Reasons, Does.Contain("jack_cluster_dominant"));
        }

        [Test]
        public void SevenKeyJackBucketUsesJackShareWithoutPatternTags()
        {
            var chart = new EzChartSkillInfo
            {
                Patterns = [],
                JackShare = 0.7,
                StreamShare = 0.2,
                TechCategory = false,
                DanEligible = true,
                LnRatio = 0.05,
                KeyCount = 7,
            };

            Assert.That(EzDanSkillsetFiling.ChartBelongsToTagBucket(
                EzDanSkillsetFiling.Buckets(7, EzDanSide.Rc).First(b => b.Id == EzDanSkillsetBuckets.JACK),
                chart), Is.True);

            var buckets = EzDanSkillsetFiling.BucketsForValues(7, EzDanSide.Rc, new Dictionary<string, double>(), 90, 1, chart);
            Assert.That(buckets, Does.Contain(EzDanSkillsetBuckets.JACK));
        }

        [Test]
        public void SevenKeyStreamBucketUsesStreamShare()
        {
            var chart = new EzChartSkillInfo
            {
                Patterns = [],
                JackShare = 0.1,
                StreamShare = 0.85,
                TechCategory = false,
                DanEligible = true,
                LnRatio = 0.05,
                KeyCount = 7,
            };

            var buckets = EzDanSkillsetFiling.BucketsForValues(7, EzDanSide.Rc, new Dictionary<string, double>(), 90, 1, chart);
            Assert.That(buckets, Does.Contain(EzDanSkillsetBuckets.STREAM));
            Assert.That(buckets, Does.Not.Contain(EzDanSkillsetBuckets.JACK));
        }

        [Test]
        public void FourKeyJackDemandFromClustersOverridesJumpstreamArgmax()
        {
            var msd = new Dictionary<string, double>
            {
                ["Stream"] = 20,
                ["Jumpstream"] = 34,
                ["Handstream"] = 30,
                ["Stamina"] = 28,
                ["JackSpeed"] = 18,
                ["Chordjack"] = 25,
                ["Technical"] = 32,
            };

            var chart = new EzChartSkillInfo
            {
                Patterns = [],
                JackDemand = true,
                JackShare = 0.65,
                DanEligible = true,
                LnRatio = 0,
                KeyCount = 4,
            };

            Assert.That(EzDanSkillsetFiling.BucketsForValues(4, EzDanSide.Rc, msd, 120, 1, null),
                Is.EqualTo(new[] { EzDanSkillsetBuckets.TECH }));
            Assert.That(EzDanSkillsetFiling.BucketsForValues(4, EzDanSide.Rc, msd, 120, 1, chart),
                Is.EqualTo(new[] { EzDanSkillsetBuckets.JACK }));
        }

        [Test]
        public void AnalyzerProducesClustersOnDenseSyntheticChart()
        {
            var notes = new List<EzManiaNote>();
            double t = 0;

            // Dense 4K chord jacks: two-note chords repeating.
            for (int i = 0; i < 200; i++)
            {
                notes.Add(new EzManiaNote(t, 0, false, t));
                notes.Add(new EzManiaNote(t, 1, false, t));
                t += 100;
            }

            var input = new EzManiaChartInput
            {
                KeyCount = 4,
                Bpm = 150,
                TotalLengthMs = t,
                Notes = notes,
                TimingPoints = [new EzManiaTimingPoint(0, 400)],
            };

            var leo = EzLeoBlackPatternAnalyzer.TryAnalyze(input);
            Assert.That(leo, Is.Not.Null);
            Assert.That(leo!.Clusters.Count, Is.GreaterThan(0));
            Assert.That(leo.Category, Is.Not.Empty);

            var info = EzChartSkillInfoComputer.Compute(input);
            Assert.That(info.JackShare.HasValue || info.StreamShare.HasValue || info.TechCategory.HasValue, Is.True);
        }
    }
}

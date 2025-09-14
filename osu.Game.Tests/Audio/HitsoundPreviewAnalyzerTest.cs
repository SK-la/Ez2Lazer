// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Game.Audio;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Tests.Audio
{
    [TestFixture]
    public class HitsoundPreviewAnalyzerTest
    {
        private HitsoundPreviewAnalyzer analyzer = null!;

        [SetUp]
        public void SetUp()
        {
            analyzer = new HitsoundPreviewAnalyzer();
        }

        [Test]
        public void TestShouldPreviewHitsoundsWithLowCount()
        {
            var beatmap = createTestBeatmap(10); // 10 hitsounds
            Assert.That(analyzer.ShouldPreviewHitsounds(beatmap), Is.False);
        }

        [Test]
        public void TestShouldPreviewHitsoundsWithHighCount()
        {
            var beatmap = createTestBeatmap(25); // 25 hitsounds
            Assert.That(analyzer.ShouldPreviewHitsounds(beatmap), Is.True);
        }

        [Test]
        public void TestShouldPreviewHitsoundsWithExactThreshold()
        {
            var beatmap = createTestBeatmap(20); // Exactly 20 hitsounds
            Assert.That(analyzer.ShouldPreviewHitsounds(beatmap), Is.False);
        }

        [Test]
        public void TestCountHitsounds()
        {
            var beatmap = createTestBeatmap(15);
            Assert.That(analyzer.CountHitsounds(beatmap), Is.EqualTo(15));
        }

        [Test]
        public void TestGetHitsoundPreviewPoints()
        {
            var beatmap = createTestBeatmap(5);
            var points = analyzer.GetHitsoundPreviewPoints(beatmap, 1000, 5000);

            Assert.That(points.Count, Is.EqualTo(5));
            Assert.That(points.All(p => p.Time >= 1000 && p.Time <= 5000), Is.True);
        }

        [Test]
        public void TestEmptyBeatmap()
        {
            var beatmap = new TestBeatmap(new OsuRuleset().RulesetInfo);
            Assert.That(analyzer.ShouldPreviewHitsounds(beatmap), Is.False);
            Assert.That(analyzer.CountHitsounds(beatmap), Is.EqualTo(0));
        }

        private TestBeatmap createTestBeatmap(int hitsoundCount)
        {
            var beatmap = new TestBeatmap(new OsuRuleset().RulesetInfo);

            for (int i = 0; i < hitsoundCount; i++)
            {
                var hitObject = new HitCircle
                {
                    StartTime = 1000 + i * 100,
                    Samples = { new HitSampleInfo(HitSampleInfo.HIT_WHISTLE) }
                };
                beatmap.HitObjects.Add(hitObject);
            }

            return beatmap;
        }
    }
}

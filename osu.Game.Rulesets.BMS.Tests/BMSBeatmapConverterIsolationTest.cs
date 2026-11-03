// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.BMS.Beatmaps;
using osu.Game.Rulesets.BMS.Objects;

namespace osu.Game.Rulesets.BMS.Tests
{
    [TestFixture]
    public class BMSBeatmapConverterIsolationTest
    {
        [Test]
        public void TestConvertedBeatmapsDoNotShareHitObjectInstances()
        {
            var sourceBeatmap = new Beatmap
            {
                HitObjects =
                {
                    new BMSHoldNote
                    {
                        StartTime = 1000,
                        Duration = 500,
                        Column = 0,
                        IsScratch = true
                    }
                }
            };

            var ruleset = new BMSRuleset();
            var converter = new BMSBeatmapConverter(sourceBeatmap, ruleset);

            var convertedA = converter.Convert();
            var convertedB = converter.Convert();

            var holdA = convertedA.HitObjects.OfType<BMSHoldNote>().Single();
            var holdB = convertedB.HitObjects.OfType<BMSHoldNote>().Single();

            Assert.That(ReferenceEquals(holdA, holdB), Is.False, "conversions must not share mutable hitobject instances");
            Assert.That(ReferenceEquals(holdA, sourceBeatmap.HitObjects.Single()), Is.False, "converted object must be detached from source beatmap");
        }
    }
}

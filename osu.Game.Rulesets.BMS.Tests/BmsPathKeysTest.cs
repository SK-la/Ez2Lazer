// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Rulesets.BMS.Beatmaps;

namespace osu.Game.Rulesets.BMS.Tests
{
    [TestFixture]
    public class BmsPathKeysTest
    {
        [Test]
        public void TestPathKeyIsStableForSamePath()
        {
            const string path = @"E:\Music\test\chart.bms";
            string first = BmsPathKeys.ComputeChartPathKey(path);
            string second = BmsPathKeys.ComputeChartPathKey(path);
            Assert.That(first, Is.EqualTo(second));
            Assert.That(first, Has.Length.EqualTo(64));
        }

        [Test]
        public void TestRealmFileHashMatchesPathKey()
        {
            const string path = @"E:\Music\test\chart.bms";
            Assert.That(BmsPathKeys.ComputeRealmFileHash(path), Is.EqualTo(BmsPathKeys.ComputeChartPathKey(path)));
        }
    }
}

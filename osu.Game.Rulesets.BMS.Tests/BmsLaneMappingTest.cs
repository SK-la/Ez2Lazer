// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;

namespace osu.Game.Rulesets.BMS.Tests
{
    [TestFixture]
    public class BmsLaneMappingTest
    {
        [TestCase(16, true)]
        [TestCase(56, true)]
        [TestCase(26, true)]
        [TestCase(66, true)]
        [TestCase(11, false)]
        [TestCase(21, false)]
        public void TestScratchChannelDetection(int channel, bool expected)
            => Assert.That(BmsLaneMapping.IsScratchChannel(channel), Is.EqualTo(expected));

        [TestCase(16, 0)]
        [TestCase(11, 1)]
        [TestCase(19, 7)]
        [TestCase(26, 8)]
        [TestCase(21, 9)]
        [TestCase(29, 15)]
        [TestCase(999, 0)]
        public void TestChannelToColumnMapping(int channel, int expectedColumn)
            => Assert.That(BmsLaneMapping.ChannelToColumn(channel), Is.EqualTo(expectedColumn));
    }
}

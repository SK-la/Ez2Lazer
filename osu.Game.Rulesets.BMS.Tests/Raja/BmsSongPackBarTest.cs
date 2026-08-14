// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Game.Rulesets.BMS.Beatmaps;
using osu.Game.Rulesets.BMS.UI.BmsSongSelect;
using osu.Game.Rulesets.BMS.UI.BmsSongSelect.Bars;

namespace osu.Game.Rulesets.BMS.Tests.Raja
{
    [TestFixture]
    public class BmsSongPackBarTest
    {
        [Test]
        public void TestPackUsesSharedTitleAndOrdersDifficultiesByLevel()
        {
            var easy = summary("E:\\bms\\song", "easy.bms", "Song", 3);
            var hyper = summary("E:\\bms\\song", "hyper.bms", "Song", 12);
            var pack = new BmsSongPackBar(@"E:\bms\song", "song", new[] { hyper, easy });

            Assert.Multiple(() =>
            {
                Assert.That(pack.Title, Is.EqualTo("Song"));
                Assert.That(pack.Subtitle, Does.Contain("2 diffs"));
                Assert.That(pack.Difficulties[0].PlayLevel, Is.EqualTo(3));
                Assert.That(pack.Difficulties[1].PlayLevel, Is.EqualTo(12));
            });
        }

        [Test]
        public void TestSortPolicyOrdersPacksWithSongs()
        {
            var pack = new BmsSongPackBar(@"E:\bms\b-pack", "b-pack", new[] { summary(@"E:\bms\b-pack", "a.bms", "Beta", 8) });
            var song = new BmsSongBar(summary(@"E:\bms\a-song", "z.bms", "Alpha", 4));
            var policy = new BmsSortPolicy();

            var sorted = policy.Sort(new BmsBar[] { pack, song });

            Assert.That(sorted[0].Title, Is.EqualTo("Alpha"));
            Assert.That(sorted[1].Title, Is.EqualTo("Beta"));
        }

        private static BmsChartSummary summary(string folder, string fileName, string title, int level)
        {
            return new BmsChartSummary(
                Guid.NewGuid(),
                Guid.NewGuid(),
                fileName,
                System.IO.Path.Combine(folder, fileName),
                folder,
                fileName,
                title,
                "Artist",
                level,
                7,
                140,
                100,
                -1);
        }
    }
}

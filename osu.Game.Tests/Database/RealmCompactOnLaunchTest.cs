// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Database;

namespace osu.Game.Tests.Database
{
    /// <summary>
    /// Compacting on launch is what returns the pages a migration freed back to the file system. It only pays for
    /// itself once free space outgrows live data, and it blocks startup while it runs.
    /// </summary>
    [TestFixture]
    public class RealmCompactOnLaunchTest
    {
        private const ulong megabyte = 1024 * 1024;

        [Test]
        public void Small_file_is_left_alone_however_empty_it_is()
        {
            Assert.That(RealmAccess.ShouldCompactOnLaunchForTesting(10 * megabyte, 1 * megabyte), Is.False);
        }

        [Test]
        public void Large_mostly_live_file_is_left_alone()
        {
            Assert.That(RealmAccess.ShouldCompactOnLaunchForTesting(400 * megabyte, 300 * megabyte), Is.False);
        }

        [Test]
        public void Large_file_whose_free_space_outgrows_its_data_is_compacted()
        {
            Assert.That(RealmAccess.ShouldCompactOnLaunchForTesting(400 * megabyte, 100 * megabyte), Is.True);
        }

        [Test]
        public void Half_used_file_is_not_yet_worth_rewriting()
        {
            // Exactly half is the boundary: the rewrite has to be clearly profitable to justify blocking startup.
            Assert.That(RealmAccess.ShouldCompactOnLaunchForTesting(400 * megabyte, 200 * megabyte), Is.False);
        }
    }
}

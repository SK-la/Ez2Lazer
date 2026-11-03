// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Database;

namespace osu.Game.Tests.Database
{
    [TestFixture]
    public class TestRealmSchemaMode
    {
        [Test]
        public void Ez_file_schema_version_matches_RealmAccess_constants()
        {
            Assert.That(RealmAccess.EzFileSchemaVersion, Is.EqualTo(RealmAccess.UpstreamSchemaVersion * 1000 + RealmAccess.EZ_REALM_SCHEMA_VERSION));
        }

        [Test]
        public void Official_mode_uses_raw_upstream_version()
        {
            Assert.That(RealmAccess.UpstreamSchemaVersion, Is.GreaterThan(0));
        }
    }
}

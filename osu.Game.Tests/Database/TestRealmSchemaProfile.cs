// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Database;

namespace osu.Game.Tests.Database
{
    [TestFixture]
    public class TestRealmSchemaProfile
    {
        [Test]
        public void Ez_profile_matches_RealmAccess_constants()
        {
            Assert.That(EzRealmSchemaProfile.Instance.FileSchemaVersion, Is.EqualTo(51 * 1000 + RealmAccess.EZ_REALM_SCHEMA_VERSION));
            Assert.That(EzRealmSchemaProfile.Instance.EzRealmSchemaVersion, Is.EqualTo(RealmAccess.EZ_REALM_SCHEMA_VERSION));
        }

        [Test]
        public void Official_profile_uses_raw_upstream_version()
        {
            Assert.That(OfficialRealmSchemaProfile.Instance.FileSchemaVersion, Is.EqualTo(OfficialRealmSchemaProfile.UPSTREAM_SCHEMA_VERSION));
            Assert.That(OfficialRealmSchemaProfile.Instance.EzRealmSchemaVersion, Is.Zero);
        }
    }
}

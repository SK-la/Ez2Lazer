// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Database;

namespace osu.Game.Tests.Database
{
    [TestFixture]
    public class TestOfficialRealmAccess : RealmTest
    {
        [Test]
        public void TestOpensWithUpstreamSchemaVersionOnly()
        {
            RunTestWithOfficialRealm((official, _) =>
            {
                official.Run(realm =>
                {
                    Assert.That(realm.Config.SchemaVersion, Is.EqualTo(RealmAccess.UpstreamSchemaVersion));
                });
            });
        }
    }
}

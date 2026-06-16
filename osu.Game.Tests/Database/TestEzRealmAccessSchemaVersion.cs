// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Database;

namespace osu.Game.Tests.Database
{
    [TestFixture]
    public class TestEzRealmAccessSchemaVersion : RealmTest
    {
        [Test]
        public void TestOpensWithEzFileSchemaVersion()
        {
            RunTestWithRealm((realm, _) =>
            {
                realm.Run(r =>
                {
                    Assert.That(r.Config.SchemaVersion, Is.EqualTo(RealmAccess.EzFileSchemaVersion));
                });
            });
        }
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Game.Database;

namespace osu.Game.Tests.Database
{
    [TestFixture]
    public class TestEzRealmFilenameSidecar
    {
        [Test]
        public void GetVersionedRealmFilename_uses_file_schema_version()
        {
            Assert.That(
                RealmAccess.GetVersionedRealmFilename("client.realm", EzRealmSchemaProfile.Instance.FileSchemaVersion),
                Is.EqualTo($"client_{EzRealmSchemaProfile.Instance.FileSchemaVersion}.realm"));
        }

        [Test]
        public void EnumerateSidecarPredecessorFilenames_prefers_earlier_ez_versions_then_legacy_upstream_then_client_realm()
        {
            var predecessors = RealmAccess.EnumerateSidecarPredecessorFilenames("client.realm", EzRealmSchemaProfile.Instance).ToList();

            int baseVersion = EzRealmSchemaProfile.UPSTREAM_SCHEMA_VERSION * 1000;

            for (int ez = EzRealmSchemaProfile.EZ_REALM_SCHEMA_VERSION - 1; ez >= 1; ez--)
                Assert.That(predecessors, Does.Contain($"client_{baseVersion + ez}.realm"));

            Assert.That(predecessors, Does.Contain($"client_{EzRealmSchemaProfile.UPSTREAM_SCHEMA_VERSION}.realm"));
            Assert.That(predecessors[^1], Is.EqualTo("client.realm"));
        }

        [Test]
        public void EnumerateSidecarPredecessorFilenames_for_official_profile_uses_upstream_chain()
        {
            var predecessors = RealmAccess.EnumerateSidecarPredecessorFilenames("client.realm", OfficialRealmSchemaProfile.Instance).ToList();

            Assert.That(predecessors, Does.Contain("client_50.realm"));
            Assert.That(predecessors, Does.Contain("client_0.realm"));
            Assert.That(predecessors[^1], Is.EqualTo("client.realm"));
            Assert.That(predecessors.Any(f => f.Contains("510")), Is.False);
        }
    }
}

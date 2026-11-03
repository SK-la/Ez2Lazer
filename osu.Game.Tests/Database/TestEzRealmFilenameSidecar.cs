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
                RealmAccess.GetVersionedRealmFilename("client.realm", RealmAccess.EzFileSchemaVersion),
                Is.EqualTo($"client_{RealmAccess.EzFileSchemaVersion}.realm"));
        }

        [Test]
        public void EnumerateSidecarPredecessorFilenames_prefers_earlier_ez_versions_then_legacy_upstream_then_client_realm()
        {
            var predecessors = RealmAccess.EnumerateSidecarPredecessorFilenames("client.realm", RealmSchemaMode.Ez).ToList();

            int currentUpstream = RealmAccess.UpstreamSchemaVersion;
            int currentBase = currentUpstream * 1000;
            int previousUpstream = currentUpstream - 1;
            int previousBase = previousUpstream * 1000;

            for (int ez = RealmAccess.EZ_REALM_SCHEMA_VERSION - 1; ez >= 1; ez--)
                Assert.That(predecessors, Does.Contain($"client_{currentBase + ez}.realm"));

            Assert.That(predecessors, Does.Contain($"client_{currentUpstream}.realm"));

            // Cross-upstream bump must find the previous DEBUG sidecar (e.g. 51007 → 52007).
            Assert.That(predecessors, Does.Contain($"client_{previousBase + RealmAccess.EZ_REALM_SCHEMA_VERSION}.realm"));
            Assert.That(predecessors, Does.Contain($"client_{previousUpstream}.realm"));

            int previousFullEzIndex = predecessors.IndexOf($"client_{previousBase + RealmAccess.EZ_REALM_SCHEMA_VERSION}.realm");
            int previousPlainIndex = predecessors.IndexOf($"client_{previousUpstream}.realm");
            Assert.That(previousFullEzIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(previousFullEzIndex, Is.LessThan(previousPlainIndex));

            Assert.That(predecessors[^1], Is.EqualTo("client.realm"));
        }

        [Test]
        public void EnumerateSidecarPredecessorFilenames_for_official_mode_uses_upstream_chain()
        {
            var predecessors = RealmAccess.EnumerateSidecarPredecessorFilenames("client.realm", RealmSchemaMode.Official).ToList();

            Assert.That(predecessors, Does.Contain($"client_{RealmAccess.UpstreamSchemaVersion - 1}.realm"));
            Assert.That(predecessors, Does.Contain("client_0.realm"));
            Assert.That(predecessors[^1], Is.EqualTo("client.realm"));
            Assert.That(predecessors.Any(f => f.Contains("510")), Is.False);
        }

        [Test]
        public void DecodeDiskSchemaVersion_splits_ez_encoded_and_plain_upstream()
        {
            RealmAccess.DecodeDiskSchemaVersion(51007, out int upstream, out int ez);
            Assert.That(upstream, Is.EqualTo(51));
            Assert.That(ez, Is.EqualTo(7));

            RealmAccess.DecodeDiskSchemaVersion(52, out upstream, out ez);
            Assert.That(upstream, Is.EqualTo(52));
            Assert.That(ez, Is.EqualTo(0));

            RealmAccess.DecodeDiskSchemaVersion(52007, out upstream, out ez);
            Assert.That(upstream, Is.EqualTo(52));
            Assert.That(ez, Is.EqualTo(7));
        }
    }
}

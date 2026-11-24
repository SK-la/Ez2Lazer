// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.EzOsuGame.Changelog;
using osu.Game.EzOsuGame.Wiki;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Updater;

namespace osu.Game.Tests.EzOsuGame.Changelog
{
    [TestFixture]
    public class EzGitHubChangelogCatalogueTest
    {
        private static GitHubRelease sampleRelease(string tag, string body) => new GitHubRelease
        {
            TagName = tag,
            Body = body,
            PublishedAt = new DateTime(2026, 11, 22, 0, 0, 0, DateTimeKind.Utc),
        };

        [Test]
        public void TestEmptyBuildListProducesNoUpdateStreams()
        {
            APIChangelogBuild[] builds = Array.Empty<APIChangelogBuild>();

            Assert.That(EzGitHubChangelogCatalogue.CreateUpdateStreams(builds), Is.Empty);
        }

        [Test]
        public void TestWikiChangelogManifestPathUsesWikiRawBase()
        {
            Assert.That(
                $"{EzWikiPaths.RAW_WIKI_BASE}{EzGitHubChangelogCatalogue.WIKI_CHANGELOG_MANIFEST_FILE}",
                Does.Contain("/wiki/SK-la/Ez2Lazer/changelog-releases.json"));
        }

        [Test]
        public void TestTryParseManifestReadsReleasesArray()
        {
            const string json = """
                {
                  "releases": [
                    {
                      "tag_name": "2026.1122.0-ez2lazer",
                      "body": "## zh\\n测试",
                      "published_at": "2026-11-22T00:00:00Z"
                    }
                  ]
                }
                """;

            Assert.That(EzGitHubChangelogReleaseCache.TryParseManifest(json, out var releases), Is.True);
            Assert.That(releases.Single().TagName, Is.EqualTo("2026.1122.0-ez2lazer"));
            Assert.That(releases.Single().Body, Does.Contain("测试"));
        }

        [Test]
        public async Task TestFetchBuildsAsyncUsesLocalCacheWhenManifestMissing()
        {
            using var storage = new TemporaryNativeStorage(nameof(TestFetchBuildsAsyncUsesLocalCacheWhenManifestMissing));

            const string tag = "2026.1122.0-ez2lazer";
            EzGitHubChangelogReleaseCache.Write(storage, new[] { sampleRelease(tag, "cached-markdown") });

            var catalogue = new EzGitHubChangelogCatalogue();
            var builds = await catalogue.FetchBuildsAsync(storage, CancellationToken.None).ConfigureAwait(false);

            Assert.That(builds, Has.Count.EqualTo(1));
            Assert.That(builds[0].DisplayVersion, Is.EqualTo(tag));
            Assert.That(catalogue.GetBody(tag), Is.EqualTo("cached-markdown"));
        }
    }
}
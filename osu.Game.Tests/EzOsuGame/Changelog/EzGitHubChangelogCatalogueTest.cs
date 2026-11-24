// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.EzOsuGame.Changelog;
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
        public void TestReleaseChangelogBundleUrlIsGitHubReleaseAsset()
        {
            Assert.That(
                EzGitHubChangelogCatalogue.RELEASE_CHANGELOG_BUNDLE_URL,
                Does.Contain("/releases/latest/download/changelog-releases.json"));
        }

        [Test]
        public async Task TestFetchBuildsAsyncReturnsFreshCacheWithoutRemoteHeadFetch()
        {
            using var storage = new TemporaryNativeStorage(nameof(TestFetchBuildsAsyncReturnsFreshCacheWithoutRemoteHeadFetch));

            const string tag = "2026.1122.0-ez2lazer";
            EzGitHubChangelogReleaseCache.Write(storage, new[] { sampleRelease(tag, "fresh-markdown") });

            var catalogue = new EzGitHubChangelogCatalogue();
            var builds = await catalogue.FetchBuildsAsync(storage, CancellationToken.None).ConfigureAwait(false);

            Assert.That(builds, Has.Count.EqualTo(1));
            Assert.That(builds[0].DisplayVersion, Is.EqualTo(tag));
            Assert.That(catalogue.GetBody(tag), Is.EqualTo("fresh-markdown"));
        }
    }
}

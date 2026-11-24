// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
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

        [SetUp]
        public void SetUp() => EzGitHubChangelogCatalogue.ReleaseChangelogBundleUrlOverrideForTests = null;

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
                EzGitHubChangelogCatalogue.ReleaseChangelogBundleUrl,
                Does.Contain("/releases/latest/download/changelog-releases.json"));
        }

        [Test]
        public void TestMergeReleasesByTagPrefersHeadBodyForSameTag()
        {
            var cached = new[] { sampleRelease("2026.1102.0-ez2lazer", "old-body") };
            var head = new[] { sampleRelease("2026.1102.0-ez2lazer", "new-body"), sampleRelease("2026.1122.0-ez2lazer", "latest") };

            var merged = EzGitHubChangelogCatalogue.MergeReleasesByTagForTesting(cached, head);

            Assert.That(merged, Has.Count.EqualTo(2));
            Assert.That(merged.Single(r => r.TagName == "2026.1102.0-ez2lazer").Body, Is.EqualTo("new-body"));
        }

        [Test]
        public void TestWriteThenTryReadIsFresh()
        {
            using var storage = new TemporaryNativeStorage(nameof(TestWriteThenTryReadIsFresh));

            EzGitHubChangelogReleaseCache.Write(storage, new[] { sampleRelease("2026.1122.0-ez2lazer", "## zh\n\nhello") });

            Assert.That(
                EzGitHubChangelogReleaseCache.TryRead(storage, out var releases, out var fresh, out var backoffUntil),
                Is.True);
            Assert.That(fresh, Is.True);
            Assert.That(backoffUntil, Is.Null);
            Assert.That(releases, Has.Length.EqualTo(1));
            Assert.That(releases[0].TagName, Is.EqualTo("2026.1122.0-ez2lazer"));
        }

        [Test]
        public void TestApplyApiBackoffPreservesFetchedAtAndReleases()
        {
            using var storage = new TemporaryNativeStorage(nameof(TestApplyApiBackoffPreservesFetchedAtAndReleases));

            EzGitHubChangelogReleaseCache.Write(storage, new[] { sampleRelease("2026.1102.0-ez2lazer", "cached-body") });

            string path = storage.GetFullPath("EzData/github-changelog-releases.json", createIfNotExisting: false);
            var before = JsonConvert.DeserializeObject<EzGitHubChangelogReleaseCache.CachedFile>(File.ReadAllText(path))!;

            var backoffUntil = DateTimeOffset.UtcNow.AddHours(1);
            EzGitHubChangelogReleaseCache.ApplyApiBackoff(storage, backoffUntil);

            var after = JsonConvert.DeserializeObject<EzGitHubChangelogReleaseCache.CachedFile>(File.ReadAllText(path))!;

            Assert.That(after.FetchedAt, Is.EqualTo(before.FetchedAt));
            Assert.That(after.Releases, Has.Count.EqualTo(1));
            Assert.That(after.ApiBackoffUntil, Is.EqualTo(backoffUntil));
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

        [Test]
        public async Task TestFetchBuildsAsyncServesStaleCacheDuringApiBackoff()
        {
            using var storage = new TemporaryNativeStorage(nameof(TestFetchBuildsAsyncServesStaleCacheDuringApiBackoff));

            const string tag = "2026.1102.0-ez2lazer";
            EzGitHubChangelogReleaseCache.Write(storage, new[] { sampleRelease(tag, "stale-markdown") });

            string path = storage.GetFullPath("EzData/github-changelog-releases.json", createIfNotExisting: false);
            var cached = JsonConvert.DeserializeObject<EzGitHubChangelogReleaseCache.CachedFile>(File.ReadAllText(path))!;
            cached.FetchedAt = DateTimeOffset.UtcNow.AddDays(-2);
            cached.ApiBackoffUntil = DateTimeOffset.UtcNow.AddHours(1);
            File.WriteAllText(path, JsonConvert.SerializeObject(cached));

            var catalogue = new EzGitHubChangelogCatalogue();
            var builds = await catalogue.FetchBuildsAsync(storage, CancellationToken.None).ConfigureAwait(false);

            Assert.That(builds, Has.Count.EqualTo(1));
            Assert.That(catalogue.GetBody(tag), Is.EqualTo("stale-markdown"));
        }

        [Test]
        public async Task TestFetchBuildsAsyncDownloadsReleaseBundleWhenCacheStale()
        {
            const string bundleTag = "2026.1122.0-ez2lazer";
            string bundleJson = JsonConvert.SerializeObject(new EzGitHubChangelogReleaseCache.CachedFile
            {
                FetchedAt = DateTimeOffset.UtcNow,
                Releases = new List<GitHubRelease> { sampleRelease(bundleTag, "bundle-body") },
            });

            using var listener = new HttpListener();
            listener.Prefixes.Add("http://127.0.0.1:18765/");
            listener.Start();

            var serveTask = Task.Run(async () =>
            {
                var context = await listener.GetContextAsync().ConfigureAwait(false);
                byte[] payload = Encoding.UTF8.GetBytes(bundleJson);
                context.Response.ContentType = "application/json";
                context.Response.ContentLength64 = payload.Length;
                await context.Response.OutputStream.WriteAsync(payload).ConfigureAwait(false);
                context.Response.Close();
            });

            EzGitHubChangelogCatalogue.ReleaseChangelogBundleUrlOverrideForTests = "http://127.0.0.1:18765/changelog-releases.json";

            using var storage = new TemporaryNativeStorage(nameof(TestFetchBuildsAsyncDownloadsReleaseBundleWhenCacheStale));
            EzGitHubChangelogReleaseCache.Write(storage, new[] { sampleRelease("2026.1102.0-ez2lazer", "stale") });

            string path = storage.GetFullPath("EzData/github-changelog-releases.json", createIfNotExisting: false);
            var cached = JsonConvert.DeserializeObject<EzGitHubChangelogReleaseCache.CachedFile>(File.ReadAllText(path))!;
            cached.FetchedAt = DateTimeOffset.UtcNow.AddDays(-1);
            File.WriteAllText(path, JsonConvert.SerializeObject(cached));

            try
            {
                var catalogue = new EzGitHubChangelogCatalogue();
                var builds = await catalogue.FetchBuildsAsync(storage, CancellationToken.None).ConfigureAwait(false);

                await serveTask.ConfigureAwait(false);

                Assert.That(builds.Any(b => b.DisplayVersion == bundleTag), Is.True);
                Assert.That(catalogue.GetBody(bundleTag), Is.EqualTo("bundle-body"));
            }
            finally
            {
                listener.Stop();
                listener.Close();
            }
        }
    }
}
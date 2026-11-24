// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NUnit.Framework;
using osu.Framework.Platform;
using osu.Game.EzOsuGame;
using osu.Game.EzOsuGame.Changelog;
using osu.Game.Updater;

namespace osu.Game.Tests.EzOsuGame.Changelog
{
    [TestFixture]
    public class EzGitHubChangelogReleaseCacheTest
    {
        private static GitHubRelease sampleRelease(string tag, string body) => new GitHubRelease
        {
            TagName = tag,
            Body = body,
            PublishedAt = new DateTime(2026, 11, 22, 0, 0, 0, DateTimeKind.Utc),
        };

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
        public void TestTryReadStaleWhenFetchedAtExpired()
        {
            using var storage = new TemporaryNativeStorage(nameof(TestTryReadStaleWhenFetchedAtExpired));

            EzGitHubChangelogReleaseCache.Write(storage, new[] { sampleRelease("2026.1102.0-ez2lazer", "body") });

            string path = storage.GetFullPath("EzData/github-changelog-releases.json", createIfNotExisting: false);
            var cached = JsonConvert.DeserializeObject<EzGitHubChangelogReleaseCache.CachedFile>(File.ReadAllText(path))!;
            cached.FetchedAt = DateTimeOffset.UtcNow.AddHours(-7);
            File.WriteAllText(path, JsonConvert.SerializeObject(cached));

            Assert.That(
                EzGitHubChangelogReleaseCache.TryRead(storage, out _, out var fresh, out _),
                Is.True);
            Assert.That(fresh, Is.False);
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
        public void TestBundleJsonDeserializesIntoCacheFileShape()
        {
            const string json = """
                                {
                                  "fetchedAt": "2026-11-22T00:00:00+00:00",
                                  "releases": [
                                    {
                                      "tag_name": "2026.1122.0-ez2lazer",
                                      "body": "## zh\n\ntest",
                                      "published_at": "2026-11-22T00:00:00Z",
                                      "prerelease": false
                                    }
                                  ]
                                }
                                """;

            var file = JsonConvert.DeserializeObject<EzGitHubChangelogReleaseCache.CachedFile>(json);

            Assert.That(file, Is.Not.Null);
            Assert.That(file!.Releases, Has.Count.EqualTo(1));
            Assert.That(file.Releases[0].TagName, Is.EqualTo("2026.1122.0-ez2lazer"));
            Assert.That(file.Releases[0].Body, Does.Contain("test"));
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
    }
}
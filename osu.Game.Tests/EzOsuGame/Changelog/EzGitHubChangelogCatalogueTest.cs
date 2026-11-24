// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Game.EzOsuGame.Changelog;
using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Tests.EzOsuGame.Changelog
{
    [TestFixture]
    public class EzGitHubChangelogCatalogueTest
    {
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
    }
}

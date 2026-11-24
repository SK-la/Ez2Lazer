// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Game.EzOsuGame;
using osu.Game.Online.API;

namespace osu.Game.Tests.EzOsuGame
{
    [TestFixture]
    public class EzGitHubRequestHelperTest
    {
        [Test]
        public void TestConfigureGitHubRequestUsesEnvironmentToken()
        {
            string? savedEz = Environment.GetEnvironmentVariable("EZ2LAZER_GITHUB_TOKEN");
            string? savedGithub = Environment.GetEnvironmentVariable("GITHUB_TOKEN");

            try
            {
                Environment.SetEnvironmentVariable("EZ2LAZER_GITHUB_TOKEN", null);
                Environment.SetEnvironmentVariable("GITHUB_TOKEN", "unit-test-token");

                var request = new OsuWebRequest("https://api.github.com/repos/SK-la/Ez2Lazer/releases");
                Assert.DoesNotThrow(() => EzGitHubRequestHelper.ConfigureGitHubRequest(request));
            }
            finally
            {
                Environment.SetEnvironmentVariable("EZ2LAZER_GITHUB_TOKEN", savedEz);
                Environment.SetEnvironmentVariable("GITHUB_TOKEN", savedGithub);
            }
        }
    }
}
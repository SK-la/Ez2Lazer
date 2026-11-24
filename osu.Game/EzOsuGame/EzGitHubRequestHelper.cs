// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.IO.Network;

namespace osu.Game.EzOsuGame
{
    public static class EzGitHubRequestHelper
    {
        public static void ConfigureGitHubRequest(WebRequest request)
        {
            request.AddHeader("Accept", "application/vnd.github+json");

            string? token = Environment.GetEnvironmentVariable("EZ2LAZER_GITHUB_TOKEN")
                            ?? Environment.GetEnvironmentVariable("GITHUB_TOKEN");

            if (!string.IsNullOrWhiteSpace(token))
                request.AddHeader("Authorization", $"Bearer {token.Trim()}");
        }
    }
}
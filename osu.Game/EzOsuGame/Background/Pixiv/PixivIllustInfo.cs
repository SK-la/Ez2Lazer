// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.EzOsuGame.Background.Pixiv
{
    public readonly struct PixivIllustInfo
    {
        public string Account { get; }
        public long IllustId { get; }
        public int Page { get; }
        public string ImageUrl { get; }
        public int SanityLevel { get; }
        public string[] Tags { get; }
        public string AttributionLabel => $"{Account}_{IllustId}";

        public PixivIllustInfo(string account, long illustId, int page, string imageUrl, int sanityLevel = 0, string[]? tags = null)
        {
            Account = account;
            IllustId = illustId;
            Page = page;
            ImageUrl = imageUrl;
            SanityLevel = sanityLevel;
            Tags = tags ?? Array.Empty<string>();
        }
    }
}

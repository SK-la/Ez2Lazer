// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.EzOsuGame.Background.Pixiv
{
    public readonly struct PixivIllustInfo
    {
        public string Account { get; }
        public string UserName { get; }
        public long IllustId { get; }
        public int Page { get; }
        public string ImageUrl { get; }
        public int SanityLevel { get; }
        public string[] Tags { get; }
        public string IllustType { get; }
        public int Width { get; }
        public int Height { get; }
        public int IllustAiType { get; }
        public int XRestrict { get; }
        public string AttributionLabel => $"{(string.IsNullOrWhiteSpace(UserName) ? Account : UserName)}_{IllustId}";

        public PixivIllustInfo(
            string account,
            long illustId,
            int page,
            string imageUrl,
            int sanityLevel = 0,
            string[]? tags = null,
            string illustType = "illust",
            int width = 0,
            int height = 0,
            int illustAiType = 0,
            int xRestrict = 0,
            string userName = "")
        {
            Account = account;
            UserName = userName;
            IllustId = illustId;
            Page = page;
            ImageUrl = imageUrl;
            SanityLevel = sanityLevel;
            Tags = tags ?? Array.Empty<string>();
            IllustType = illustType;
            Width = width;
            Height = height;
            IllustAiType = illustAiType;
            XRestrict = xRestrict;
        }
    }
}

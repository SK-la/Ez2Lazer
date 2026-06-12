// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.EzOsuGame.Background.Pixiv
{
    internal static class PixivFilterListParser
    {
        public static string[] Parse(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return Array.Empty<string>();

            return raw.Split(new[] { '\r', '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
    }
}

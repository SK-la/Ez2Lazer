// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;

namespace osu.Game.EzOsuGame.Background.Pixiv
{
    internal static class PixivFilterListParser
    {
        private static readonly char[] comma_delimiters = { ',', ';', '\r', '\n' };
        private static readonly char[] space_delimiters = { ' ', '\t' };

        public static string[] Parse(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return Array.Empty<string>();

            if (raw.IndexOfAny(comma_delimiters) >= 0)
            {
                var results = new List<string>();

                foreach (string segment in raw.Split(comma_delimiters, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    results.Add(segment);

                return results.ToArray();
            }

            return raw.Split(space_delimiters, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
    }
}

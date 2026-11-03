// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Background.Pixiv
{
    public static class PixivAccountNormalizer
    {
        /// <summary>
        /// Strips promotional @ suffixes from Pixiv handles (e.g. "Anmi@画集発売中" → "Anmi").
        /// </summary>
        public static string Normalize(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            string trimmed = raw.Trim();
            int atIndex = trimmed.IndexOf('@');

            if (atIndex >= 0)
                trimmed = trimmed.Substring(0, atIndex);

            return trimmed.Trim();
        }
    }
}

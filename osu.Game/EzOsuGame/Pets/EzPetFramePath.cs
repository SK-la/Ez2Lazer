// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace osu.Game.EzOsuGame.Pets
{
    public static class EzPetFramePath
    {
        public const int MAX_FRAMES = 60;

        private static readonly Regex index_placeholder = new Regex(@"\{(0+)\}", RegexOptions.Compiled);

        public static bool HasIndexPlaceholder(string template)
            => !string.IsNullOrEmpty(template) && index_placeholder.IsMatch(template);

        public static string Format(string template, int frameIndex)
        {
            if (string.IsNullOrEmpty(template))
                return string.Empty;

            return index_placeholder.Replace(template, m =>
            {
                int width = Math.Clamp(m.Groups[1].Value.Length, 1, 9);
                return frameIndex.ToString($"D{width}", CultureInfo.InvariantCulture);
            });
        }

        /// <summary>
        /// Enumerates frame names (no extension) until <paramref name="exists"/> fails or <see cref="MAX_FRAMES"/> is reached.
        /// A template without an index placeholder is treated as a single frame.
        /// </summary>
        public static IReadOnlyList<string> Enumerate(string template, Func<string, bool> exists)
        {
            var names = new List<string>();

            if (string.IsNullOrWhiteSpace(template))
                return names;

            if (!HasIndexPlaceholder(template))
            {
                if (exists(template))
                    names.Add(template);

                return names;
            }

            for (int i = 0; i < MAX_FRAMES; i++)
            {
                string name = Format(template, i);

                if (!exists(name))
                    break;

                names.Add(name);
            }

            return names;
        }
    }
}

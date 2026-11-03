// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace osu.Game.EzOsuGame.Pets
{
    public static class EzPetFramePath
    {
        public const int MAX_FRAMES = 120;

        /// <summary>
        /// Matches <c>_000</c>, <c>_00</c>, <c>_12</c> immediately before the extension.
        /// Preferred on-disk suffix is three digits: <c>_000</c> … <c>_095</c>.
        /// </summary>
        private static readonly Regex suffix_index = new Regex(@"_(\d+)\.(png|jpg|jpeg)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static bool TryGetFrameIndex(string fileName, out int index)
        {
            index = -1;

            if (string.IsNullOrEmpty(fileName))
                return false;

            var match = suffix_index.Match(Path.GetFileName(fileName));
            if (!match.Success)
                return false;

            return int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out index);
        }

        /// <summary>
        /// Picks one file per index (lowest name wins), then returns names without extension in index order.
        /// </summary>
        public static IReadOnlyList<string> CollectIndexedFrameNames(IEnumerable<string> fileNames)
        {
            var byIndex = new SortedDictionary<int, string>();

            foreach (string fileName in fileNames)
            {
                if (!TryGetFrameIndex(fileName, out int index))
                    continue;

                if (index < 0)
                    continue;

                string withoutExtension = Path.GetFileNameWithoutExtension(fileName);
                if (string.IsNullOrEmpty(withoutExtension))
                    continue;

                if (!byIndex.TryGetValue(index, out string? existing) || string.CompareOrdinal(withoutExtension, existing) < 0)
                    byIndex[index] = withoutExtension;
            }

            var names = new List<string>(Math.Min(byIndex.Count, MAX_FRAMES));

            foreach ((_, string name) in byIndex)
            {
                names.Add(name);
                if (names.Count >= MAX_FRAMES)
                    break;
            }

            return names;
        }

        public static string ToSnakeCase(string clipName)
        {
            if (string.IsNullOrEmpty(clipName))
                return string.Empty;

            var builder = new StringBuilder(clipName.Length + 4);

            for (int i = 0; i < clipName.Length; i++)
            {
                char c = clipName[i];

                if (i > 0 && char.IsUpper(c) && !char.IsUpper(clipName[i - 1]))
                    builder.Append('_');

                builder.Append(char.ToLowerInvariant(c));
            }

            return builder.ToString();
        }
    }
}

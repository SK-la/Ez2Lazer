// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using osu.Game.EzOsuGame.Changelog;

namespace osu.Game.EzOsuGame.Wiki
{
    public static class EzWikiSidebarParser
    {
        private static readonly Regex locale_heading = new Regex(
            @"^#{2,3}\s+(zh|en|中文|english)\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static string ExtractLocaleSection(string? markdown, EzContentLocale locale)
        {
            if (string.IsNullOrWhiteSpace(markdown))
                return string.Empty;

            string[] lines = markdown.Replace("\r\n", "\n").Split('\n');
            int start = findSectionStart(lines, locale);

            if (start < 0)
                return markdown.Trim();

            int end = lines.Length;

            for (int i = start; i < lines.Length; i++)
            {
                var match = locale_heading.Match(lines[i]);

                if (!match.Success)
                    continue;

                if (!headingMatchesLocale(match.Groups[1].Value, locale))
                {
                    end = i;
                    break;
                }
            }

            var section = new List<string>();

            for (int i = start; i < end; i++)
            {
                if (locale_heading.IsMatch(lines[i]))
                    continue;

                section.Add(lines[i]);
            }

            return string.Join('\n', section).Trim();
        }

        private static int findSectionStart(string[] lines, EzContentLocale locale)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                var match = locale_heading.Match(lines[i]);

                if (match.Success && headingMatchesLocale(match.Groups[1].Value, locale))
                    return i + 1;
            }

            return -1;
        }

        private static bool headingMatchesLocale(string headingToken, EzContentLocale locale) =>
            locale switch
            {
                EzContentLocale.Zh => headingToken.Equals("zh", StringComparison.OrdinalIgnoreCase)
                                      || headingToken == "中文",
                EzContentLocale.En => headingToken.Equals("en", StringComparison.OrdinalIgnoreCase)
                                      || headingToken.Equals("english", StringComparison.OrdinalIgnoreCase),
                _ => false,
            };
    }
}

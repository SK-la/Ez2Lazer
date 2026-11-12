// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Text.RegularExpressions;

namespace osu.Game.EzOsuGame.Wiki
{
    public static class EzWikiMarkdownRewrite
    {
        private static readonly Regex relative_wiki_link = new Regex(@"\]\(\./([^)#]+)\)", RegexOptions.Compiled);

        public static string RewritePhysicalLinks(string markdown, EzWikiLocalePages pages)
        {
            if (string.IsNullOrEmpty(markdown))
                return markdown;

            return relative_wiki_link.Replace(markdown, match =>
            {
                string target = Uri.UnescapeDataString(match.Groups[1].Value.Trim());

                if (target.StartsWith('@'))
                    return match.Value;

                string? logical = pages.TryGetLogicalKey(target);
                return logical == null ? match.Value : $"](./@{logical})";
            });
        }
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using osu.Game.EzOsuGame;

namespace osu.Game.EzOsuGame.Background.Pixiv
{
    public static class PixivFileNamer
    {
        private static readonly Regex invalid_chars = new Regex(@"[\\/:*?""<>|]", RegexOptions.Compiled);

        public static string SanitizeAccount(string account)
        {
            if (string.IsNullOrWhiteSpace(account))
                return "unknown";

            string sanitized = invalid_chars.Replace(account.Trim(), "_");
            return string.IsNullOrWhiteSpace(sanitized) ? "unknown" : sanitized;
        }

        public static string BuildFileName(string account, long illustId, int page, string extension = ".png")
        {
            if (!extension.StartsWith('.'))
                extension = "." + extension;

            return $"{SanitizeAccount(account)}_{illustId}_p{page}{extension}";
        }

        public static string BuildRelativePath(string account, long illustId, int page, string extension = ".png")
            => Path.Combine(EzModifyPath.BG_PIXIV_PATH, BuildFileName(account, illustId, page, extension)).Replace('\\', '/');

        public static bool TryParseFileName(string fileName, out string account, out long illustId, out int page)
        {
            account = string.Empty;
            illustId = 0;
            page = 0;

            string name = Path.GetFileNameWithoutExtension(fileName);
            if (string.IsNullOrEmpty(name))
                return false;

            int pageIndex = name.LastIndexOf("_p", StringComparison.Ordinal);
            if (pageIndex < 0)
                return false;

            if (!int.TryParse(name.AsSpan(pageIndex + 2), out page))
                return false;

            string prefix = name.Substring(0, pageIndex);
            int idIndex = prefix.LastIndexOf('_');
            if (idIndex < 0)
                return false;

            if (!long.TryParse(prefix.AsSpan(idIndex + 1), out illustId))
                return false;

            account = prefix.Substring(0, idIndex);
            return !string.IsNullOrEmpty(account);
        }

        public static bool IsSupportedImageExtension(string fileName)
        {
            string ext = Path.GetExtension(fileName);
            return ext.Equals(".png", StringComparison.OrdinalIgnoreCase)
                   || ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                   || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
                   || ext.Equals(".webp", StringComparison.OrdinalIgnoreCase);
        }

        public static string GetExtensionFromUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return ".png";

            string path = url.Split('?').First();
            string ext = Path.GetExtension(path);

            return string.IsNullOrEmpty(ext) ? ".png" : ext.ToLowerInvariant();
        }
    }
}

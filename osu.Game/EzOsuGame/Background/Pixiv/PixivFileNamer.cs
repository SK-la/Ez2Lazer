// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace osu.Game.EzOsuGame.Background.Pixiv
{
    public static class PixivFileNamer
    {
        private static readonly Regex invalid_chars = new Regex(@"[\\/:*?""<>|]", RegexOptions.Compiled);

        public static string GetDisplayLabel(PixivIllustInfo illust) => string.IsNullOrWhiteSpace(illust.UserName) ? illust.Account : illust.UserName;

        public static string SanitizeFileLabel(string label)
        {
            label = PixivAccountNormalizer.Normalize(label);

            if (string.IsNullOrWhiteSpace(label))
                return "unknown";

            string sanitized = invalid_chars.Replace(label, "_");
            return string.IsNullOrWhiteSpace(sanitized) ? "unknown" : sanitized;
        }

        /// <summary>
        /// On-disk download name, e.g. ヒトこもる_145826326_p0.png
        /// </summary>
        public static string BuildDownloadFileName(PixivIllustInfo illust, string? extension = null)
        {
            extension ??= GetExtensionFromUrl(illust.ImageUrl);

            if (!extension.StartsWith('.'))
                extension = "." + extension;

            return $"{SanitizeFileLabel(GetDisplayLabel(illust))}_{illust.IllustId}_p{illust.Page}{extension}";
        }

        public static string BuildDownloadRelativePath(PixivIllustInfo illust, string? extension = null) =>
            Path.Combine(EzModifyPath.BG_PIXIV_PATH, BuildDownloadFileName(illust, extension)).Replace('\\', '/');

        /// <summary>
        /// Id-only key for queue / dedupe lookups, e.g. 145826326_p0.png
        /// </summary>
        public static string BuildIdKeyFileName(long illustId, int page, string extension = ".png")
        {
            if (!extension.StartsWith('.'))
                extension = "." + extension;

            return $"{illustId}_p{page}{extension}";
        }

        public static bool TryParseFileName(string fileName, out long illustId, out int page)
        {
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

            string idPart = name.Substring(0, pageIndex);

            if (long.TryParse(idPart, out illustId))
                return true;

            int idIndex = idPart.LastIndexOf('_');
            if (idIndex < 0)
                return false;

            return long.TryParse(idPart.AsSpan(idIndex + 1), out illustId);
        }

        public static bool TryParseFileLabel(string fileName, out string fileLabel)
        {
            fileLabel = string.Empty;

            string name = Path.GetFileNameWithoutExtension(fileName);
            if (string.IsNullOrEmpty(name))
                return false;

            int pageIndex = name.LastIndexOf("_p", StringComparison.Ordinal);
            if (pageIndex < 0)
                return false;

            string idPart = name.Substring(0, pageIndex);

            if (long.TryParse(idPart, out _))
                return false;

            int idIndex = idPart.LastIndexOf('_');
            if (idIndex < 0)
                return false;

            fileLabel = idPart.Substring(0, idIndex);
            return fileLabel.Length > 0;
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

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;

namespace osu.Game.EzOsuGame
{
    /// <summary>
    /// EzResources 逻辑路径工具：统一以 <c>'/'</c> 分隔，负责 <c>directory/name</c> 的规范化、拆分与拼接。
    /// 所有传给 <see cref="EzResourceStore"/> 的路径都先经此处规整，故下层不必重复处理分隔符差异。
    /// </summary>
    internal static class EzResourcePath
    {
        /// <summary>
        /// 规范化：反斜杠转正斜杠、去掉首尾斜杠。已是规范形式时原样返回（不分配）。
        /// </summary>
        public static string Normalise(string? path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            return path.IndexOf('\\') >= 0
                ? path.Replace('\\', '/').Trim('/')
                : path.Trim('/');
        }

        /// <summary>
        /// 拆成「目录」与「末段名」。无目录时 <paramref name="directory"/> 为空串。
        /// </summary>
        public static void Split(string path, out string directory, out string name)
        {
            string normalised = Normalise(path);
            int slash = normalised.LastIndexOf('/');

            if (slash < 0)
            {
                directory = string.Empty;
                name = normalised;
            }
            else
            {
                directory = normalised[..slash];
                name = normalised[(slash + 1)..];
            }
        }

        /// <summary>
        /// 拼接目录与末段名，任一侧为空则退化为另一侧。
        /// </summary>
        public static string Combine(string directory, string name)
        {
            if (directory.Length == 0)
                return name;

            return name.Length == 0 ? directory : $"{directory}/{name}";
        }

        /// <summary>
        /// 取文件名（含后缀），用于把 <c>Storage</c> 返回的平台分隔路径折成逻辑名。
        /// </summary>
        public static string FileName(string path) => Path.GetFileName(path);

        /// <summary>
        /// 去掉后缀；无后缀时原样返回。
        /// </summary>
        public static string Stem(string fileName)
        {
            int dot = fileName.LastIndexOf('.');

            // 前导点（如 .DS_Store）不算后缀。
            return dot <= 0 ? fileName : fileName[..dot];
        }

        /// <summary>
        /// 取后缀（含点，小写）；无后缀时返回空串。
        /// </summary>
        public static string Extension(string fileName)
        {
            int dot = fileName.LastIndexOf('.');

            return dot <= 0 ? string.Empty : fileName[dot..].ToLowerInvariant();
        }

        /// <summary>
        /// 是否为可交给纹理加载链的图片文件（无后缀、非图片后缀一律否）。
        /// </summary>
        public static bool IsImage(string fileName, out string extension)
        {
            extension = Extension(fileName);

            return extension is ".png" or ".jpg" or ".jpeg" or ".gif";
        }
    }
}

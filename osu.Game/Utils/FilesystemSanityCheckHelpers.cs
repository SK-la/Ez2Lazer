// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Text;
using osu.Game.Extensions;

namespace osu.Game.Utils
{
    public static class FilesystemSanityCheckHelpers
    {
        private const string fallback_archive_entry_name = "file";

        /// <summary>
        /// Returns whether <paramref name="path"/> is potentially susceptible to path traversal style attacks.
        /// </summary>
        public static bool IncursPathTraversalRisk(string path)
            => path.Contains("../", StringComparison.Ordinal) || path.Contains("..\\", StringComparison.Ordinal) || Path.IsPathRooted(path);

        /// <summary>
        /// Returns an archive entry name that cannot escape the extract directory.
        /// Names that are already safe are returned unchanged.
        /// </summary>
        public static string GetSafeArchiveEntryName(string path)
        {
            if (!entryEscapesArchive(path))
                return path;

            string standardised = path.Replace('\\', '/');
            var builder = new StringBuilder();

            foreach (string piece in standardised.Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                if (piece == "." || piece == "..")
                    continue;

                string cleaned = piece.GetValidFilename();

                if (cleaned.Length == 0)
                    continue;

                if (builder.Length > 0)
                    builder.Append('/');

                builder.Append(cleaned);
            }

            return builder.Length > 0 ? builder.ToString() : fallback_archive_entry_name;
        }

        // Path.IsPathRooted misses Windows drive prefixes when this runs on a non-Windows host.
        private static bool entryEscapesArchive(string path)
        {
            if (IncursPathTraversalRisk(path))
                return true;

            if (path.Length > 0 && (path[0] == '/' || path[0] == '\\'))
                return true;

            if (path.Length >= 2 && isAsciiLetter(path[0]) && path[1] == ':')
                return true;

            if (!path.Contains("..", StringComparison.Ordinal))
                return false;

            foreach (string piece in path.Split('/', '\\', StringSplitOptions.RemoveEmptyEntries))
            {
                if (piece == "..")
                    return true;
            }

            return false;
        }

        private static bool isAsciiLetter(char c) => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');

        /// <summary>
        /// Returns whether <paramref name="child"/> is a subdirectory (direct or nested) of <paramref name="parent"/>.
        /// </summary>
        public static bool IsSubDirectory(string parent, string child)
        {
            // `Path.GetFullPath()` invocations are required to fully resolve the paths to unambiguous downwards-traversal-only paths.
            var parentInfo = new DirectoryInfo(Path.GetFullPath(parent));
            var childInfo = new DirectoryInfo(Path.GetFullPath(child));

            while (childInfo != null)
            {
                if (parentInfo.FullName == childInfo.FullName)
                    return true;

                childInfo = childInfo.Parent;
            }

            return false;
        }
    }
}

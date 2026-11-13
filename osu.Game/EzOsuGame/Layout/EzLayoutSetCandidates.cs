// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Platform;

namespace osu.Game.EzOsuGame.Layout
{
    /// <summary>
    /// Lists sibling folders under <see cref="EzModifyPath.CONFIG_ROOT_PATH"/>: default <c>layout</c> and
    /// <c>layout-*</c> theme packs (not subdirectories of <c>layout</c>). Rescan only updates the list.
    /// </summary>
    public static class EzLayoutSetCandidates
    {
        private static readonly List<string> cached = new List<string>();

        public static event Action? Changed;

        public static IReadOnlyList<string> Items => cached;

        public static void Refresh(Storage storage)
        {
            cached.Clear();

            try
            {
                string path = storage.GetFullPath(EzModifyPath.CONFIG_ROOT_PATH);

                if (Directory.Exists(path))
                {
                    foreach (string directory in Directory.GetDirectories(path))
                    {
                        string name = Path.GetFileName(directory);

                        if (EzLayoutSetNames.IsLayoutSetDirectory(name))
                            cached.Add(name);
                    }
                }
            }
            catch
            {
            }

            cached.Sort(StringComparer.OrdinalIgnoreCase);

            int defaultIndex = cached.FindIndex(n => n.Equals(EzModifyPath.LAYOUT_SET_DEFAULT, StringComparison.OrdinalIgnoreCase));

            if (defaultIndex > 0)
            {
                cached.RemoveAt(defaultIndex);
                cached.Insert(0, EzModifyPath.LAYOUT_SET_DEFAULT);
            }
            else if (defaultIndex < 0)
                cached.Insert(0, EzModifyPath.LAYOUT_SET_DEFAULT);

            Changed?.Invoke();
        }

        public static void EnsureListed(string layoutSetName)
        {
            string normalized = EzLayoutSetNames.Normalize(layoutSetName);

            if (!cached.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                cached.Add(normalized);
        }

        public static void EnsureInitialSelection(Bindable<string> current, Storage storage)
        {
            Refresh(storage);

            if (string.IsNullOrEmpty(current.Value))
            {
                current.Value = EzModifyPath.LAYOUT_SET_DEFAULT;
                return;
            }

            string normalized = EzLayoutSetNames.Normalize(current.Value);

            if (!cached.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                EnsureListed(normalized);
                return;
            }

            if (!current.Value.Equals(normalized, StringComparison.Ordinal))
                current.Value = normalized;
        }
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Platform;
using osu.Game.EzOsuGame.HUD;

namespace osu.Game.EzOsuGame.Overlays
{
    /// <summary>
    /// <see cref="EzModifyPath.GAME_THEME_PATH"/> 子目录缓存；重扫只更新列表，不改当前选中值。
    /// </summary>
    public static class EzGameThemeCandidates
    {
        private static readonly List<string> cached = new List<string>();

        public static event Action? Changed;

        public static IReadOnlyList<string> Items => cached;

        public static void Refresh(Storage storage)
        {
            cached.Clear();
            cached.AddRange(EzResourceDiscovery.ListGameThemeCandidates(storage));
            Changed?.Invoke();
        }

        public static List<string> CreateDropdownItems(Bindable<string> current)
        {
            var items = cached.ToList();
            ensureCurrentListed(items, current.Value);

            if (items.Count == 0)
                items.Add(EzGameThemeNames.DEFAULT);

            return items;
        }

        public static List<string> CreateCardItems(string? selected)
        {
            var items = cached.ToList();
            ensureCurrentListed(items, selected);
            return items;
        }

        public static void EnsureInitialSelection(Bindable<string> current)
        {
            if (!string.IsNullOrEmpty(current.Value))
                return;

            current.Value = cached.FirstOrDefault() ?? EzGameThemeNames.DEFAULT;
        }

        private static void ensureCurrentListed(List<string> items, string? value)
        {
            string resolved = EzGameThemeNames.Resolve(value);

            if (!string.IsNullOrEmpty(resolved) && !items.Contains(resolved))
                items.Insert(0, resolved);
        }
    }
}

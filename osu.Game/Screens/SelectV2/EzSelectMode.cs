// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.Screens.SelectV2
{
    public class EzSelectModeInfo
    {
        public required string Id { get; set; }
        public required string DisplayName { get; set; }
        public float? KeyCount { get; set; }
        public bool IsDefault { get; set; }
    }

    public static class EzSelectModes
    {
        private const int mania_ruleset_id = 3;

        public static readonly List<EzSelectModeInfo> ALL = new List<EzSelectModeInfo>
        {
            new EzSelectModeInfo { Id = "All", DisplayName = "All", IsDefault = true },
            new EzSelectModeInfo { Id = "CS1", DisplayName = "1", KeyCount = 1 },
            new EzSelectModeInfo { Id = "CS2", DisplayName = "2", KeyCount = 2 },
            new EzSelectModeInfo { Id = "CS3", DisplayName = "3", KeyCount = 3 },
            new EzSelectModeInfo { Id = "CS4", DisplayName = "4", KeyCount = 4 },
            new EzSelectModeInfo { Id = "CS5", DisplayName = "5", KeyCount = 5 },
            new EzSelectModeInfo { Id = "CS6", DisplayName = "6", KeyCount = 6 },
            new EzSelectModeInfo { Id = "CS7", DisplayName = "7", KeyCount = 7 },
            new EzSelectModeInfo { Id = "CS8", DisplayName = "8", KeyCount = 8 },
            new EzSelectModeInfo { Id = "CS9", DisplayName = "9", KeyCount = 9 },
            new EzSelectModeInfo { Id = "CS10", DisplayName = "10", KeyCount = 10 },
            new EzSelectModeInfo { Id = "CS12", DisplayName = "12", KeyCount = 12 },
            new EzSelectModeInfo { Id = "CS14", DisplayName = "14", KeyCount = 14 },
            new EzSelectModeInfo { Id = "CS16", DisplayName = "16", KeyCount = 16 },
            new EzSelectModeInfo { Id = "CS18", DisplayName = "18", KeyCount = 18 },
        };

        public static List<EzSelectModeInfo> GetModesForRuleset(int rulesetId)
        {
            if (rulesetId == mania_ruleset_id)
                return ALL.Where(m => m.KeyCount == null || m.KeyCount >= 4).ToList();

            return ALL.Where(m => m.KeyCount == null || m.KeyCount <= 10).ToList();
        }

        public static EzSelectModeInfo? GetById(string id) => ALL.FirstOrDefault(m => m.Id == id);
    }

    public class MultiEzSelectMode
    {
        public HashSet<string> SelectedModeIds { get; } = new HashSet<string> { "All" };

        public event Action? SelectionChanged;

        public void SetSelection(HashSet<string> modeIds)
        {
            SelectedModeIds.Clear();
            SelectedModeIds.UnionWith(modeIds.Count == 0 ? new[] { "All" } : modeIds);
            SelectionChanged?.Invoke();
        }
    }
}

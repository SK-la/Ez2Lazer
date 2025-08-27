// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.Screens.SelectV2
{
    public class EzKeyModeInfo
    {
        public required string Id { get; set; }           // 唯一标识
        public required string DisplayName { get; set; }  // 显示名
        public float? KeyCount { get; set; }     // 键数（可选）
        public bool IsDefault { get; set; }      // 是否默认
    }

    public static class EzKeyModes
    {
        private const int MANIA_RULESET_ID = 3;

        public static readonly List<EzKeyModeInfo> ALL = new List<EzKeyModeInfo>
        {
            new EzKeyModeInfo { Id = "All", DisplayName = "All", IsDefault = true },
            new EzKeyModeInfo { Id = "CS1", DisplayName = "1", KeyCount = 1 },
            new EzKeyModeInfo { Id = "CS2", DisplayName = "2", KeyCount = 2 },
            new EzKeyModeInfo { Id = "CS3", DisplayName = "3", KeyCount = 3 },
            new EzKeyModeInfo { Id = "CS4", DisplayName = "4", KeyCount = 4 },
            new EzKeyModeInfo { Id = "CS5", DisplayName = "5", KeyCount = 5 },
            new EzKeyModeInfo { Id = "CS6", DisplayName = "6", KeyCount = 6 },
            new EzKeyModeInfo { Id = "CS7", DisplayName = "7", KeyCount = 7 },
            new EzKeyModeInfo { Id = "CS8", DisplayName = "8", KeyCount = 8 },
            new EzKeyModeInfo { Id = "CS9", DisplayName = "9", KeyCount = 9 },
            new EzKeyModeInfo { Id = "CS10", DisplayName = "10", KeyCount = 10 },
            new EzKeyModeInfo { Id = "CS12", DisplayName = "12", KeyCount = 12 },
            new EzKeyModeInfo { Id = "CS14", DisplayName = "14", KeyCount = 14 },
            new EzKeyModeInfo { Id = "CS16", DisplayName = "16", KeyCount = 16 },
            new EzKeyModeInfo { Id = "CS18", DisplayName = "18", KeyCount = 18 },
        };

        public static List<EzKeyModeInfo> GetModesForRuleset(int rulesetId)
        {
            if (rulesetId == MANIA_RULESET_ID)
                return ALL.Where(m => m.KeyCount == null || m.KeyCount >= 4).ToList();

            return ALL.Where(m => m.KeyCount == null || m.KeyCount <= 10).ToList();
        }

        public static EzKeyModeInfo? GetById(string id) => ALL.FirstOrDefault(m => m.Id == id);
    }

    public class MultiSelectEzKeyMode
    {
        public HashSet<string> SelectedModeIds { get; } = new HashSet<string> { "All" };

        public event Action? SelectionChanged;

        public bool IsSelected(string keyModeId) => SelectedModeIds.Contains(keyModeId);

        public void ToggleSelection(string keyModeId, bool isMultiSelect = false)
        {
            if (keyModeId == "All")
            {
                SelectedModeIds.Clear();
                SelectedModeIds.Add("All");
            }
            else if (isMultiSelect)
            {
                handleMultiSelect(keyModeId);
            }
            else
            {
                handleSingleSelect(keyModeId);
            }

            SelectionChanged?.Invoke();
        }

        private void handleMultiSelect(string keyModeId)
        {
            if (SelectedModeIds.Contains("All"))
            {
                SelectedModeIds.Clear();
                SelectedModeIds.Add(keyModeId);
            }
            else
            {
                if (!SelectedModeIds.Remove(keyModeId))
                    SelectedModeIds.Add(keyModeId);

                if (SelectedModeIds.Count == 0)
                    SelectedModeIds.Add("All");
            }
        }

        private void handleSingleSelect(string keyModeId)
        {
            if (SelectedModeIds.Contains(keyModeId) && SelectedModeIds.Count == 1)
            {
                SelectedModeIds.Clear();
                SelectedModeIds.Add("All");
            }
            else
            {
                SelectedModeIds.Clear();
                SelectedModeIds.Add(keyModeId);
            }
        }

        public void SetSelection(HashSet<string> modeIds)
        {
            SelectedModeIds.Clear();
            SelectedModeIds.UnionWith(modeIds.Count == 0 ? new[] { "All" } : modeIds);
            SelectionChanged?.Invoke();
        }
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.Screens.LAsEzExtensions
{
    public enum EzSelectManiaSubset
    {
        All, // 默认子集
        Osu,
        Bms,
        Ez,
        Convertor,
    }

    public enum EzSelectMode
    {
        [Description("ALL")]
        All,

        [Description("4k")]
        Key4,

        [Description("5k")]
        Key5,

        [Description("6k")]
        Key6,

        [Description("7k")]
        Key7,

        [Description("8k")]
        Key8,

        [Description("9k")]
        Key9,

        [Description("10k")]
        Key10,

        [Description("12k")]
        Key12,

        [Description("14k")]
        Key14,

        [Description("16k")]
        Key16,

        [Description("18k")]
        Key18,
    }

    public static class EzModeHelper
    {
        public static double GetKeyCountFromEzMode(EzSelectMode mode)
        {
            return mode switch
            {
                EzSelectMode.Key4 => 4,
                EzSelectMode.Key5 => 5,
                EzSelectMode.Key6 => 6,
                EzSelectMode.Key7 => 7,
                EzSelectMode.Key8 => 8,
                EzSelectMode.Key9 => 9,
                EzSelectMode.Key10 => 10,
                EzSelectMode.Key12 => 12,
                EzSelectMode.Key14 => 14,
                EzSelectMode.Key16 => 16,
                EzSelectMode.Key18 => 18,
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
            };
        }

        /// <summary>
        /// 从多个选中的模式生成 CircleSize 过滤条件
        /// </summary>
        public static List<float> GetKeyCountsFromSelectedModes(HashSet<EzSelectMode> selectedModes)
        {
            if (selectedModes.Contains(EzSelectMode.All) || selectedModes.Count == 0)
                return new List<float>(); // 返回空列表表示无过滤条件

            return selectedModes.Where(mode => mode != EzSelectMode.All)
                                .Select(mode => (float)GetKeyCountFromEzMode(mode))
                                .ToList();
        }

        /// <summary>
        /// 检查给定的键数是否匹配任何选中的模式
        /// </summary>
        public static bool MatchesAnySelectedMode(HashSet<EzSelectMode> selectedModes, float circleSize)
        {
            if (selectedModes.Contains(EzSelectMode.All) || selectedModes.Count == 0)
                return true;

            var targetKeyCounts = GetKeyCountsFromSelectedModes(selectedModes);
            return targetKeyCounts.Contains(circleSize);
        }
    }

    /// <summary>
    /// 用于管理多选 EzSelectMode 状态的类
    /// </summary>
    public class MultiSelectEzMode
    {
        private readonly HashSet<EzSelectMode> selectedModes = new HashSet<EzSelectMode> { EzSelectMode.All };

        public event Action? SelectionChanged;

        public HashSet<EzSelectMode> SelectedModes => new HashSet<EzSelectMode>(selectedModes);

        public bool IsSelected(EzSelectMode mode) => selectedModes.Contains(mode);

        public void ToggleSelection(EzSelectMode mode, bool isRightClick = false)
        {
            if (mode == EzSelectMode.All)
            {
                // 点击 All 时清除其他所有选择
                selectedModes.Clear();
                selectedModes.Add(EzSelectMode.All);
            }
            else
            {
                if (isRightClick)
                {
                    // 右键多选逻辑
                    if (selectedModes.Contains(EzSelectMode.All))
                    {
                        // 如果当前是 All，切换到只选择这个模式
                        selectedModes.Clear();
                        selectedModes.Add(mode);
                    }
                    else
                    {
                        // 切换这个模式的选择状态
                        if (!selectedModes.Add(mode))
                        {
                            selectedModes.Remove(mode);
                            // 如果没有选择任何模式，回到 All
                            if (selectedModes.Count == 0)
                                selectedModes.Add(EzSelectMode.All);
                        }
                    }
                }
                else
                {
                    // 左键单选逻辑
                    if (selectedModes.Contains(mode) && selectedModes.Count == 1)
                    {
                        // 如果只选中这一个，切换到 All
                        selectedModes.Clear();
                        selectedModes.Add(EzSelectMode.All);
                    }
                    else
                    {
                        // 否则只选择这一个
                        selectedModes.Clear();
                        selectedModes.Add(mode);
                    }
                }
            }

            SelectionChanged?.Invoke();
        }

        public void SetSelection(HashSet<EzSelectMode> modes)
        {
            selectedModes.Clear();
            if (modes.Count == 0)
                selectedModes.Add(EzSelectMode.All);
            else
                selectedModes.UnionWith(modes);

            SelectionChanged?.Invoke();
        }
    }
}

//     public enum EzSelectMode
//     {
//         [Description("ALL")]
//         All,
//
//         [Description("ClubMix")]
//         ClubMix,
//
//         [Description("SpaceMix")]
//         SpaceMix,
//
//         [Description("14RadioMix")]
//         RadioMix14,
//
//         [Description("StreetMix")]
//         StreetMix,
//
//         [Description("7StreetMix")]
//         StreetMix7,
//
//         [Description("5Radio")]
//         Radio5,
//
//         [Description("7Radio")]
//         Radio7,
//
//         [Description("5KeyMix")]
//         KeyMix5,
//
//         [Description("RubyMix")]
//         RubyMix,
//
//         [Description("RadioMix")]
//         RadioMix,
//
//         [Description("ScratchMix")]
//         ScratchMix,
//
//         [Description("Catch")]
//         Catch,
//     }
// }
//
// public static string GenerateFilterString(List<EzSelectMode> modes)
// {
//     if (modes.Contains(EzSelectMode.All))
//         return string.Empty;
//
//     var keyCounts = modes.Select(GetKeyCountFromEzMode);
//     return string.Join(" ", keyCounts.Select(k => $"cs={k}"));
// }

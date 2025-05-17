// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.ComponentModel;

namespace osu.Game.Screens.Select.Filter
{
    public enum SelectManiaRulesetSubset
    {
        All, // 默认子集
        Osu,
        Bms,
        Ez,
        Convertor,
    }

    public enum SelectEzMode
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
        // public static string GetDescription(this SelectEzMode value)
        // {
        //     var field = value.GetType().GetField(value.ToString());
        //     if (field == null) return value.ToString();
        //
        //     var attribute = (DescriptionAttribute)field.GetCustomAttributes(typeof(DescriptionAttribute), false).FirstOrDefault()!;
        //     return attribute.Description;
        // }

        public static double GetKeyCountFromEzMode(SelectEzMode mode)
        {
            return mode switch
            {
                SelectEzMode.Key4 => 4,
                SelectEzMode.Key5 => 5,
                SelectEzMode.Key6 => 6,
                SelectEzMode.Key7 => 7,
                SelectEzMode.Key8 => 8,
                SelectEzMode.Key9 => 9,
                SelectEzMode.Key10 => 10,
                SelectEzMode.Key12 => 12,
                SelectEzMode.Key14 => 14,
                SelectEzMode.Key16 => 16,
                SelectEzMode.Key18 => 18,
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
            };
        }

        public static string GenerateFilterString(SelectEzMode mode)
        {
            if (mode == SelectEzMode.All) return string.Empty;

            double keyCount = GetKeyCountFromEzMode(mode);
            return $" cs={keyCount} ";
        }
    }
    //
    // public class Control
    // {
    //     private SelectEzMode selectedMode;
    //
    //     public void ToggleMode(SelectEzMode mode)
    //     {
    //         if (mode == SelectEzMode.All)
    //         {
    //             selectedMode = SelectEzMode.All;
    //         }
    //         else
    //         {
    //             selectedMode = mode;
    //         }
    //
    //         updateFilterString();
    //     }
    //
    //     private void updateFilterString()
    //     {
    //         string filterString = EzModeHelper.GenerateFilterString(selectedMode);
    //         // Console.WriteLine(filterString);
    //     }
    //
    //     public SelectEzMode GetSelectedMode() => selectedMode;
    // }
}

//     public enum SelectEzMode
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
// public static string GenerateFilterString(List<SelectEzMode> modes)
// {
//     if (modes.Contains(SelectEzMode.All))
//         return string.Empty;
//
//     var keyCounts = modes.Select(GetKeyCountFromEzMode);
//     return string.Join(" ", keyCounts.Select(k => $"cs={k}"));
// }

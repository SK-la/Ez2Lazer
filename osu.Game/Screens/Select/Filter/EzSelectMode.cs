// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.ComponentModel;

namespace osu.Game.Screens.Select.Filter
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

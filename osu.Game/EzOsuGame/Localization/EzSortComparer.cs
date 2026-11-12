// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using osu.Game.Utils;

namespace osu.Game.EzOsuGame.Localization
{
    /// <summary>
    /// Orders carousel text the way the current UI language reads it.
    /// Chinese UI compares with the zh-CN pinyin collation, so Han characters order by 音序 and sort before Latin text;
    /// every other language keeps the ordinal ordering.
    /// </summary>
    public static class EzSortComparer
    {
        private const string pinyin_initials = "ABCDEFGHJKLMNOPQRSTWXYZ";

        // One character per initial, carrying the first pinyin syllable of that initial. The list must stay ascending
        // under the zh-CN collation; the A-Z bucketing below relies on that order.
        private static readonly string[] pinyin_initial_probes =
        {
            "阿", "八", "擦", "搭", "婀", "发", "旮", "哈", "击", "喀", "拉",
            "妈", "那", "哦", "趴", "七", "然", "撒", "他", "挖", "西", "压", "匝",
        };

        private static readonly CompareInfo pinyin_compare_info = CultureInfo.GetCultureInfo("zh-CN").CompareInfo;

        private static readonly IComparer<string> ordinal = OrdinalSortByCaseStringComparer.DEFAULT;
        private static readonly IComparer<string> pinyin = new PinyinSortByCaseComparer();

        /// <summary>
        /// The comparer matching the current UI language.
        /// </summary>
        public static IComparer<string> ForCurrentUiCulture => isChineseUi ? pinyin : ordinal;

        private static bool isChineseUi => CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.Ordinal);

        /// <summary>
        /// Resolves the A-Z group of text led by a Han character under the pinyin collation.
        /// Returns <see langword="false"/> for non-Chinese UI, non-Han text, and characters the collation has no pinyin for.
        /// </summary>
        public static bool TryGetPinyinGroupLetter(string? text, out char letter)
        {
            letter = '\0';

            if (!isChineseUi || string.IsNullOrEmpty(text))
                return false;

            if (!Rune.TryGetRuneAt(text, 0, out Rune rune) || !isHan(rune))
                return false;

            string first = rune.ToString();
            int matched = -1;

            for (int i = 0; i < pinyin_initial_probes.Length; i++)
            {
                if (pinyin_compare_info.Compare(first, pinyin_initial_probes[i], CompareOptions.IgnoreCase) < 0)
                    break;

                matched = i;
            }

            if (matched < 0)
                return false;

            letter = pinyin_initials[matched];
            return true;
        }

        private static bool isHan(Rune rune)
        {
            int value = rune.Value;

            return value is >= 0x3400 and <= 0x4DBF   // extension A
                   or >= 0x4E00 and <= 0x9FFF         // unified ideographs
                   or >= 0xF900 and <= 0xFAFF         // compatibility ideographs
                   or >= 0x20000 and <= 0x3FFFF;      // extensions B-H
        }

        private sealed class PinyinSortByCaseComparer : IComparer<string>
        {
            public int Compare(string? a, string? b)
            {
                int result = pinyin_compare_info.Compare(a, b, CompareOptions.IgnoreCase);

                if (result == 0)
                    result = -StringComparer.Ordinal.Compare(a, b); // negative to place lowercase letters before uppercase.

                return result;
            }
        }
    }
}

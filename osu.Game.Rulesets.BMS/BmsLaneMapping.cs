// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.BMS
{
    /// <summary>
    /// Single source for BMS channel → gameplay column mapping (decoder, UI scratch width, input routing).
    /// </summary>
    public static class BmsLaneMapping
    {
        /// <summary>
        /// Channel 16/56 = 1P scratch, 26/66 = 2P scratch.
        /// </summary>
        public static bool IsScratchChannel(int channel)
            => channel == 16 || channel == 26 || channel == 56 || channel == 66;

        /// <summary>
        /// Map BMS measure channel to mania-style column index (decoder scheme).
        /// </summary>
        /// <remarks>
        /// 1P: 11=1, 12=2, … 15=5, 16→scratch column 0, 18=6, 19=7 (7 keys + scratch left).
        /// 2P: similar with base columns 9–15 for second deck.
        /// </remarks>
        public static int ChannelToColumn(int channel) =>
            channel switch
            {
                16 or 56 => 0,
                11 or 51 => 1,
                12 or 52 => 2,
                13 or 53 => 3,
                14 or 54 => 4,
                15 or 55 => 5,
                18 or 58 => 6,
                19 or 59 => 7,

                26 or 66 => 8,
                21 or 61 => 9,
                22 or 62 => 10,
                23 or 63 => 11,
                24 or 64 => 12,
                25 or 65 => 13,
                28 or 68 => 14,
                29 or 69 => 15,

                _ => 0,
            };
    }
}

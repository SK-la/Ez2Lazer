// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Globalization;
using System.Linq;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Skinning;

namespace osu.Game.EzOsuGame.Edit
{
    /// <summary>
    /// Inverse transforms for legacy Mania <c>skin.ini</c> values written by Ez editors.
    /// Mirrors <see cref="LegacyManiaSkinDecoder"/> read semantics.
    /// </summary>
    public static class EzSkinLegacyManiaIniFormat
    {
        private const float min_legacy_hit_position = 240f;
        private const float max_legacy_hit_position = 480f;

        public static string FormatScaledValue(float ezValue) =>
            (ezValue / LegacyManiaSkinConfiguration.POSITION_SCALE_FACTOR).ToString(CultureInfo.InvariantCulture);

        public static string FormatHitPosition(float ezHitPosition)
        {
            float legacy = max_legacy_hit_position - ezHitPosition / LegacyManiaSkinConfiguration.POSITION_SCALE_FACTOR;
            legacy = Math.Clamp(legacy, min_legacy_hit_position, max_legacy_hit_position);
            return legacy.ToString(CultureInfo.InvariantCulture);
        }

        public static string FormatColumnWidthArray(int keyMode, Ez2ConfigManager config)
        {
            var values = Enumerable.Range(0, keyMode)
                                   .Select(i => FormatScaledValue(config.GetColumnWidth(keyMode, i)));

            return string.Join(',', values);
        }

        public static string FormatWidthForNoteHeightScale(int keyMode, Ez2ConfigManager config)
        {
            float columnWidth = config.GetColumnWidth(keyMode, 0);
            double noteHeightScale = config.Get<double>(Ez2Setting.NoteHeightScaleToWidth);
            float ezPixelHeight = columnWidth * 0.5f * (float)noteHeightScale;
            return FormatScaledValue(ezPixelHeight);
        }
    }
}

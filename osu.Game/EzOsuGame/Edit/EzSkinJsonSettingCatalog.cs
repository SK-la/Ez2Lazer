// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.EzOsuGame.Configuration;

namespace osu.Game.EzOsuGame.Edit
{
    /// <summary>
    /// Ez settings persisted in per-skin <see cref="EzSkinJsonDocument"/> (editor catalog only).
    /// This is the Ez2Config subset that participates in <see cref="EzSkinEditorComparisonSnapshot"/> default baselines.
    /// </summary>
    public static class EzSkinJsonSettingCatalog
    {
        public static IReadOnlyList<Ez2Setting> All { get; } = new[]
        {
            // Texture
            Ez2Setting.GameThemeName,
            Ez2Setting.StageName,
            Ez2Setting.NoteSetName,

            // Stage
            Ez2Setting.ManiaPseudo3DRotation,
            Ez2Setting.ColumnDim,
            Ez2Setting.ColumnBlur,
            Ez2Setting.StagePanelEnabled,

            // Size
            Ez2Setting.ColumnWidthStyle,
            Ez2Setting.ColumnWidth,
            Ez2Setting.SpecialFactor,
            Ez2Setting.HitPositionGlobalEnable,
            Ez2Setting.HitPosition,
            Ez2Setting.HitTargetFloatFixed,
            Ez2Setting.HitTargetAlpha,
            Ez2Setting.NoteHeightScaleToWidth,
            Ez2Setting.NoteCornerRadius,

            // Skin-specific
            Ez2Setting.ManiaLNGradientEnable,
            Ez2Setting.ManiaHoldTailMaskGradientHeight,
            Ez2Setting.ManiaHoldTailAlpha,
            Ez2Setting.NoteTrackLineHeight,

            // Colour
            Ez2Setting.ColorSettingsEnabled,
            Ez2Setting.ColumnTypeListSelect,
            Ez2Setting.ColumnTypeA,
            Ez2Setting.ColumnTypeB,
            Ez2Setting.ColumnTypeS,
            Ez2Setting.ColumnTypeE,
            Ez2Setting.ColumnTypeP,
            Ez2Setting.ColumnTypeOf4K,
            Ez2Setting.ColumnTypeOf5K,
            Ez2Setting.ColumnTypeOf6K,
            Ez2Setting.ColumnTypeOf7K,
            Ez2Setting.ColumnTypeOf8K,
            Ez2Setting.ColumnTypeOf9K,
            Ez2Setting.ColumnTypeOf10K,
            Ez2Setting.ColumnTypeOf12K,
            Ez2Setting.ColumnTypeOf14K,
            Ez2Setting.ColumnTypeOf16K,
            Ez2Setting.ColumnTypeOf18K,
        };

        private static readonly HashSet<Ez2Setting> lookup = new HashSet<Ez2Setting>(All);

        public static bool Contains(Ez2Setting setting) => lookup.Contains(setting);
    }
}

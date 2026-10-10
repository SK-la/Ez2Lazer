// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.Skinning;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Rulesets.Mania.EzMania
{
    public readonly struct ManiaColumnSize
    {
        public float Width { get; init; }
        public MarginPadding Margin { get; init; }

        public float TotalWidth => Width + Margin.Left + Margin.Right;

        public static ManiaColumnSize Zero => new ManiaColumnSize { Width = 0, Margin = default };
    }

    /// <summary>
    /// Shared Mania playfield layout helpers for HUD/components (column sizing, panel width, hit position).
    /// Column sizing logic is extracted from <see cref="ColumnFlow{TContent}.updateColumnSize"/>.
    /// Hit position logic matches <see cref="UI.Components.HitPositionPaddedContainer"/>.
    /// </summary>
    public static class ManiaPlayfieldLayoutHelper
    {
        public const bool DEFAULT_MATCH_PANEL_WIDTH = true;

        public static BindableBool CreateDefaultMatchPanelWidthBindable() => new BindableBool(DEFAULT_MATCH_PANEL_WIDTH);

        /// <summary>
        /// Aligns a HUD element to the judgement line by position (not height).
        /// Anchor at bottom-centre, origin at centre, X = 0, Y = hit position.
        /// </summary>
        public static void ApplyHitPositionPlacement(Drawable drawable, float hitPosition)
        {
            drawable.Position = new Vector2(0, -hitPosition);
        }

        public static float CalculateMobileAdjust(int keyMode, ManiaMobileLayout mobileLayout, Vector2? containingCellSize)
        {
            if (!RuntimeInfo.IsMobile || mobileLayout != ManiaMobileLayout.LandscapeExpandedColumns)
                return 1f;

            // Will be null in tests.
            if (containingCellSize == null || containingCellSize.Value.X < containingCellSize.Value.Y)
                return 1f;

            float aspectRatio = containingCellSize.Value.X / containingCellSize.Value.Y;

            // 2.83 is a mostly arbitrary scale-up (170 / 60, based on original implementation for argon)
            float mobileAdjust = 2.83f * Math.Min(1, 7f / keyMode);
            // 1.92 is a "reference" mobile screen aspect ratio for phones.
            // We should scale it back for cases like tablets which aren't so extreme.
            mobileAdjust *= aspectRatio / 1.92f;
            return mobileAdjust;
        }

        /// <summary>
        /// Returns the effective hit position using the same rules as the in-game playfield.
        /// </summary>
        public static float GetHitPosition(ISkinSource skin, Ez2ConfigManager ezSkinConfig)
        {
            if (ezSkinConfig.Get<bool>(Ez2Setting.HitPositionGlobalEnable))
                return (float)ezSkinConfig.Get<double>(Ez2Setting.HitPosition);

            return skin.GetConfig<ManiaSkinConfigurationLookup, float>(
                           new ManiaSkinConfigurationLookup(LegacyManiaSkinConfigurationLookups.HitPosition))
                       ?.Value
                   ?? (float)ezSkinConfig.Get<double>(Ez2Setting.HitPosition);
        }

        /// <summary>
        /// Returns the effective hit position using the same rules as the in-game playfield.
        /// </summary>
        public static float GetHitPosition(
            ISkinSource skin,
            bool hitPositionGlobalEnable,
            double hitPosition)
        {
            if (hitPositionGlobalEnable)
                return (float)hitPosition;

            return skin.GetConfig<ManiaSkinConfigurationLookup, float>(
                           new ManiaSkinConfigurationLookup(LegacyManiaSkinConfigurationLookups.HitPosition))
                       ?.Value
                   ?? (float)hitPosition;
        }

        /// When <c>false</c>, only skin column width (or the configured base width) is used.
        /// When <c>true</c>, the full ColumnFlow sizing path is used, including spacing and global width style.
        /// <param name="columnIndex">The index of the column for which to calculate size.</param>
        /// <param name="keyMode">The number of columns in the stage.</param>
        /// <param name="skin">The skin source.</param>
        /// <param name="skinManager">The skin manager.</param>
        /// <param name="ezSkinConfig">The Ez skin configuration.</param>
        /// <param name="columnWidth">Configured base column width.</param>
        /// <param name="specialFactor">Configured special-column width factor.</param>
        /// <param name="columnWidthStyle">Global column width style from Ez skin settings.</param>
        /// <param name="mobileAdjust">Mobile landscape column scale factor.</param>
        /// <param name="applyGlobalWidthSettings">Whether to apply global width settings.</param>
        public static ManiaColumnSize CalculateColumnSize(
            int columnIndex,
            int keyMode,
            ISkinSource skin,
            SkinManager skinManager,
            Ez2ConfigManager ezSkinConfig,
            double columnWidth,
            double specialFactor,
            ColumnWidthStyle columnWidthStyle,
            float mobileAdjust,
            bool applyGlobalWidthSettings)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(columnIndex);

            if (!applyGlobalWidthSettings)
            {
                float? skinWidth = skin.GetConfig<ManiaSkinConfigurationLookup, float>(
                                           new ManiaSkinConfigurationLookup(LegacyManiaSkinConfigurationLookups.ColumnWidth, columnIndex))
                                       ?.Value;

                return new ManiaColumnSize
                {
                    Width = skinWidth ?? (float)columnWidth,
                    Margin = default,
                };
            }

            float leftSpacing = skin.GetConfig<ManiaSkinConfigurationLookup, float>(
                                        new ManiaSkinConfigurationLookup(LegacyManiaSkinConfigurationLookups.LeftColumnSpacing, columnIndex))
                                    ?.Value ?? Stage.COLUMN_SPACING;

            float rightSpacing = skin.GetConfig<ManiaSkinConfigurationLookup, float>(
                                         new ManiaSkinConfigurationLookup(LegacyManiaSkinConfigurationLookups.RightColumnSpacing, columnIndex))
                                     ?.Value ?? Stage.COLUMN_SPACING;

            float? width = skin.GetConfig<ManiaSkinConfigurationLookup, float>(
                                   new ManiaSkinConfigurationLookup(LegacyManiaSkinConfigurationLookups.ColumnWidth, columnIndex))
                               ?.Value;

            if (width == 0)
                return ManiaColumnSize.Zero;

            bool isSpecialColumn = ezSkinConfig.IsSpecialColumnFast(keyMode, columnIndex);
            float ezWidth = (float)columnWidth * (isSpecialColumn ? (float)specialFactor : 1);

            switch (columnWidthStyle)
            {
                case ColumnWidthStyle.EzSkinOnly:
                    var skinInfoType = skinManager.CurrentSkinInfo.Value.GetType();
                    if (skinInfoType == typeof(EzStyleProSkin) || skinInfoType == typeof(Ez2Skin) || skinInfoType == typeof(SbISkin))
                        width = ezWidth;
                    break;

                case ColumnWidthStyle.GlobalWidth:
                    width = ezWidth;
                    break;

                case ColumnWidthStyle.GlobalTotalWidth:
                    width = ezWidth * 10 / keyMode;
                    break;
            }

            // only used by default skin (legacy skins get defaults set in LegacyManiaSkinConfiguration)
            width ??= isSpecialColumn ? Column.SPECIAL_COLUMN_WIDTH : Column.COLUMN_WIDTH;

            return new ManiaColumnSize
            {
                Width = width.Value * mobileAdjust,
                Margin = new MarginPadding { Left = leftSpacing, Right = rightSpacing },
            };
        }

        public static float CalculatePanelTotalWidth(
            int keyMode,
            int displayColumnCount,
            ISkinSource skin,
            SkinManager skinManager,
            Ez2ConfigManager ezSkinConfig,
            double columnWidth,
            double specialFactor,
            ColumnWidthStyle columnWidthStyle,
            float mobileAdjust,
            bool applyGlobalWidthSettings)
        {
            float totalWidth = 0;

            for (int i = 0; i < displayColumnCount; i++)
            {
                totalWidth += CalculateColumnSize(
                    i,
                    keyMode,
                    skin,
                    skinManager,
                    ezSkinConfig,
                    columnWidth,
                    specialFactor,
                    columnWidthStyle,
                    mobileAdjust,
                    applyGlobalWidthSettings).TotalWidth;
            }

            return totalWidth;
        }
    }
}

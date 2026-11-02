// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Game.EzOsuGame.Configuration;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Overlays.Preview
{
    /// <summary>
    /// Bottom of the drawable is map start; time progresses upward (mania scroll direction).
    /// 颜色与厚度默认值以 <see cref="EzPreviewManiaSettings"/> 为唯一来源。
    /// </summary>
    public static class ManiaPreviewDrawHelper
    {
        private static Colour4 colourFor(EzPreviewManiaSettings settings, ManiaPreviewNoteKind kind) => settings.For(toPart(kind));

        private static EzManiaNotePart toPart(ManiaPreviewNoteKind kind) => kind switch
        {
            ManiaPreviewNoteKind.HoldHead => EzManiaNotePart.HoldHead,
            ManiaPreviewNoteKind.HoldBody => EzManiaNotePart.HoldBody,
            ManiaPreviewNoteKind.HoldTail => EzManiaNotePart.HoldTail,
            _ => EzManiaNotePart.Note,
        };

        public static void AddLaneLines(List<PreviewQuad> quads, ManiaPreviewData data, float originX, float width, float height, float thickness)
        {
            int totalColumns = data.TotalColumns;
            if (totalColumns <= 1)
                return;

            float laneWidth = width / totalColumns;

            for (int lane = 1; lane < totalColumns; lane++)
            {
                bool hasSeparator = lane - 1 < data.SeparatorAfterColumns.Count && data.SeparatorAfterColumns[lane - 1];
                if (!hasSeparator)
                    continue;

                float x = originX + lane * laneWidth - thickness * 0.5f;
                quads.Add(new PreviewQuad(x, 0, thickness * 1.8f, height, Color4.White.Opacity(0.30f)));
            }
        }

        public static void AddBeatLines(List<PreviewQuad> quads, int totalRows, float rowStep, float originX, float width, float height, float thickness)
        {
            for (int row = ManiaPreviewFixedLayout.ROWS_PER_BEAT; row < totalRows; row += ManiaPreviewFixedLayout.ROWS_PER_BEAT)
            {
                float y = GetSlotBottomY(row, rowStep, height) - thickness * 0.5f;
                quads.Add(new PreviewQuad(originX, y, width, thickness, Color4.White.Opacity(0.22f)));
            }
        }

        public static void AddLayoutEntries(
            List<PreviewQuad> quads,
            IReadOnlyList<ManiaPreviewLayoutEntry> entries,
            int totalColumns,
            float originX,
            float width,
            float height,
            float rowStep,
            float noteHeight,
            bool flatNotes,
            EzPreviewManiaSettings settings,
            double timeOriginMs = double.NaN,
            double msPerRow = 0)
        {
            float laneWidth = width / Math.Max(1, totalColumns);
            float laneInset = laneWidth * 0.1f;
            float headHeight = (flatNotes ? Math.Max(1f, noteHeight * 0.55f) : noteHeight) * settings.ThicknessScale.Value;
            bool mapTime = msPerRow > 0 && !double.IsNaN(timeOriginMs);

            float yAtTime(double time) => height - (float)((time - timeOriginMs) / msPerRow) * rowStep;

            foreach (ManiaPreviewLayoutEntry entry in entries)
            {
                float x = originX + entry.Column * laneWidth + laneInset;
                float laneW = laneWidth - laneInset * 2;
                float anchorY = mapTime ? yAtTime(entry.StartTime) : GetSlotBottomY(entry.Row, rowStep, height);

                switch (entry.Kind)
                {
                    case ManiaPreviewNoteKind.Tap:
                    case ManiaPreviewNoteKind.HoldHead:
                    {
                        quads.Add(new PreviewQuad(x, anchorY - headHeight, laneW, headHeight, colourFor(settings, entry.Kind)));
                        break;
                    }

                    case ManiaPreviewNoteKind.HoldBody:
                    {
                        // 身体停在结束时刻。不要铺到格子远端，否则尾巴会和下一个头贴死，滚动映射比例也拉不开。
                        float bodyTop = mapTime ? yAtTime(entry.EndTime) : GetSlotBottomY(entry.EndRow, rowStep, height);
                        float bodyBottom = anchorY - headHeight;
                        float h = Math.Max(1f, bodyBottom - bodyTop);
                        quads.Add(new PreviewQuad(x, bodyTop, laneW, h, colourFor(settings, entry.Kind)));
                        break;
                    }
                }
            }
        }

        /// <summary>Bottom edge of the row slot (map start is at drawable bottom).</summary>
        public static float GetSlotBottomY(int row, float rowStep, float height) => height - row * rowStep;

        /// <summary>Top edge of the row slot.</summary>
        public static float GetSlotTopY(int row, float rowStep, float height) => height - (row + 1) * rowStep;

        public static (float rowStep, float noteHeight) ComputeRowMetrics(int totalRows, float availableHeight)
        {
            float rowStep = availableHeight / Math.Max(1, totalRows);
            float noteHeight = rowStep * 0.5f;
            return (rowStep, noteHeight);
        }
    }
}

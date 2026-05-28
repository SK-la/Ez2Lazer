// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Extensions.Color4Extensions;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Overlays.Preview
{
    /// <summary>
    /// Bottom of the drawable is map start; time progresses upward (mania scroll direction).
    /// </summary>
    public static class ManiaPreviewDrawHelper
    {
        private static readonly Color4 tap_colour = new Color4(80, 150, 255, 230);
        private static readonly Color4 hold_head_colour = new Color4(255, 80, 80, 235);
        private static readonly Color4 hold_body_tail_colour = new Color4(255, 220, 70, 230);

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
            bool flatNotes)
        {
            float laneWidth = width / Math.Max(1, totalColumns);
            float laneInset = laneWidth * 0.1f;
            float headHeight = flatNotes ? Math.Max(1f, noteHeight * 0.55f) : noteHeight;

            foreach (ManiaPreviewLayoutEntry entry in entries)
            {
                float x = originX + entry.Column * laneWidth + laneInset;
                float laneW = laneWidth - laneInset * 2;

                switch (entry.Kind)
                {
                    case ManiaPreviewNoteKind.Tap:
                    {
                        float y = GetSlotBottomY(entry.Row, rowStep, height) - headHeight;
                        quads.Add(new PreviewQuad(x, y, laneW, headHeight, getColour(entry.Kind)));
                        break;
                    }

                    case ManiaPreviewNoteKind.HoldHead:
                    {
                        // Head at bottom of LN (press / start time row).
                        float y = GetSlotBottomY(entry.Row, rowStep, height) - headHeight;
                        quads.Add(new PreviewQuad(x, y, laneW, headHeight, getColour(entry.Kind)));
                        break;
                    }

                    case ManiaPreviewNoteKind.HoldTail:
                    {
                        // Tail at top of LN (release / end time row).
                        float y = GetSlotTopY(entry.Row, rowStep, height);
                        quads.Add(new PreviewQuad(x, y, laneW, headHeight, getColour(entry.Kind)));
                        break;
                    }

                    case ManiaPreviewNoteKind.HoldBody:
                    {
                        float bodyBottom = GetSlotBottomY(entry.Row, rowStep, height) - headHeight;
                        float bodyTop = GetSlotTopY(entry.EndRow, rowStep, height) + headHeight;
                        float h = Math.Max(headHeight, bodyBottom - bodyTop);
                        quads.Add(new PreviewQuad(x, bodyTop, laneW, h, getColour(entry.Kind)));
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

        private static Color4 getColour(ManiaPreviewNoteKind kind) => kind switch
        {
            ManiaPreviewNoteKind.Tap => tap_colour,
            ManiaPreviewNoteKind.HoldHead => hold_head_colour,
            ManiaPreviewNoteKind.HoldBody => hold_body_tail_colour,
            ManiaPreviewNoteKind.HoldTail => hold_body_tail_colour,
            _ => tap_colour,
        };
    }
}

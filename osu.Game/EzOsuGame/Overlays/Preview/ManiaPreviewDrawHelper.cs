// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Extensions.Color4Extensions;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Overlays.Preview
{
    public static class ManiaPreviewDrawHelper
    {
        private static readonly Color4 tap_colour = new Color4(80, 150, 255, 230);
        private static readonly Color4 hold_head_colour = new Color4(255, 80, 80, 235);
        private static readonly Color4 hold_body_tail_colour = new Color4(255, 220, 70, 230);

        public static void AddLaneLines(List<PreviewQuad> quads, int totalColumns, float originX, float width, float height, float thickness)
        {
            if (totalColumns <= 1)
                return;

            float laneWidth = width / totalColumns;

            for (int lane = 1; lane < totalColumns; lane++)
            {
                float x = originX + lane * laneWidth - thickness * 0.5f;
                quads.Add(new PreviewQuad(x, 0, thickness, height, Color4.White.Opacity(0.12f)));
            }
        }

        public static void AddBeatLines(List<PreviewQuad> quads, int totalRows, float rowStep, float originX, float width, float thickness)
        {
            for (int row = ManiaPreviewFixedLayout.ROWS_PER_BEAT; row < totalRows; row += ManiaPreviewFixedLayout.ROWS_PER_BEAT)
            {
                float y = row * rowStep - thickness * 0.5f;
                quads.Add(new PreviewQuad(originX, y, width, thickness, Color4.White.Opacity(0.22f)));
            }
        }

        public static void AddLayoutEntries(
            List<PreviewQuad> quads,
            IReadOnlyList<ManiaPreviewLayoutEntry> entries,
            int totalColumns,
            float originX,
            float width,
            float rowStep,
            float noteHeight,
            bool flatNotes)
        {
            float laneWidth = width / Math.Max(1, totalColumns);
            float laneInset = laneWidth * 0.1f;

            foreach (ManiaPreviewLayoutEntry entry in entries)
            {
                float x = originX + entry.Column * laneWidth + laneInset;
                float laneW = laneWidth - laneInset * 2;

                switch (entry.Kind)
                {
                    case ManiaPreviewNoteKind.Tap:
                    case ManiaPreviewNoteKind.HoldHead:
                    case ManiaPreviewNoteKind.HoldTail:
                    {
                        float y = entry.Row * rowStep;
                        float h = flatNotes ? Math.Max(1f, noteHeight * 0.55f) : noteHeight;
                        quads.Add(new PreviewQuad(x, y, laneW, h, getColour(entry.Kind)));
                        break;
                    }

                    case ManiaPreviewNoteKind.HoldBody:
                    {
                        float y0 = entry.Row * rowStep + noteHeight * 0.55f;
                        float y1 = (entry.EndRow + 1) * rowStep;
                        float h = Math.Max(noteHeight, y1 - y0);
                        quads.Add(new PreviewQuad(x, y0, laneW, h, getColour(entry.Kind)));
                        break;
                    }
                }
            }
        }

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

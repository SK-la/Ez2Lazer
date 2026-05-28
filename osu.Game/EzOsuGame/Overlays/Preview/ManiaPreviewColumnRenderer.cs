// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Extensions.Color4Extensions;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Overlays.Preview
{
    public static class ManiaPreviewColumnRenderer
    {
        public static void BuildColumnPanels(
            List<PreviewQuad> quads,
            IReadOnlyList<ManiaPreviewLayoutEntry> layoutEntries,
            ManiaPreviewColumnLayout layout,
            ManiaPreviewData data,
            int totalRows,
            float panelHeight)
        {
            float laneLineThickness = Math.Max(0.5f, layout.ColumnWidth * 0.004f);
            float beatLineThickness = Math.Max(0.5f, layout.RowStep * 0.08f);

            for (int col = 0; col < layout.ColumnCount; col++)
            {
                float panelX = col * (layout.ColumnWidth + ManiaPreviewColumnLayout.COLUMN_SPACING);
                int rowStart = col * layout.RowsPerColumn;
                int rowEnd = Math.Min(totalRows, rowStart + layout.RowsPerColumn);

                quads.Add(new PreviewQuad(panelX, 0, 1f, panelHeight, Color4.White.Opacity(0.18f)));
                quads.Add(new PreviewQuad(panelX + layout.ColumnWidth - 1f, 0, 1f, panelHeight, Color4.White.Opacity(0.18f)));

                ManiaPreviewDrawHelper.AddLaneLines(quads, data, panelX, layout.ColumnWidth, panelHeight, laneLineThickness);

                for (int row = rowStart + ManiaPreviewFixedLayout.ROWS_PER_BEAT; row <= rowEnd; row += ManiaPreviewFixedLayout.ROWS_PER_BEAT)
                {
                    int localRow = row - rowStart;
                    float y = ManiaPreviewDrawHelper.GetSlotBottomY(localRow, layout.RowStep, panelHeight) - beatLineThickness * 0.5f;
                    quads.Add(new PreviewQuad(panelX, y, layout.ColumnWidth, beatLineThickness, Color4.White.Opacity(0.2f)));
                }
            }

            foreach (ManiaPreviewLayoutEntry entry in layoutEntries)
            {
                int col = entry.Row / layout.RowsPerColumn;
                if (col >= layout.ColumnCount)
                    continue;

                int rowStart = col * layout.RowsPerColumn;
                int rowEnd = Math.Min(totalRows, rowStart + layout.RowsPerColumn);

                if (entry.Row >= rowEnd)
                    continue;

                float panelX = col * (layout.ColumnWidth + ManiaPreviewColumnLayout.COLUMN_SPACING);
                var localEntry = new ManiaPreviewLayoutEntry(
                    entry.Column,
                    entry.Row - rowStart,
                    Math.Min(entry.EndRow, rowEnd - 1) - rowStart,
                    entry.Kind);

                ManiaPreviewDrawHelper.AddLayoutEntries(
                    quads,
                    new[] { localEntry },
                    data.TotalColumns,
                    panelX,
                    layout.ColumnWidth,
                    panelHeight,
                    layout.RowStep,
                    layout.NoteHeight,
                    flatNotes: false);
            }
        }
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Mania.Beatmaps;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.LAsEZMania
{
    public static class EzStageDefinitionExtensions
    {
        public static bool EzIsSpecialColumn(this StageDefinition stage, int columnIndex)
        {
            if (columnIndex < 0 || columnIndex >= stage.Columns)
                return false;

            return stage.Columns switch
            {
                7 when columnIndex is 3 => true,
                9 when columnIndex is 4 => true,
                12 when columnIndex is 0 or 11 => true,
                14 when columnIndex is 0 or 12 => true,
                16 when columnIndex is 0 or 15 => true,
                _ => false
            };
        }

        public static Color4 EzGetColumnColor(this StageDefinition stage, int columnIndex)
        {
            if (columnIndex < 0 || columnIndex >= stage.Columns)
                return colour_column;

            return stage.Columns switch
            {
                12 when columnIndex is 0 or 11 => colour_scratch,
                14 when columnIndex is 0 or 12 => colour_scratch,
                14 when columnIndex is 13 => colour_alpha,
                14 when columnIndex is 6 => colour_panel,
                16 when columnIndex is 0 or 15 => colour_scratch,
                16 when columnIndex is 6 or 7 or 8 or 9 => colour_scratch,
                _ => colour_column
            };
        }

        private static readonly Color4 colour_column = new Color4(4, 4, 4, 255);
        private static readonly Color4 colour_scratch = new Color4(20, 0, 0, 255);
        private static readonly Color4 colour_panel = new Color4(0, 20, 0, 255);
        private static readonly Color4 colour_alpha = new Color4(0, 0, 0, 0);
    }
}

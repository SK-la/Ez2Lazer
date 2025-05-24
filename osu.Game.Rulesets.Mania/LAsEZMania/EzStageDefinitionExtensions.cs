// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
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

        // 颜色定义
        private static readonly Color4 colour_special = new Color4(206, 6, 3, 255);

        private static readonly Color4 colour_green = new Color4(100, 192, 92, 255);
        private static readonly Color4 colour_red = new Color4(206, 6, 3, 255);

        private static readonly Color4 colour_withe = new Color4(222, 222, 222, 255);
        private static readonly Color4 colour_blue = new Color4(55, 155, 255, 255);

        private const int total_colours = 3;

        private static readonly Color4 colour_cyan = new Color4(72, 198, 255, 255);
        private static readonly Color4 colour_pink = new Color4(213, 35, 90, 255);
        private static readonly Color4 colour_purple = new Color4(203, 60, 236, 255);

        public static Color4 GetColourForLayout(this StageDefinition stage, int columnIndex)
        {
            columnIndex %= stage.Columns;

            switch (stage.Columns)
            {
                case 4:
                    return columnIndex switch
                    {
                        0 => colour_green,
                        1 => colour_red,
                        2 => colour_blue,
                        3 => colour_cyan,
                        _ => throw new ArgumentOutOfRangeException()
                    };

                case 5:
                    return columnIndex switch
                    {
                        0 => colour_green,
                        1 => colour_blue,
                        2 => colour_red,
                        3 => colour_cyan,
                        4 => colour_purple,
                        _ => throw new ArgumentOutOfRangeException()
                    };

                case 7:
                    return columnIndex switch
                    {
                        1 or 5 => colour_withe,
                        0 or 2 or 4 or 6 => colour_blue,
                        3 => colour_green,
                        _ => throw new ArgumentOutOfRangeException()
                    };

                case 8:
                    return columnIndex switch
                    {
                        0 or 4 => colour_red,
                        2 or 6 => colour_withe,
                        1 or 3 or 5 or 7 => colour_blue,
                        _ => throw new ArgumentOutOfRangeException()
                    };

                case 9:
                    return columnIndex switch
                    {
                        0 or 6 or 7 => colour_red,
                        2 or 4 => colour_withe,
                        1 or 3 or 5 => colour_blue,
                        8 => colour_green,
                        _ => throw new ArgumentOutOfRangeException()
                    };

                case 10:
                    return columnIndex switch
                    {
                        0 or 9 => colour_green,
                        2 or 4 or 5 or 7 => colour_withe,
                        1 or 3 or 6 or 8 => colour_blue,
                        _ => throw new ArgumentOutOfRangeException()
                    };

                case 12:
                    return columnIndex switch
                    {
                        0 or 11 => colour_red,
                        1 or 3 or 5 or 6 or 8 or 10 => colour_withe,
                        2 or 4 or 7 or 9 => colour_blue,
                        _ => throw new ArgumentOutOfRangeException()
                    };

                case 14:
                    return columnIndex switch
                    {
                        0 or 12 or 13 => colour_red,
                        1 or 3 or 5 or 7 or 9 or 11 => colour_withe,
                        2 or 4 or 8 or 10 => colour_blue,
                        6 => colour_green,
                        _ => throw new ArgumentOutOfRangeException()
                    };

                case 16:
                    return columnIndex switch
                    {
                        0 or 6 or 7 or 8 or 9 or 15 => colour_red,
                        1 or 3 or 5 or 10 or 12 or 14 => colour_withe,
                        2 or 4 or 11 or 13 => colour_blue,
                        _ => throw new ArgumentOutOfRangeException()
                    };
            }

            // 后备逻辑保持不变
            if (stage.EzIsSpecialColumn(columnIndex))
                return colour_special;

            switch (columnIndex % total_colours)
            {
                case 0: return colour_cyan;

                case 1: return colour_pink;

                case 2: return colour_purple;

                default: throw new ArgumentOutOfRangeException();
            }
        }
    }
}

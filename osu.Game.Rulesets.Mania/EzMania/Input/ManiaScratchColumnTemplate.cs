// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Mania.EzMania.Input
{
    /// <summary>
    /// 转盘轴模板：将 L/R scratch 映射到 12/14/16K 首尾列。
    /// </summary>
    public static class ManiaScratchColumnTemplate
    {
        /// <summary>
        /// 解析当前键数下的 (L列, R列)。无匹配时返回 false。
        /// </summary>
        /// <param name="variant">Mania variant（= 逻辑键数）。</param>
        /// <param name="skipEmptyEdgeColumns">10k2s1p / <c>ManiaSkipEmptyEdgeColumns</c>。</param>
        /// <param name="leftColumn">L-Scratch 对应列下标。</param>
        /// <param name="rightColumn">R-Scratch 对应列下标。</param>
        public static bool TryResolve(int variant, bool skipEmptyEdgeColumns, out int leftColumn, out int rightColumn)
        {
            switch (variant)
            {
                case 12:
                    leftColumn = 0;
                    rightColumn = 11;
                    return true;

                case 14:
                    leftColumn = 0;
                    rightColumn = skipEmptyEdgeColumns ? 12 : 13;
                    return true;

                case 16:
                    leftColumn = 0;
                    rightColumn = 15;
                    return true;

                default:
                    leftColumn = rightColumn = -1;
                    return false;
            }
        }
    }
}

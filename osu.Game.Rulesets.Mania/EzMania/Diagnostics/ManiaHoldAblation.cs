// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Mania.EzMania.Diagnostics
{
    /// <summary>
    /// LN 绘制 / 判定 / 输入队列的消融开关。仅 Debug 构建生效；Release 全部为编译期常量 false。
    /// </summary>
    public static class ManiaHoldAblation
    {
#if DEBUG
        public static bool DisableSubtractionStroke;
        public static bool DisableHoldTickScan;
        public static bool DisableHoldTickGeneration;
        public static bool ForceHoldTickGeneration;
        public static bool EnqueueHoldEnds;

        public static void Reset()
        {
            DisableSubtractionStroke = false;
            DisableHoldTickScan = false;
            DisableHoldTickGeneration = false;
            ForceHoldTickGeneration = false;
            EnqueueHoldEnds = false;
        }
#else
        public const bool DisableSubtractionStroke = false;
        public const bool DisableHoldTickScan = false;
        public const bool DisableHoldTickGeneration = false;
        public const bool ForceHoldTickGeneration = false;
        public const bool EnqueueHoldEnds = false;

        public static void Reset()
        {
        }
#endif
    }
}

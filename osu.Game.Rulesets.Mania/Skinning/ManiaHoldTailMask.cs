// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.UI.Scrolling;

namespace osu.Game.Rulesets.Mania.Skinning
{
    /// <summary>
    /// LN 投皮距离：UI 档位 / 32 = 节拍，再乘拍长得到时长。
    /// 0 关闭投皮额外负担；默认 4 -> 4/32 = 2/16。
    /// <see cref="EzOsuGame.Configuration.Ez2Setting.ManiaLNGradientEnable"/> 只控制 tail/body 显隐，body 内还要再判档位。
    /// </summary>
    public static class ManiaHoldTailMask
    {
        /// <summary>
        /// UI 档位除数。注释约定：值 / 32 得到节拍。
        /// </summary>
        public const double BEAT_DIVISOR = 32;

        /// <summary>
        /// 档位为 0 时不计算投皮距离，避免伪面尾开启后的额外负担。
        /// </summary>
        public static bool IsEnabled(double uiLevel) => uiLevel > 0;

        public static double GetDuration(double uiLevel, double beatLength)
        {
            if (beatLength <= 0 || uiLevel <= 0)
                return 0;

            return beatLength * uiLevel / BEAT_DIVISOR;
        }

        public static float GetLength(ScrollingHitObjectContainer? hitObjectContainer, double uiLevel, double beatLength)
        {
            double duration = GetDuration(uiLevel, beatLength);

            if (duration <= 0 || hitObjectContainer == null)
                return 0;

            return hitObjectContainer.LengthAtTime(0, duration);
        }

        public static double ResolveTimeRange(IScrollingInfo? scrollingInfo)
            => scrollingInfo?.TimeRange.Value ?? double.NaN;

        public static double ResolveLevel(DrawableManiaRuleset? ruleset, double fallbackLevel)
            => ruleset?.HoldTailMaskLevel ?? fallbackLevel;

        public static double ResolveBeatLength(DrawableManiaRuleset? ruleset)
        {
            if (ruleset == null)
                return 500;

            if (IsEnabled(ruleset.HoldTailMaskLevel) && ruleset.DynamicHoldTailMask)
            {
                double liveBeatLength = ruleset.HoldTailSpeedTracker?.BeatLength.Value ?? 0;
                if (liveBeatLength > 0)
                    return liveBeatLength;
            }

            return ruleset.MainBeatLength > 0 ? ruleset.MainBeatLength : 500;
        }
    }
}

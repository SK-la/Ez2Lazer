// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.UI.Scrolling;

namespace osu.Game.EzOsuGame
{
    /// <summary>
    /// 舞台板相对演奏区的摆放。判定线是唯一基准：舞台板缩放原点与落点都由它推出，
    /// 因此列宽、画面缩放与命中位置设置都不会让图上的判定线离开游戏判定线。
    /// </summary>
    public static class EzStagePlacement
    {
        /// <summary>
        /// layout 未声明 <c>designWidth</c> 时的设计宽度。现有 Ez 舞台均为该值。
        /// </summary>
        public const float DEFAULT_DESIGN_WIDTH = 412;

        /// <summary>
        /// 舞台板缩放系数。设计宽度来自 stage layout，自制舞台可用其它宽度。
        /// </summary>
        public static float GetStageScale(float stageWidth, float designWidth)
        {
            if (!(designWidth > 0))
                designWidth = DEFAULT_DESIGN_WIDTH;

            return stageWidth / designWidth;
        }

        /// <summary>
        /// 判定线相对 Body 图中心的偏移，向下为正。<paramref name="ratio"/> 从 Body 底边往上量。
        /// </summary>
        public static float GetJudgementLineOffset(float bodyHeight, float ratio) => (0.5f - ratio) * bodyHeight;

        /// <summary>
        /// 判定线相对演奏区中心的偏移，向下为正。演奏区没有 stage padding 时直接由高度与命中位置得到。
        /// </summary>
        public static float GetHitLineOffset(float drawHeight, float hitPosition, ScrollingDirection direction)
            => direction == ScrollingDirection.Up ? hitPosition - drawHeight / 2 : drawHeight / 2 - hitPosition;
    }
}

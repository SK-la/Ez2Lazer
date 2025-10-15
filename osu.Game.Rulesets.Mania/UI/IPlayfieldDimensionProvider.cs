// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics.Primitives;

namespace osu.Game.Rulesets.Mania.UI
{
    /// <summary>
    /// 用于为背景效果提供游戏区域尺寸信息的规则集接口。
    /// </summary>
    public interface IPlayfieldDimensionProvider
    {
        /// <summary>
        /// 获取游戏区域在屏幕空间中的边界。
        /// </summary>
        /// <returns>游戏区域在屏幕坐标中的边界，如果不可用则为 null。</returns>
        RectangleF? GetPlayfieldBounds();

        /// <summary>
        /// 此规则集是否支持背景模糊遮罩。
        /// </summary>
        bool SupportsBackgroundBlurMasking { get; }

        /// <summary>
        /// 背景图像是否应在游戏区域外模糊。
        /// </summary>
        bool ShouldBlurBackground => true;

        /// <summary>
        /// 故事板是否应在游戏区域外模糊。
        /// </summary>
        bool ShouldBlurStoryboard => true;

        /// <summary>
        /// 故事板中的视频是否应在游戏区域外模糊。
        /// </summary>
        bool ShouldBlurVideo => true;
    }
}

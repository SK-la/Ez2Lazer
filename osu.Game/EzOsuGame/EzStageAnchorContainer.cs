// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;

namespace osu.Game.EzOsuGame
{
    /// <summary>
    /// 只提供坐标原点的容器：自身不绘制像素，尺寸保持为零，子项按绝对坐标摆放。
    /// </summary>
    /// <remarks>
    /// 零尺寸容器的包围盒是一个点，默认的裁剪判定会在原点移出屏幕时把整棵子树一起剪掉。
    /// 舞台需要这种原点（它会随舞台位置移动），所以本层跳过自身判定，由各子项按自身包围盒处理。
    /// </remarks>
    public partial class EzStageAnchorContainer : Container
    {
        protected override bool ComputeIsMaskedAway(RectangleF maskingBounds) => false;
    }
}

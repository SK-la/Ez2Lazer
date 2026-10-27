// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osuTK;

namespace osu.Game.Rulesets.Osu.Skinning
{
    /// <summary>
    /// osu!stable 将滑条身栅格化到画布时如何决定画布尺寸。
    /// </summary>
    /// <remarks>
    /// <para>
    /// stable 把身画进与窗口同大的纹理，一显示像素对应一纹理像素，并把身的边界交给视口。
    /// 纹理外的内容永不栅格化。
    /// </para>
    /// <para>
    /// 身的边界换算成显示像素后若超过渲染器接受的最大视口，则在投影仍覆盖那些边界时直接拒绝，
    /// 因而什么都不画。
    /// </para>
    /// <para>
    /// 此处一律用显示像素而非场地单位，因此结果随窗口与游玩区缩放变化，而不假定固定分辨率。
    /// </para>
    /// </remarks>
    public static class StableViewportBody
    {
        /// <summary>
        /// 渲染器预期可接受的最大视口边长（像素）。
        /// </summary>
        /// <remarks>D3D11 接受 16384，是此处所用后端中的最小上限。</remarks>
        public const float MAX_VIEWPORT_DIMENSION = 16384;

        /// <summary>
        /// 窗口大小画布覆盖的场地单位区域。
        /// </summary>
        /// <param name="viewportPixels">窗口客户区，显示像素。</param>
        /// <param name="pixelsPerUnit">每场地单位占用多少显示像素。</param>
        public static Vector2 VisibleFieldSize(Vector2 viewportPixels, float pixelsPerUnit)
            => pixelsPerUnit <= 0 ? Vector2.Zero : viewportPixels / pixelsPerUnit;

        /// <summary>
        /// stable 是否会完全画不出给定场地单位尺寸的身。
        /// </summary>
        /// <param name="bodySize">身的边界，场地单位。</param>
        /// <param name="pixelsPerUnit">每场地单位占用多少显示像素。</param>
        public static bool IsRejected(Vector2 bodySize, float pixelsPerUnit)
        {
            if (!float.IsFinite(bodySize.X) || !float.IsFinite(bodySize.Y))
                return true;

            // 每边三十二个游玩区。一场地单位一像素时 stable 打不开视口；
            // 测得更小的缩放也不能放行：分配帧缓冲前 drawable 仍会放大到真实游玩区。
            if (bodySize.X > MAX_VIEWPORT_DIMENSION || bodySize.Y > MAX_VIEWPORT_DIMENSION)
                return true;

            if (!(pixelsPerUnit > 0))
                return false;

            return bodySize.X * pixelsPerUnit > MAX_VIEWPORT_DIMENSION || bodySize.Y * pixelsPerUnit > MAX_VIEWPORT_DIMENSION;
        }

        /// <summary>
        /// 该场地位置上的滑条部件能否提交给渲染器。
        /// </summary>
        /// <remarks>
        /// 超出视口上限的部件不绘制。留在那些坐标上的尾或球每帧仍被访问（游玩区关闭 masking），
        /// 产生的图元会卡住 GPU 直到谱面结束。
        /// </remarks>
        public static bool CanPlaceFieldPosition(Vector2 position)
            => float.IsFinite(position.X) && float.IsFinite(position.Y)
               && MathF.Abs(position.X) <= MAX_VIEWPORT_DIMENSION
               && MathF.Abs(position.Y) <= MAX_VIEWPORT_DIMENSION;
    }
}

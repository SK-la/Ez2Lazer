// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame
{
    /// <summary>
    /// Ez 纹理获取意图。由 <see cref="EzResourceStore"/> 映射到不同底层 store，避免误用导致崩溃或 atlas 溢出。
    /// </summary>
    public enum EzTextureUsage
    {
        /// <summary>
        /// 小尺寸 UI 纹理，可进入默认 1024 atlas。
        /// </summary>
        Atlas,

        /// <summary>
        /// 多帧 / 循环动画帧：独立 GPU 纹理、Dispose 为空操作。
        /// 可安全交给 <see cref="Framework.Graphics.Animations.TextureAnimation"/>。
        /// 禁止与 <see cref="Large"/> 混淆——Large 的引用计数会在切帧 Dispose 后 Purge。
        /// </summary>
        AnimationSafe,

        /// <summary>
        /// 单帧大图（背景、静态 Stage 等），带引用计数，用完即回收。
        /// 禁止用于循环动画帧。
        /// </summary>
        Large,

        /// <summary>
        /// 位图字形的专用页（<c>GameTheme/{theme}/combo/number/*</c> 等）。
        /// </summary>
        /// <remarks>
        /// 必须独占一页：同一套字形（数字 0-9 等）会被连续绘制，若与其他纹理共享页，
        /// 大件会把分配器挤到页底触发换页，同一套字形就被拆到两页上——
        /// 数值每变一次都要在两页之间切换纹理绑定（打断批处理）。
        /// </remarks>
        Glyph,

        /// <summary>
        /// 段位图一类「单次绘制的大件」的专用页。
        /// </summary>
        /// <remarks>
        /// 与 <see cref="Glyph"/> 分开的理由同上：大件会消耗整行/整页，和字形混在一页会互相踩踏。
        /// 这类图只有一张（每个段位一张），进图集是为了让不同行之间能共用同一个纹理绑定，
        /// 所以用比默认更大的页，减少页数。
        /// </remarks>
        Badge,
    }
}

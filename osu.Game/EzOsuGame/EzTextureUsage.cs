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
        /// 单帧大图（背景、静态 Stage），带引用计数，用完即回收。
        /// 禁止用于循环动画帧。
        /// </summary>
        /// <remarks>
        /// 显示尺寸无法通过缩放改变的整图（例如边长超过图集页的图）才走这里：
        /// 边长超过「页边长 - 2×PADDING」的图进不了图集，会被静默绕过并打日志；而把这类图放进
        /// 更大的页里往往一页只放得下一张，反而比独立纹理更占显存（例如 1024² 进 2048 页 = 4 倍）。
        /// 代价是各引用全部释放后会被立即回收，重新显示时需要重新解码；仅适用于切换不频繁的图。
        /// </remarks>
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
        /// 段位标（<c>Dans/*</c>）一类「源图远大于显示尺寸」的成套小图：加载期等比压到规范边长后，进独立图集页。
        /// </summary>
        /// <remarks>
        /// 显存占用只能由上传的像素决定：显示期的比例（<see cref="Framework.Graphics.Textures.Texture.ScaleAdjust"/>、位图字形的 scale）只影响
        /// 绘制与度量。位图字形能直接进页是因为美术已按规范尺寸（96px 级）出图；段位标源图边长 280–1024，
        /// 超过 1024 页的可放上限（页边长 - 2×PADDING）会被静默绕过并打日志，560 那种也会一页只放得下一两张，
        /// 故在「创建纹理」这一步把尺寸定死（见 <see cref="EzResourceStore"/> 的 dan 页）。
        /// </remarks>
        Badge,
    }
}

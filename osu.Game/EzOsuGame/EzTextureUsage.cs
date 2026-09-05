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
    }
}

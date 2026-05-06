// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Overlays
{
    /// <summary>
    /// 全屏大图资源选择覆盖层入口（GameTheme / NoteSet / Stage）。
    /// </summary>
    public interface IEzResourcePickerOverlay
    {
        /// <summary>
        /// 打开选择器并从当前会话构建候选列表。
        /// </summary>
        void Present(EzResourcePickerDescriptor descriptor);
    }
}

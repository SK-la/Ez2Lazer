// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;

namespace osu.Game.LAsEzExtensions.HUD
{
    /// <summary>
    /// 战未来预览组件接口，提供预览用的纹理信息
    /// </summary>
    public interface IPreviewable
    {
        Bindable<string> TextureNameBindable { get; }
        string TextureBasePath { get; }
    }
}

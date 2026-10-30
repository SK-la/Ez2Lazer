// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osu.Framework.Localisation;

namespace osu.Game.EzOsuGame.Edit
{
    /// <summary>
    /// 当前场景下一个侧栏设置组；导航轨按顺序索引并滚动到对应分组。
    /// </summary>
    public sealed class EzSkinEditorSidebarGroupDefinition
    {
        /// <summary>
        /// 可选稳定标识，供交互接线与导航定位使用。
        /// </summary>
        public string? Id { get; init; }

        public LocalisableString Title { get; init; }

        public Func<Drawable> CreateContent { get; init; } = null!;

        public bool ExpandedByDefault { get; init; } = true;

        public bool Visible { get; init; } = true;
    }
}

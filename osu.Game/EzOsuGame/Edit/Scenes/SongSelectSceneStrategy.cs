// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Game.Localisation;

namespace osu.Game.EzOsuGame.Edit.Scenes
{
    /// <summary>
    /// 选歌场景：游玩区由 Overlay 插入现成 <c>SoloSongSelect</c>，本策略只提供侧栏占位。
    /// </summary>
    public class SongSelectSceneStrategy : IEzSkinEditorSceneStrategy
    {
        public EzSkinEditorSceneType SceneType => EzSkinEditorSceneType.SongSelect;

        public LocalisableString TabTitle => SkinEditorStrings.SongSelect;

        public Drawable CreateSceneContent(EzSkinEditorSceneContext context) =>
            new Container { RelativeSizeAxes = Axes.Both };

        public IReadOnlyList<EzSkinEditorSidebarGroupDefinition> CreateSidebarGroups(EzSkinEditorSceneContext context) =>
            Array.Empty<EzSkinEditorSidebarGroupDefinition>();
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Edit.Components;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Localisation;

namespace osu.Game.EzOsuGame.Edit.Scenes
{
    /// <summary>
    /// 选歌场景：游玩区由 Overlay 插入现成 <c>SoloSongSelect</c>，侧栏提供 Ez 选歌布局层。
    /// </summary>
    public class SongSelectSceneStrategy : IEzSkinEditorSceneStrategy
    {
        public EzSkinEditorSceneType SceneType => EzSkinEditorSceneType.SongSelect;

        public LocalisableString TabTitle => SkinEditorStrings.SongSelect;

        public bool SupportsHudLayer => true;

        public bool HoverExpandsSidebar => false;

        public IReadOnlyList<EzSkinEditorSidebarGroupDefinition> CreateSidebarGroups(EzSkinEditorSceneContext context) => new[]
        {
            new EzSkinEditorSidebarGroupDefinition
            {
                Id = GameplaySceneStrategy.GROUP_ID_HUD,
                Title = EzEditorStrings.SIDEBAR_TAB_HUD,
                CreateContent = () => new EzSkinEditorHudWorkspaceSection(),
            },
        };
    }
}

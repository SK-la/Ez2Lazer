// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Edit.Components;
using osu.Game.EzOsuGame.Localization;

namespace osu.Game.EzOsuGame.Edit.Scenes
{
    /// <summary>
    /// 选歌场景：游玩区由 Overlay 插入现成 <c>SoloSongSelect</c>，侧栏提供 Ez 选歌布局层。
    /// </summary>
    public class SongSelectSceneStrategy : IEzSkinEditorSceneStrategy
    {
        private EzSkinEditorSettingsGroup[]? sections;

        public EzEditorSceneType SceneType => EzEditorSceneType.SongSelectHud;

        public EzEditorSceneGroup Group => EzEditorSceneGroup.SongSelect;

        public LocalisableString TabTitle => EzEditorStrings.SIDEBAR_TAB_HUD;

        public bool SupportsHudLayer => true;

        public bool HoverExpandsSidebar => false;

        public IReadOnlyList<EzSkinEditorSettingsGroup> Sections => sections ??= new EzSkinEditorSettingsGroup[]
        {
            new EzSkinEditorHudWorkspaceSection(),
            new EzSkinEditorComponentLibrarySection(rulesetScoped: false),
        };
    }
}

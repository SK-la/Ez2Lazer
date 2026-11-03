// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Edit.Components;
using osu.Game.EzOsuGame.Localization;

namespace osu.Game.EzOsuGame.Edit.Scenes
{
    public class GameplaySceneStrategy : IEzSkinEditorSceneStrategy
    {
        private EzSkinEditorSettingsGroup[]? sections;

        public EzSkinEditorSceneType SceneType => EzSkinEditorSceneType.GameplayHud;

        public EzSkinEditorSceneGroup Group => EzSkinEditorSceneGroup.Gameplay;

        public LocalisableString TabTitle => EzEditorStrings.SIDEBAR_TAB_HUD;

        public bool SupportsHudLayer => true;

        public bool HoverExpandsSidebar => false;

        public IReadOnlyList<EzSkinEditorSettingsGroup> Sections => sections ??= new EzSkinEditorSettingsGroup[]
        {
            new EzSkinEditorHudWorkspaceSection(),
            new EzSkinEditorComponentLibrarySection(rulesetScoped: false),
            new EzSkinEditorComponentLibrarySection(rulesetScoped: true),
        };
    }
}

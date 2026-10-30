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
        public const string GROUP_ID_HUD = "hud";

        public EzSkinEditorSceneType SceneType => EzSkinEditorSceneType.Gameplay;

        public LocalisableString TabTitle => EzEditorStrings.SIDEBAR_TAB_HUD;

        public bool SupportsHudLayer => true;

        public bool HoverExpandsSidebar => false;

        public IReadOnlyList<EzSkinEditorSidebarGroupDefinition> CreateSidebarGroups(EzSkinEditorSceneContext context) => new[]
        {
            new EzSkinEditorSidebarGroupDefinition
            {
                Id = GROUP_ID_HUD,
                Title = EzEditorStrings.SIDEBAR_TAB_HUD,
                CreateContent = () => new EzSkinEditorHudWorkspaceSection(),
            },
        };
    }
}

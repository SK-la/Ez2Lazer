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

        public const string GROUP_ID_LIBRARY_GLOBAL = "library-global";

        public const string GROUP_ID_LIBRARY_RULESET = "library-ruleset";

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
            new EzSkinEditorSidebarGroupDefinition
            {
                Id = GROUP_ID_LIBRARY_GLOBAL,
                Title = EzEditorStrings.LIBRARY_GLOBAL_TITLE,
                Visible = hasEditingLayer(context.ActiveLayer),
                CreateContent = () => new EzSkinEditorComponentLibrarySection(rulesetScoped: false) { Title = EzEditorStrings.LIBRARY_GLOBAL_TITLE },
            },
            new EzSkinEditorSidebarGroupDefinition
            {
                Id = GROUP_ID_LIBRARY_RULESET,
                Title = EzEditorStrings.LIBRARY_RULESET_TITLE,
                Visible = isRulesetLibraryActive(context.ActiveLayer),
                CreateContent = () => new EzSkinEditorComponentLibrarySection(rulesetScoped: true) { Title = EzEditorStrings.LIBRARY_RULESET_TITLE },
            },
        };

        // 通用库在任何编辑层都在；模式库在模式专用层时追加为第二个库分组（各自一个导航按钮）。
        private static bool hasEditingLayer(EzSkinEditorLayer layer) => layer != EzSkinEditorLayer.HudOff;

        private static bool isRulesetLibraryActive(EzSkinEditorLayer layer)
            => layer is EzSkinEditorLayer.EzHudRuleset or EzSkinEditorLayer.EzPlayfieldRuleset;
    }
}

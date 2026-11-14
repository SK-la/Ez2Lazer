// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Edit.Components;
using osu.Game.EzOsuGame.Localization;

namespace osu.Game.EzOsuGame.Edit.Scenes
{
    public class GameplaySceneStrategy : IEzSkinEditorSceneStrategy
    {
        private EzSkinEditorSettingsGroup[]? sections;

        public EzEditorSceneType SceneType => EzEditorSceneType.GameplayHud;

        public EzEditorSceneGroup Group => EzEditorSceneGroup.Gameplay;

        public LocalisableString TabTitle => EzEditorStrings.SIDEBAR_TAB_HUD;

        public bool SupportsHudLayer => true;

        public bool HoverExpandsSidebar => false;

        public void ReleaseCachedDrawables() => sections = null;

        public IReadOnlyList<EzSkinEditorSettingsGroup> Sections => sections ??=
            new[] { new EzSkinEditorHudWorkspaceSection() }
                .Concat(EzSkinEditorComponentLibrarySections.Create(rulesetScoped: false))
                .Concat(EzSkinEditorComponentLibrarySections.Create(rulesetScoped: true))
                .ToArray();
    }
}

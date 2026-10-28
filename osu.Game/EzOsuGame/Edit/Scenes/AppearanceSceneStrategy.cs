// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Edit.Components;
using osu.Game.EzOsuGame.Localization;

namespace osu.Game.EzOsuGame.Edit.Scenes
{
    public class AppearanceSceneStrategy : IEzSkinEditorSceneStrategy
    {
        public EzSkinEditorSceneType SceneType => EzSkinEditorSceneType.Appearance;

        public LocalisableString TabTitle => EzEditorStrings.TAB_APPEARANCE;

        public Drawable CreateSceneContent(EzSkinEditorSceneContext context) =>
            context.UseLoopBeatmap
                ? new EzSkinEditorPreviewHost(context)
                : new EzSkinEditorAppearanceSceneContent(context);

        // 侧栏由 EzSkinEditorSidebar.ApplyWorkspace 提供五栏，此处不产出分组。
        public IReadOnlyList<EzSkinEditorSidebarGroupDefinition> CreateSidebarGroups(EzSkinEditorSceneContext context) =>
            Array.Empty<EzSkinEditorSidebarGroupDefinition>();
    }
}

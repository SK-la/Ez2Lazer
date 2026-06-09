// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Edit.Components;
using osu.Game.EzOsuGame.Edit.Settings.Sections;

namespace osu.Game.EzOsuGame.Edit.Scenes
{
    public class AppearanceSceneStrategy : IEzSkinEditorSceneStrategy
    {
        public EzSkinEditorSceneType SceneType => EzSkinEditorSceneType.Appearance;

        public LocalisableString TabTitle => "外观";

        public Drawable CreateSceneContent(EzSkinEditorSceneContext context) =>
            new EzSkinEditorPreviewHost(SceneType, context);

        public IReadOnlyList<EzSkinEditorSidebarGroupDefinition> CreateSidebarGroups(EzSkinEditorSceneContext context) =>
            new[]
            {
                new EzSkinEditorSidebarGroupDefinition
                {
                    Title = "纹理",
                    CreateContent = () => new EzSkinEditorTextureSettingsSection(),
                },
                new EzSkinEditorSidebarGroupDefinition
                {
                    Title = "舞台",
                    CreateContent = () => new EzSkinEditorStageSettingsSection(),
                },
            };
    }
}

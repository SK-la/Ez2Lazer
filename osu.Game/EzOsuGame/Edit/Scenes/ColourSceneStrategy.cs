// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Edit.Components;
using osu.Game.EzOsuGame.Edit.Settings.Sections;

namespace osu.Game.EzOsuGame.Edit.Scenes
{
    public class ColourSceneStrategy : IEzSkinEditorSceneStrategy
    {
        public EzSkinEditorSceneType SceneType => EzSkinEditorSceneType.Colour;

        public LocalisableString TabTitle => "颜色";

        public Drawable CreateSceneContent(EzSkinEditorSceneContext context) =>
            new EzSkinEditorPreviewHost(context);

        public IReadOnlyList<EzSkinEditorSidebarGroupDefinition> CreateSidebarGroups(EzSkinEditorSceneContext context) =>
            new[]
            {
                new EzSkinEditorSidebarGroupDefinition
                {
                    Title = "基础颜色",
                    CreateContent = () => new EzSkinEditorBaseColourSettingsSection(),
                },
                new EzSkinEditorSidebarGroupDefinition
                {
                    Title = "列配色",
                    CreateContent = () => new EzSkinEditorColumnColourSettingsSection(),
                },
            };
    }
}

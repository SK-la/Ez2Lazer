// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Edit.Components;
using osu.Game.EzOsuGame.Edit.Settings.Sections;
using osu.Game.EzOsuGame.Localization;

namespace osu.Game.EzOsuGame.Edit.Scenes
{
    public class ColourSceneStrategy : IEzSkinEditorSceneStrategy
    {
        public EzSkinEditorSceneType SceneType => EzSkinEditorSceneType.Colour;

        public LocalisableString TabTitle => EzEditorStrings.TAB_COLOUR;

        public Drawable CreateSceneContent(EzSkinEditorSceneContext context) =>
            new EzSkinEditorPreviewHost(context);

        public IReadOnlyList<EzSkinEditorSidebarGroupDefinition> CreateSidebarGroups(EzSkinEditorSceneContext context) => new[]
        {
            new EzSkinEditorSidebarGroupDefinition
            {
                Title = EzEditorStrings.GROUP_BASE_COLOURS,
                CreateContent = () => new EzSkinEditorBaseColourSettingsSection(),
            },
            new EzSkinEditorSidebarGroupDefinition
            {
                Title = EzEditorStrings.GROUP_COLUMN_COLOURS,
                CreateContent = () => new EzSkinEditorColumnColourSettingsSection(context.PreviewState),
            },
        };
    }
}

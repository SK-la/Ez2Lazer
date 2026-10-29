// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Edit.Settings.Sections;
using osu.Game.EzOsuGame.Localization;

namespace osu.Game.EzOsuGame.Edit.Scenes
{
    public class AppearanceSceneStrategy : IEzSkinEditorSceneStrategy
    {
        public const string GROUP_ID_TEXTURE = "texture";
        public const string GROUP_ID_STAGE = "stage";
        public const string GROUP_ID_SIZE = "size";
        public const string GROUP_ID_BASE_COLOURS = "base_colours";
        public const string GROUP_ID_COLUMN_COLOURS = "column_colours";
        public const string GROUP_ID_SKIN_SCOPED = "skin_scoped";

        public EzSkinEditorSceneType SceneType => EzSkinEditorSceneType.Appearance;

        public LocalisableString TabTitle => EzEditorStrings.TAB_APPEARANCE;

        public IReadOnlyList<EzSkinEditorSidebarGroupDefinition> CreateSidebarGroups(EzSkinEditorSceneContext context)
        {
            var kind = EzSkinEditorSkinKindResolver.Resolve(context.ActualSkin);

            var groups = new List<EzSkinEditorSidebarGroupDefinition>
            {
                new EzSkinEditorSidebarGroupDefinition
                {
                    Id = GROUP_ID_TEXTURE,
                    Title = EzEditorStrings.GROUP_TEXTURE,
                    CreateContent = () => new EzSkinEditorTextureSettingsSection(),
                },
                new EzSkinEditorSidebarGroupDefinition
                {
                    Id = GROUP_ID_STAGE,
                    Title = EzEditorStrings.GROUP_STAGE,
                    CreateContent = () => new EzSkinEditorStageSettingsSection(),
                },
                new EzSkinEditorSidebarGroupDefinition
                {
                    Id = GROUP_ID_SIZE,
                    Title = EzEditorStrings.SIDEBAR_TAB_SIZE,
                    CreateContent = () => new EzSkinEditorSizeSettingsSection(),
                },
                new EzSkinEditorSidebarGroupDefinition
                {
                    Id = GROUP_ID_BASE_COLOURS,
                    Title = EzEditorStrings.GROUP_BASE_COLOURS,
                    CreateContent = () => new EzSkinEditorBaseColourSettingsSection(),
                },
                new EzSkinEditorSidebarGroupDefinition
                {
                    Id = GROUP_ID_COLUMN_COLOURS,
                    Title = EzEditorStrings.GROUP_COLUMN_COLOURS,
                    CreateContent = () => new EzSkinEditorColumnColourSettingsSection(context.PreviewState),
                },
            };

            if (kind != EzSkinEditorSkinKind.None)
            {
                foreach (var provider in EzSkinEditorSkinScopedGroupRegistry.For(kind).Where(p => p.IsApplicable(context)))
                    groups.AddRange(provider.CreateGroups(context));
            }

            return groups;
        }
    }
}

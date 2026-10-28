// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Edit.Components;
using osu.Game.EzOsuGame.Edit.Settings.Sections;
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

        public IReadOnlyList<EzSkinEditorSidebarGroupDefinition> CreateSidebarGroups(EzSkinEditorSceneContext context)
        {
            var groups = new List<EzSkinEditorSidebarGroupDefinition>
            {
                new EzSkinEditorSidebarGroupDefinition
                {
                    Title = EzEditorStrings.GROUP_TEXTURE,
                    CreateContent = () => new EzSkinEditorTextureSettingsSection(),
                },
                new EzSkinEditorSidebarGroupDefinition
                {
                    Title = EzEditorStrings.GROUP_STAGE,
                    CreateContent = () => new EzSkinEditorStageSettingsSection(),
                },
            };

            var kind = EzSkinEditorSkinKindResolver.Resolve(context.ActualSkin);

            // One Appearance group: all dedicated settings for the equipped code/scripted skin.
            groups.AddRange(
                EzSkinEditorSkinScopedGroupRegistry.For(kind)
                                                  .Where(p => p.IsApplicable(context))
                                                  .SelectMany(p => p.CreateGroups(context))
                                                  .Select(g => new EzSkinEditorSidebarGroupDefinition
                                                  {
                                                      Title = EzEditorStrings.GROUP_SKIN_SPECIFIC,
                                                      CreateContent = g.CreateContent,
                                                  }));

            return groups;
        }
    }
}

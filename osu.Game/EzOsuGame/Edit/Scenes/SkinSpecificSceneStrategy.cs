// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Edit.Components;
using osu.Game.EzOsuGame.Localization;

namespace osu.Game.EzOsuGame.Edit.Scenes
{
    /// <summary>
    /// Skin-scoped settings for built-in Ez skins and scripted skins. Legacy skins stay on SkinIni.
    /// </summary>
    public class SkinSpecificSceneStrategy : IEzSkinEditorSceneStrategy
    {
        public EzSkinEditorSceneType SceneType => EzSkinEditorSceneType.SkinSpecific;

        public LocalisableString TabTitle => EzEditorStrings.TAB_SKIN_SPECIFIC;

        public Drawable CreateSceneContent(EzSkinEditorSceneContext context) =>
            new EzSkinEditorPreviewHost(context);

        public IReadOnlyList<EzSkinEditorSidebarGroupDefinition> CreateSidebarGroups(EzSkinEditorSceneContext context)
        {
            var kind = EzSkinEditorSkinKindResolver.Resolve(context.ActualSkin);

            return EzSkinEditorSkinScopedGroupRegistry.For(kind)
                                                     .Where(p => p.IsApplicable(context))
                                                     .SelectMany(p => p.CreateGroups(context))
                                                     .ToArray();
        }

        public Drawable? CreateSidebarFooter(EzSkinEditorSceneContext context)
        {
            var kind = EzSkinEditorSkinKindResolver.Resolve(context.ActualSkin);

            return EzSkinEditorSkinScopedGroupRegistry.For(kind)
                                                     .Where(p => p.IsApplicable(context))
                                                     .Select(p => p.CreateSidebarFooter(context))
                                                     .FirstOrDefault(f => f != null);
        }
    }
}

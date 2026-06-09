// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Localisation;

namespace osu.Game.EzOsuGame.Edit
{
    /// <summary>
    /// Each scene owns its scene content and sidebar grouping strategy.
    /// Add new scenes by implementing this interface and registering in <see cref="EzSkinEditorSceneRegistry"/>.
    /// </summary>
    public interface IEzSkinEditorSceneStrategy
    {
        EzSkinEditorSceneType SceneType { get; }

        LocalisableString TabTitle { get; }

        Drawable CreateSceneContent(EzSkinEditorSceneContext context);

        IReadOnlyList<EzSkinEditorSidebarGroupDefinition> CreateSidebarGroups(EzSkinEditorSceneContext context);

        /// <summary>
        /// Optional footer overlay pinned to the bottom of the sidebar (e.g. skin.ini save bar).
        /// </summary>
        Drawable? CreateSidebarFooter(EzSkinEditorSceneContext context) => null;
    }
}

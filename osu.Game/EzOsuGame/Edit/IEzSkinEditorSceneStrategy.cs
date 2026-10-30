// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Localisation;

namespace osu.Game.EzOsuGame.Edit
{
    /// <summary>
    /// Each scene owns its sidebar grouping strategy. Scenes with a HUD layer declare it here;
    /// scene content is hosted by <see cref="EzSkinEditorSceneCoordinator"/>.
    /// Add new scenes by implementing this interface and registering in <see cref="EzSkinEditorSceneRegistry"/>.
    /// </summary>
    public interface IEzSkinEditorSceneStrategy
    {
        EzSkinEditorSceneType SceneType { get; }

        LocalisableString TabTitle { get; }

        IReadOnlyList<EzSkinEditorSidebarGroupDefinition> CreateSidebarGroups(EzSkinEditorSceneContext context);

        /// <summary>
        /// Optional footer overlay pinned to the bottom of the sidebar (e.g. skin.ini save bar).
        /// </summary>
        Drawable? CreateSidebarFooter(EzSkinEditorSceneContext context) => null;

        /// <summary>
        /// Whether this scene presents an inserted screen whose skinnable HUD containers can be edited.
        /// </summary>
        bool SupportsHudLayer => false;

        /// <summary>
        /// Whether hovering the collapsed sidebar summons the settings panel. HUD scenes opt out so the
        /// panel does not cover the play area the user is dragging components in.
        /// </summary>
        bool HoverExpandsSidebar => true;
    }
}

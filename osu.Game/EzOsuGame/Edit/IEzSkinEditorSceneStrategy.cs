// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.EzOsuGame.Edit.Components;
using osu.Game.EzOsuGame.Layout;
using osu.Game.Rulesets;

namespace osu.Game.EzOsuGame.Edit
{
    /// <summary>
    /// Each scene owns its sidebar grouping strategy. Scenes with a HUD layer declare it here;
    /// scene content is hosted by <see cref="EzSkinEditorSceneCoordinator"/>.
    /// Add new scenes by implementing this interface and registering in <see cref="EzSkinEditorSceneRegistry"/>.
    /// </summary>
    public interface IEzSkinEditorSceneStrategy
    {
        /// <summary>
        /// 场景，一级分类。
        /// </summary>
        EzEditorSceneGroup Group { get; }

        /// <summary>
        /// 场景，二级分类
        /// </summary>
        EzEditorSceneType SceneType { get; }

        LocalisableString TabTitle { get; }

        /// <summary>
        /// Static availability (e.g. debug-only scenes). Runtime visibility is driven separately by
        /// the screen calling <c>SetSceneVisible</c>.
        /// </summary>
        bool IsAvailable => true;

        IReadOnlyList<EzSkinEditorSettingsGroup> Sections { get; }

        /// <summary>
        /// Supplies loaded rulesets before <see cref="Sections"/> is first read.
        /// Colouring scenes ask each ruleset for its own sidebar groups.
        /// </summary>
        void AttachRulesets(IReadOnlyList<Ruleset> rulesets)
        {
        }

        /// <summary>
        /// Optional footer pinned to the bottom of the sidebar (e.g. skin.ini save bar).
        /// Created once and reused.
        /// </summary>
        Drawable? Footer => null;

        /// <summary>
        /// Whether this scene presents an inserted screen whose skinnable HUD containers can be edited.
        /// </summary>
        bool SupportsHudLayer => false;

        /// <summary>
        /// Whether hovering the collapsed sidebar summons the settings panel. HUD scenes opt out so the
        /// panel does not cover the play area the user is dragging components in.
        /// </summary>
        bool HoverExpandsSidebar => true;

        /// <summary>
        /// Which Game layout directory persists this scene's HUD JSON when edited.
        /// </summary>
        EzLayoutRoot LayoutRoot => EzLayoutRoot.Theme;
    }
}

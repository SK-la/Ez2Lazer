// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osu.Framework.Localisation;

namespace osu.Game.EzOsuGame.Edit
{
    /// <summary>
    /// Describes one collapsible sidebar group produced by a scene strategy.
    /// Groups are not used to identify scenes — only strategies define grouping.
    /// </summary>
    public sealed class EzSkinEditorSidebarGroupDefinition
    {
        public LocalisableString Title { get; init; }

        public Func<Drawable> CreateContent { get; init; } = null!;

        public bool ExpandedByDefault { get; init; } = true;
    }
}

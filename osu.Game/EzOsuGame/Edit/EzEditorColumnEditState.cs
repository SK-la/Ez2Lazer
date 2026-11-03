// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;

namespace osu.Game.EzOsuGame.Edit
{
    /// <summary>
    /// Editor-scoped state for the extension columns' column-edit mode.
    /// </summary>
    /// <remarks>
    /// The editor owns the switch. A composer which provides extension columns raises <see cref="Available"/> while it is
    /// alive, which is what makes the switch visible, so rulesets without extension columns never show it.
    /// </remarks>
    public class EzEditorColumnEditState
    {
        /// <summary>
        /// Whether the current composer provides extension columns.
        /// </summary>
        public Bindable<bool> Available { get; } = new Bindable<bool>();

        /// <summary>
        /// Whether column-edit mode is on, which shows the columns' insert and remove controls.
        /// </summary>
        public Bindable<bool> ColumnEditMode { get; } = new Bindable<bool>();
    }
}

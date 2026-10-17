// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;

namespace osu.Game.Rulesets.Mania.Edit.Extension
{
    /// <summary>
    /// A single extension column.
    /// </summary>
    /// <remarks>
    /// <see cref="Number"/> is the column's channel number, kept contiguous from the left by <see cref="ExtensionColumnSet"/>.
    /// </remarks>
    public class ExtensionColumn
    {
        public BindableInt Number { get; } = new BindableInt();

        /// <summary>
        /// Notes owned by this column. Removing the column removes them with it.
        /// </summary>
        public BindableList<ExtensionNote> Notes { get; } = new BindableList<ExtensionNote>();

        public ExtensionColumn(int number)
        {
            Number.Value = number;
        }
    }
}

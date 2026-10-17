// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Input.Events;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Compose.Components;
using osuTK.Input;

namespace osu.Game.Rulesets.Mania.Edit.Extension
{
    /// <summary>
    /// Applies the generic selection operations to extension notes.
    /// </summary>
    /// <remarks>
    /// Movement and placement live in <see cref="ExtensionBlueprintContainer"/>, which is where the editor's snap provider and
    /// the column positions are available. Only deletion and the time-axis flip are handled here.
    /// </remarks>
    public partial class ExtensionSelectionHandler : SelectionHandler<ExtensionNote>
    {
        private readonly ExtensionColumnSet columnSet;

        [Resolved(CanBeNull = true)]
        private IEditorChangeHandler? changeHandler { get; set; }

        public ExtensionSelectionHandler(ExtensionColumnSet columnSet)        {
            this.columnSet = columnSet;
        }

        /// <summary>
        /// Whether right click deletes even when shift is not held.
        /// </summary>
        /// <remarks>
        /// The playfield sets this while a placement tool is active and clears it for the selection tool, so that a plain
        /// right click deletes while placing but opens the context menu while selecting. The dock mirrors it.
        /// </remarks>
        public bool RightClickAlwaysQuickDeletes { get; set; }

        protected override bool ShouldQuickDelete(MouseButtonEvent e)
        {
            if (RightClickAlwaysQuickDeletes && e.Button == MouseButton.Right)
                return true;

            return base.ShouldQuickDelete(e);
        }

        public void DeleteSelection() => DeleteSelected();

        protected override void DeleteItems(IEnumerable<ExtensionNote> items)
        {
            var notes = items.ToArray();

            if (notes.Length == 0)
                return;

            changeHandler?.BeginChange();

            foreach (var note in notes)
                columnSet.RemoveNote(note);

            changeHandler?.EndChange();
        }

        protected override void OnSelectionChanged()
        {
            base.OnSelectionChanged();

            var notes = SelectedItems.ToArray();

            // Columns are positional and mirroring them would reorder the dock, so only the time axis flips. Rotation and
            // scaling have no meaning for a time-based note, and their handlers report themselves unavailable by default.
            SelectionBox.CanFlipX = false;
            SelectionBox.CanFlipY = notes.Length > 1 && notes.Min(n => n.Time) < notes.Max(n => n.Time + n.Duration);
        }

        public override bool HandleFlip(Direction direction, bool flipOverOrigin)
        {
            if (direction != Direction.Vertical)
                return false;

            var notes = SelectedItems.ToArray();

            if (notes.Length < 2)
                return false;

            double start = notes.Min(n => n.Time);
            double end = notes.Max(n => n.Time + n.Duration);

            if (start >= end)
                return false;

            changeHandler?.BeginChange();

            foreach (var note in notes)
                note.Time = Math.Max(0, start + (end - (note.Time + note.Duration)));

            changeHandler?.EndChange();

            return true;
        }
    }
}

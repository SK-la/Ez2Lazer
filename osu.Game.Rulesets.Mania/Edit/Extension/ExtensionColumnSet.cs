// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;

namespace osu.Game.Rulesets.Mania.Edit.Extension
{
    /// <summary>
    /// Ordered, session-level collection of extension columns.
    /// </summary>
    public class ExtensionColumnSet
    {
        /// <summary>
        /// Audio track number reserved for the main audio track.
        /// </summary>
        public const int MAIN_AUDIO_TRACK_NUMBER = 1;

        /// <summary>
        /// Upper bound on the number of columns, guarding against runaway allocation.
        /// </summary>
        public const int MAX_COLUMN_COUNT = 64;

        public BindableList<ExtensionColumn> Columns { get; } = new BindableList<ExtensionColumn>();

        public ExtensionColumnSet()
        {
            // Subscribed here, before any renderer binds, so the numbers are already positional by the time the columns
            // rebuild their labels; that leaves no path which can add or remove a column without renumbering.
            Columns.BindCollectionChanged((_, _) => renumber(), true);
        }

        /// <summary>
        /// The rightmost column, if any.
        /// </summary>
        public ExtensionColumn? LastColumn => Columns.Count > 0 ? Columns[Columns.Count - 1] : null;

        /// <summary>
        /// Appends an empty column to the right.
        /// </summary>
        /// <returns>The appended column, or <c>null</c> when <see cref="MAX_COLUMN_COUNT"/> has been reached.</returns>
        public ExtensionColumn? Append() => Insert(Columns.Count);

        /// <summary>
        /// Inserts an empty column at <paramref name="index"/>.
        /// </summary>
        /// <returns>The inserted column, or <c>null</c> when <see cref="MAX_COLUMN_COUNT"/> has been reached.</returns>
        public ExtensionColumn? Insert(int index)
        {
            if (Columns.Count >= MAX_COLUMN_COUNT)
                return null;

            var column = new ExtensionColumn(index + 1);
            Columns.Insert(System.Math.Clamp(index, 0, Columns.Count), column);

            return column;
        }

        /// <summary>
        /// Removes <paramref name="column"/>, dropping its notes with it.
        /// </summary>
        /// <returns>Whether the column was removed.</returns>
        public bool Remove(ExtensionColumn column) => Columns.Remove(column);

        /// <summary>
        /// Removes the rightmost column.
        /// </summary>
        /// <returns>Whether a column was removed.</returns>
        public bool RemoveLast() => LastColumn != null && Remove(LastColumn);

        /// <summary>
        /// Adds a note to <paramref name="column"/>.
        /// </summary>
        public ExtensionNote AddNote(ExtensionColumn column, double time, double duration = 0)
        {
            var note = new ExtensionNote(time, duration);
            Add(column, note);
            return note;
        }

        /// <summary>
        /// Takes ownership of <paramref name="note"/>, putting it on <paramref name="column"/>.
        /// </summary>
        public void Add(ExtensionColumn column, ExtensionNote note)
        {
            note.Column = column;
            column.Notes.Add(note);
        }

        /// <summary>
        /// Removes a note from its owning column.
        /// </summary>
        public bool RemoveNote(ExtensionNote note) => note.Column?.Notes.Remove(note) ?? false;

        /// <summary>
        /// Moves a note to <paramref name="target"/> at <paramref name="newTime"/>.
        /// </summary>
        /// <remarks>
        /// The note keeps its identity (and therefore its definition changes with the destination).
        /// </remarks>
        public bool MoveNote(ExtensionNote note, ExtensionColumn target, double newTime)
        {
            if (note.Column == null)
                return false;

            note.Column.Notes.Remove(note);
            note.Column = target;
            note.Time = newTime;
            target.Notes.Add(note);

            return true;
        }

        private void renumber()
        {
            for (int i = 0; i < Columns.Count; i++)
                Columns[i].Number.Value = i + 1;
        }
    }
}

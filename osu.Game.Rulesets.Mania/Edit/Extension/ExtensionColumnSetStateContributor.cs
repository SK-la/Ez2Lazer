// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.IO;
using System.Text;
using osu.Game.Screens.Edit;

namespace osu.Game.Rulesets.Mania.Edit.Extension
{
    /// <summary>
    /// Stores the session-level extension columns in the editor's undo snapshots.
    /// </summary>
    /// <remarks>
    /// This is a placeholder encoding: it records only column ownership and each note's time and length, which is all the
    /// session model holds. When the on-disk format lands, only <see cref="Write"/> and <see cref="Restore"/> change;
    /// the surrounding undo mechanism stays as it is.
    /// </remarks>
    public class ExtensionColumnSetStateContributor : IEditorStateContributor
    {
        private readonly ExtensionColumnSet columnSet;

        public ExtensionColumnSetStateContributor(ExtensionColumnSet columnSet)
        {
            this.columnSet = columnSet;
        }

        public void Write(MemoryStream stream)
        {
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(columnSet.Columns.Count);

                foreach (var column in columnSet.Columns)
                {
                    writer.Write(column.Notes.Count);

                    foreach (var note in column.Notes)
                    {
                        writer.Write(note.Time);
                        writer.Write(note.Duration);
                    }
                }
            }
        }

        public void Restore(byte[] previous, byte[] current) => readInto(current);

        private void readInto(byte[] state)
        {
            var columns = new List<List<(double time, double duration)>>();

            using (var stream = new MemoryStream(state))
            using (var reader = new BinaryReader(stream, Encoding.UTF8, true))
            {
                if (stream.Length - stream.Position < sizeof(int))
                    return;

                int columnCount = reader.ReadInt32();

                // A truncated or foreign block is ignored rather than partially applied.
                if (columnCount < 0)
                    return;

                for (int i = 0; i < columnCount; i++)
                {
                    if (stream.Length - stream.Position < sizeof(int))
                        return;

                    int noteCount = reader.ReadInt32();

                    if (noteCount < 0 || (long)noteCount * sizeof(double) * 2 > stream.Length - stream.Position)
                        return;

                    var notes = new List<(double, double)>(noteCount);

                    for (int j = 0; j < noteCount; j++)
                        notes.Add((reader.ReadDouble(), reader.ReadDouble()));

                    columns.Add(notes);
                }
            }

            // Redundant columns go before missing ones are added, so the surviving ones keep their identity and the
            // drawables they own.
            while (columnSet.Columns.Count > columns.Count)
                columnSet.RemoveLast();

            while (columnSet.Columns.Count < columns.Count)
                columnSet.Append();

            for (int i = 0; i < columns.Count; i++)
            {
                var column = columnSet.Columns[i];
                var notes = columns[i];

                column.Notes.Clear();

                foreach (var (time, duration) in notes)
                    columnSet.AddNote(column, time, duration);
            }
        }
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Mania.UI;

namespace osu.Game.Rulesets.Mania.Skinning
{
    internal interface IColumnNote
    {
        void ForwardOnNoteSetChanged();
        void ForwardOnNoteSizeChanged();
        void ForwardOnColourChanged();
    }

    internal class ColumnWatcher
    {
        private readonly Column column;
        private readonly List<WeakReference<IColumnNote>> notes = new List<WeakReference<IColumnNote>>();

        public ColumnWatcher(Column column)
        {
            this.column = column;
            column.NoteSetChanged += watcherNoteSetChanged;
            column.NoteColourChanged += watcherNoteColourChanged;
            column.NoteSizeChanged += watcherNoteSizeChanged;
        }

        public void Add(IColumnNote note)
        {
            notes.Add(new WeakReference<IColumnNote>(note));
        }

        public void Remove(IColumnNote note)
        {
            notes.RemoveAll(wr => !wr.TryGetTarget(out var target) || ReferenceEquals(target, note));
        }

        private void watcherNoteSetChanged()
        {
            for (int i = notes.Count - 1; i >= 0; i--)
            {
                var wr = notes[i];

                if (!wr.TryGetTarget(out var target))
                {
                    notes.RemoveAt(i);
                    continue;
                }

                target.ForwardOnNoteSetChanged();
            }
        }

        private void watcherNoteColourChanged()
        {
            for (int i = notes.Count - 1; i >= 0; i--)
            {
                var wr = notes[i];

                if (!wr.TryGetTarget(out var target))
                {
                    notes.RemoveAt(i);
                    continue;
                }

                target.ForwardOnColourChanged();
            }
        }

        private void watcherNoteSizeChanged()
        {
            for (int i = notes.Count - 1; i >= 0; i--)
            {
                var wr = notes[i];

                if (!wr.TryGetTarget(out var target))
                {
                    notes.RemoveAt(i);
                    continue;
                }

                target.ForwardOnNoteSizeChanged();
            }
        }

        public void Unsubscribe()
        {
            column.NoteSetChanged -= watcherNoteSetChanged;
            column.NoteColourChanged -= watcherNoteColourChanged;
            column.NoteSizeChanged -= watcherNoteSizeChanged;
        }

        public bool IsEmpty => notes.All(wr => !wr.TryGetTarget(out _));

        private static readonly Dictionary<WeakReference<Column>, ColumnWatcher> watchers = new Dictionary<WeakReference<Column>, ColumnWatcher>();
        private static readonly object watcher_lock = new object();

        public static ColumnWatcher GetOrCreate(Column c)
        {
            lock (watcher_lock)
            {
                // Clean up dead references first
                cleanupDeadReferences();

                // Try to find existing watcher for this column
                ColumnWatcher? w = null;

                foreach (var kvp in watchers)
                {
                    if (kvp.Key.TryGetTarget(out var target) && ReferenceEquals(target, c))
                    {
                        w = kvp.Value;
                        break;
                    }
                }

                if (w == null)
                {
                    w = new ColumnWatcher(c);
                    var weakRef = new WeakReference<Column>(c);
                    watchers[weakRef] = w;
                }

                return w;
            }
        }

        public static void Remove(Column c, IColumnNote note)
        {
            lock (watcher_lock)
            {
                ColumnWatcher? w = null;
                WeakReference<Column>? keyToRemove = null;

                foreach (var kvp in watchers)
                {
                    if (kvp.Key.TryGetTarget(out var target) && ReferenceEquals(target, c))
                    {
                        w = kvp.Value;
                        keyToRemove = kvp.Key;
                        break;
                    }
                }

                if (w == null)
                    return;

                w.Remove(note);

                if (w.IsEmpty)
                {
                    w.Unsubscribe();
                    if (keyToRemove != null)
                        watchers.Remove(keyToRemove);
                }
            }
        }

        private static void cleanupDeadReferences()
        {
            var keysToRemove = watchers.Keys.Where(k => !k.TryGetTarget(out _)).ToList();

            foreach (var key in keysToRemove)
            {
                watchers.Remove(key);
            }
        }
    }
}

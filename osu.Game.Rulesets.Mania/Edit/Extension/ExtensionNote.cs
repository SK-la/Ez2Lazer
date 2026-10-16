// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;

namespace osu.Game.Rulesets.Mania.Edit.Extension
{
    /// <summary>
    /// An extension note. Session-level data only; never enters <c>EditorBeatmap</c>.
    /// </summary>
    public class ExtensionNote
    {
        /// <summary>
        /// The owning column. Maintained by <see cref="ExtensionColumnSet"/>; <see cref="ExtensionColumn.Notes"/> is the authoritative storage.
        /// </summary>
        public ExtensionColumn? Column { get; internal set; }

        public double Time { get; set; }

        /// <summary>
        /// Length in milliseconds. Zero for a tap note.
        /// </summary>
        public double Duration { get; set; }

        /// <summary>
        /// Whether this note is part of the extension selection. Session-level UI state.
        /// </summary>
        public BindableBool Selected { get; } = new BindableBool();

        public ExtensionNote(double time, double duration = 0)
        {
            Time = time;
            Duration = duration;
        }
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Mania.Edit.Extension
{
    /// <summary>
    /// Converts notes between the extension note model (<see cref="ExtensionNote"/>) and the playfield model.
    /// </summary>
    /// <remarks>
    /// The two models only overlap in time and length: an extension column has no playfield column, and an extension note carries no
    /// samples. Conversions therefore preserve length and identity, not skin or audio metadata.
    /// </remarks>
    public static class ExtensionNoteConversion
    {
        /// <summary>
        /// Creates the playfield object an extension note maps to.
        /// </summary>
        public static ManiaHitObject ToPlayfieldObject(ExtensionNote note, int column, double startTime)
            => note.Duration > 0
                ? new HoldNote { Column = column, StartTime = startTime, Duration = note.Duration }
                : new Note { Column = column, StartTime = startTime };

        /// <summary>
        /// Creates the extension note a playfield object maps to, keeping the object's start time and length.
        /// </summary>
        public static ExtensionNote ToExtensionNote(ManiaHitObject hitObject)
            => new ExtensionNote(hitObject.StartTime, hitObject is HoldNote hold ? hold.Duration : 0);
    }
}

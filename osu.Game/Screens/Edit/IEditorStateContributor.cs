// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;

namespace osu.Game.Screens.Edit
{
    /// <summary>
    /// Contributes a block of editor state that is not part of the encoded beatmap.
    /// </summary>
    /// <remarks>
    /// A contributor's block is stored in the same undo snapshot as the beatmap text, so one undo step restores both
    /// and the stack indices never diverge. Blocks are ordered, so a contributor must write and read the same layout.
    /// </remarks>
    public interface IEditorStateContributor
    {
        /// <summary>
        /// Writes the current state of this contributor.
        /// </summary>
        void Write(MemoryStream stream);

        /// <summary>
        /// Restores the state described by <paramref name="current"/>, using <paramref name="previous"/> for the parts
        /// where a diff is cheaper than a full rebuild.
        /// </summary>
        void Restore(byte[] previous, byte[] current);
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Game.Beatmaps;
using osu.Game.Skinning;

namespace osu.Game
{
    /// <summary>
    /// Interface for creating virtual playfield for skin editor.
    /// </summary>
    public interface ISkinEditorVirtualProvider
    {
        /// <summary>
        /// Creates a virtual playfield drawable for skin editing, using the provided skin and beatmap.
        /// </summary>
        /// <param name="skin">The skin to use for the playfield.</param>
        /// <param name="beatmap">The beatmap for the playfield.</param>
        /// <returns>The drawable playfield.</returns>
        Drawable CreateVirtualPlayfield(ISkin skin, IBeatmap beatmap);

        /// <summary>
        /// Creates a drawable for displaying the current skin's note.
        /// </summary>
        /// <param name="skin">The skin to use.</param>
        /// <returns>The drawable note display.</returns>
        Drawable CreateCurrentSkinNoteDisplay(ISkin skin);

        /// <summary>
        /// Creates a drawable for displaying the edited note.
        /// </summary>
        /// <param name="skin">The skin to use.</param>
        /// <returns>The drawable note display.</returns>
        Drawable CreateEditedNoteDisplay(ISkin skin);
    }
}

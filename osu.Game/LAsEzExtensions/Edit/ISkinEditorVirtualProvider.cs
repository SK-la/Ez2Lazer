// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Game.Skinning;

namespace osu.Game.LAsEzExtensions.Edit
{
    /// <summary>
    /// Interface for creating virtual playfield for skin editor.
    /// </summary>
    public interface ISkinEditorVirtualProvider
    {
        /// <summary>
        /// Creates the dynamic part (left) of the editor. Typically a virtual playfield
        /// which may depend on the beatmap.
        /// </summary>
        Drawable CreateDynamicPart(ISkin skin);

        /// <summary>
        /// Creates the static part (center) of the editor. Typically shows current vs edited
        /// note visuals and does not animate.
        /// </summary>
        Drawable CreateStaticPart(ISkin skin);

        /// <summary>
        /// Creates the parameters part (right) of the editor. Should provide controls to edit
        /// skin parameters for the ruleset.
        /// </summary>
        Drawable CreateParametersPart(ISkin skin);
    }
}

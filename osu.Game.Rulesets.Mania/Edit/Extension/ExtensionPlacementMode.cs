// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Mania.Edit.Extension
{
    /// <summary>
    /// What a placement gesture in the extension columns should create, following the composer's active tool.
    /// </summary>
    public enum ExtensionPlacementMode
    {
        /// <summary>
        /// No placement: the tool is not a note placement tool.
        /// </summary>
        None,

        /// <summary>
        /// Tap notes only, mirroring the playfield's note tool.
        /// </summary>
        Tap,

        /// <summary>
        /// Hold notes only, mirroring the playfield's hold note tool.
        /// </summary>
        Hold,
    }
}

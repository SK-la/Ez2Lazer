// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Skinning;

namespace osu.Game.EzOsuGame.Edit.Note
{
    /// <summary>
    /// Resolves note variant families from the equipped skin, not the editor preview wrapper.
    /// Legacy skins use 1/2/S; Ez-family skins use A/B/S/E/P.
    /// </summary>
    public static class EzSkinEditorNoteVariantResolver
    {
        public static bool UsesEzVariants(ISkin actualSkin) =>
            actualSkin is Ez2Skin or EzStyleProSkin or SbISkin or ScriptedSkinWrapper;
    }
}

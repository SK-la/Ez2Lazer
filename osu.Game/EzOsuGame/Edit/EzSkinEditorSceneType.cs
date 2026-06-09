// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Edit
{
    /// <summary>
    /// Identifies an Ez skin editor scene. Each value maps to a dedicated <see cref="IEzSkinEditorSceneStrategy"/>.
    /// </summary>
    public enum EzSkinEditorSceneType
    {
        Appearance,
        Size,
        Colour,
        SkinIni,
    }
}

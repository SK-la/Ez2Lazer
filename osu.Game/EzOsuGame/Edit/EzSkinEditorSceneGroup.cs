// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Edit
{
    /// <summary>
    /// Top-level grouping of <see cref="EzSkinEditorSceneType"/>. The scene bar switches between
    /// groups; the secondary scene rail lists the scenes inside the selected group.
    /// </summary>
    public enum EzSkinEditorSceneGroup
    {
        SongSelect,
        Gameplay,
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.Edit
{
    /// <summary>
    /// Whether the Ez skin editor currently owns the screen. While active, plugged screens must not
    /// push their own beatmap background, so the global (main menu) background stays alive and keeps
    /// rotating behind the editor.
    /// </summary>
    public static class EzSkinEditorActiveState
    {
        private static volatile bool active;

        public static bool Active => active;

        internal static void SetActive(bool active) => EzSkinEditorActiveState.active = active;
    }
}

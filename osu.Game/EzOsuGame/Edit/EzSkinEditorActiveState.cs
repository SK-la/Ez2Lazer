// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Screens;
using osu.Game.Screens.Backgrounds;

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
        private static BackgroundScreen? suppressedBackground;
        private static float suppressedBackgroundAlpha = 1;

        public static bool Active => active;

        internal static void SetActive(bool active) => EzSkinEditorActiveState.active = active;

        // Opening from the menu already exited this background. Opening from song select resumes it,
        // and that full-screen draw stays inside the scaled scene.
        // Hide beatmap backgrounds still referenced on the stack so the menu background below stays visible.
        // Do not touch BackgroundScreenDefault — re-plugged song select often has only that on top.
        internal static void SuppressBackgroundDraw(BackgroundScreen screen)
        {
            if (screen is not BackgroundScreenBeatmap)
                return;

            if (!screen.IsAlive)
                return;

            if (!ReferenceEquals(suppressedBackground, screen))
            {
                RestoreBackgroundDraw();
                suppressedBackground = screen;
                suppressedBackgroundAlpha = screen.Alpha;
            }

            screen.Alpha = 0;
        }

        internal static void RestoreBackgroundDraw()
        {
            if (suppressedBackground == null)
                return;

            if (suppressedBackground.IsAlive)
                suppressedBackground.Alpha = suppressedBackgroundAlpha;

            suppressedBackground = null;
            suppressedBackgroundAlpha = 1;
        }
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using osu.Framework.Screens;
using osu.Game.EzOsuGame.Edit;
using osu.Game.Screens.Backgrounds;

namespace osu.Game.Screens.Play
{
    public abstract partial class ScreenWithBeatmapBackground : OsuScreen
    {
        protected override BackgroundScreen? CreateBackground()
        {
            // While the Ez skin editor is open, keep the global (main menu) background alive behind the
            // plugged screen instead of replacing it with a beatmap background.
            if (EzSkinEditorActiveState.Active)
                return null;

            return new BackgroundScreenBeatmap(Beatmap.Value);
        }

        public void ApplyToBackground(Action<BackgroundScreenBeatmap> action)
        {
            Debug.Assert(this.IsCurrentScreen());

            base.ApplyToBackground(b =>
            {
                // The background may be the global one while the Ez skin editor is open.
                if (b is BackgroundScreenBeatmap beatmapBackground)
                    action.Invoke(beatmapBackground);
            });
        }
    }
}

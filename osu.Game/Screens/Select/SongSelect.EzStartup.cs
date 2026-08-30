// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Screens;
using osu.Game.EzOsuGame.Startup;
using osu.Game.Screens.Menu;

namespace osu.Game.Screens.Select
{
    public abstract partial class SongSelect
    {
        private bool ezDeferInitialCarouselFilter;

        partial void onEzSongSelectEntering(ScreenTransitionEvent e)
        {
            if (e.Last is MainMenu)
            {
                ezDeferInitialCarouselFilter = true;
                EzSongSelectEnterTrace.RecordSongSelectEntering();
            }
        }

        private partial double? onEzGetFilterScheduleDelay(bool isFirstFilter)
        {
            if (ezDeferInitialCarouselFilter && isFirstFilter)
            {
                ezDeferInitialCarouselFilter = false;
                return EzStartupTuning.SongSelectEnterFilterDelayMs;
            }

            return null;
        }

        partial void onEzCarouselItemsPresented() => EzSongSelectEnterTrace.RecordCarouselReady();
    }
}

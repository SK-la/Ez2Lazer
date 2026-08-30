// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Threading;
using osu.Game.EzOsuGame.Startup;
using osu.Game.Screens.Select;

namespace osu.Game.Screens.Menu
{
    public partial class MainMenu : IEzStartupSongSelectPreloadHost
    {
        private SoloSongSelect? preloadedSongSelect;
        private bool songSelectPreloadScheduled;
        private bool songSelectConsumed = true;
        private double? mainMenuEnteredAt;
        private ScheduledDelegate? songSelectUiSettleDelegate;

        [Resolved(CanBeNull = true)]
        private ISongSelectScreenFactory? songSelectScreenFactory { get; set; }

        public void ScheduleSongSelectPreload()
        {
            if (songSelectPreloadScheduled || songSelectScreenFactory == null)
                return;

            songSelectUiSettleDelegate?.Cancel();
            songSelectUiSettleDelegate = null;

            songSelectPreloadScheduled = true;
            songSelectConsumed = false;

            preloadedSongSelect?.Expire();
            preloadedSongSelect = null;

            var screen = songSelectScreenFactory.Create();

            LoadComponentAsync(screen, loaded =>
            {
                songSelectPreloadScheduled = false;
                preloadedSongSelect = loaded;
            });
        }

        public void ScheduleSongSelectPreloadAfterUiSettle()
        {
            if (songSelectPreloadScheduled || songSelectScreenFactory == null)
                return;

            songSelectUiSettleDelegate?.Cancel();

            if (!mainMenuEnteredAt.HasValue)
            {
                ScheduleSongSelectPreload();
                return;
            }

            double elapsed = Clock.CurrentTime - mainMenuEnteredAt.Value;
            double delay = Math.Max(0, EzStartupTuning.MainMenuUiSettleBeforeHeavyWorkMs - elapsed);

            if (delay <= 0)
            {
                ScheduleSongSelectPreload();
                return;
            }

            songSelectUiSettleDelegate = Scheduler.AddDelayed(ScheduleSongSelectPreload, delay);
        }

        internal void NotifyMainMenuEnteredForStartup()
        {
            mainMenuEnteredAt = Clock.CurrentTime;
        }

        public bool TryConsumePreloadedSongSelect(out SoloSongSelect? screen)
        {
            if (preloadedSongSelect != null && !songSelectConsumed && isSongSelectPreloadReady(preloadedSongSelect))
            {
                screen = preloadedSongSelect;
                songSelectConsumed = true;
                preloadedSongSelect = null;
                return true;
            }

            screen = null;
            return false;
        }

        public void LogSongSelectPreloadStatus(string context)
        {
        }

        private static bool isSongSelectPreloadReady(SoloSongSelect screen)
            => screen.LoadState >= LoadState.Ready;
    }
}

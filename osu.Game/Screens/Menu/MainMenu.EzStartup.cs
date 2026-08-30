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

            EzStartupTrace.Log("MainMenu.ScheduleSongSelectPreload started");
            var screen = songSelectScreenFactory.Create();

            LoadComponentAsync(screen, loaded =>
            {
                songSelectPreloadScheduled = false;
                preloadedSongSelect = loaded;

                EzStartupTrace.Log(
                    $"MainMenu.ScheduleSongSelectPreload finished LoadState={loaded.LoadState} preloadReady={isSongSelectPreloadReady(loaded)}");
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

            EzStartupTrace.Log($"MainMenu.ScheduleSongSelectPreloadAfterUiSettle delay={delay:0}ms");
            songSelectUiSettleDelegate = Scheduler.AddDelayed(ScheduleSongSelectPreload, delay);
        }

        internal void NotifyMainMenuEnteredForStartup()
        {
            mainMenuEnteredAt = Clock.CurrentTime;
        }

        public bool TryConsumePreloadedSongSelect(out SoloSongSelect? screen)
        {
            LogSongSelectPreloadStatus("TryConsumePreloadedSongSelect");

            if (preloadedSongSelect != null && !songSelectConsumed && isSongSelectPreloadReady(preloadedSongSelect))
            {
                screen = preloadedSongSelect;
                songSelectConsumed = true;
                preloadedSongSelect = null;
                EzStartupTrace.Log($"MainMenu.TryConsumePreloadedSongSelect hit (LoadState={screen.LoadState})");
                return true;
            }

            screen = null;
            EzStartupTrace.Log(
                $"MainMenu.TryConsumePreloadedSongSelect miss (preloaded={preloadedSongSelect != null}, consumed={songSelectConsumed}, loadState={preloadedSongSelect?.LoadState})");
            return false;
        }

        public void LogSongSelectPreloadStatus(string context)
        {
            bool preloadReady = preloadedSongSelect != null && isSongSelectPreloadReady(preloadedSongSelect);

            EzStartupTrace.Log(
                $"MainMenu[{context}] songSelectScheduled={songSelectPreloadScheduled} songSelectPreloaded={preloadedSongSelect != null} " +
                $"songSelectPreloadReady={preloadReady} songSelectLoadState={preloadedSongSelect?.LoadState} songSelectConsumed={songSelectConsumed}");
        }

        private static bool isSongSelectPreloadReady(SoloSongSelect screen)
            => screen.LoadState >= LoadState.Ready;
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Threading;
using osu.Game.Database;

namespace osu.Game.EzOsuGame.Startup
{
    /// <summary>
    /// Inserts Ez preload tasks into gaps in the official startup timeline without reordering OsuGame component load.
    /// </summary>
    public partial class EzStartupWorkCoordinator : CompositeDrawable, IEzStartupWorkCoordinator
    {
        /// <summary>
        /// Brief buffer after BDSP startup processing before song select preload begins.
        /// </summary>
        private const double bdsp_finished_song_select_buffer_ms = 500;

        private const double late_song_select_delay_ms = 1000;

        /// <summary>
        /// Defer detach until after song-select preload window; avoids stacking with BDSP Ez backfill.
        /// </summary>
        private const double main_menu_detach_delay_ms = 7000;

        private const double bdsp_poll_interval_ms = 500;

        private const double bdsp_availability_poll_interval_ms = 100;

        private IEzStartupContentPreloader preloader = null!;
        private IEzStartupSongSelectPreloadHost? songSelectHost;
        private bool detachWarmupScheduled;
        private bool songSelectScheduleRegistered;
        private bool songSelectPreloadStarted;
        private bool mainMenuEntered;
        private ScheduledDelegate? songSelectFallbackDelegate;

        [Resolved(CanBeNull = true)]
        private BackgroundDataStoreProcessor? backgroundDataStoreProcessor { get; set; }

        public EzStartupWorkCoordinator()
        {
            RelativeSizeAxes = Axes.Both;
        }

        public void Configure(IEzStartupContentPreloader preloader)
        {
            this.preloader = preloader;
        }

        public void OnPrepareMenuLoad()
        {
            switch (EzStartupTuning.PreloadTiming)
            {
                case EzStartupPreloadTiming.PipelineDefault:
                    // Settings preload is started from OsuGame.loadStartupContentPreloader.
                    break;

                case EzStartupPreloadTiming.PipelineEarlySongSelect:
                    preloader.BeginLight();
                    break;

                case EzStartupPreloadTiming.PipelineLateSongSelect:
                    preloader.BeginLight();
                    break;

                case EzStartupPreloadTiming.ImmediateAll:
                    preloader.BeginLight();
                    preloader.ScheduleDetachWarmup();
                    break;
            }
        }

        public void OnLoadMenu()
        {
            tryStartSongSelectPreloadAfterBdsp("LoadMenu");
        }

        public void OnMainMenuOffscreenReady(IEzStartupSongSelectPreloadHost songSelectHost)
        {
            this.songSelectHost = songSelectHost;
            registerSongSelectPreloadSchedule();
        }

        public void OnMainMenuEntered(IEzStartupSongSelectPreloadHost songSelectHost)
        {
            this.songSelectHost = songSelectHost;
            mainMenuEntered = true;

            // Fallback if offscreen MainMenu load finished after Push.
            if (!songSelectScheduleRegistered)
                registerSongSelectPreloadSchedule();

            switch (EzStartupTuning.PreloadTiming)
            {
                case EzStartupPreloadTiming.PipelineDefault:
                case EzStartupPreloadTiming.PipelineEarlySongSelect:
                case EzStartupPreloadTiming.PipelineLateSongSelect:
                case EzStartupPreloadTiming.ImmediateAll:
                    scheduleDetachWarmupWhenSafe(main_menu_detach_delay_ms);
                    break;
            }
        }

        private void registerSongSelectPreloadSchedule()
        {
            if (songSelectScheduleRegistered)
                return;

            songSelectScheduleRegistered = true;

            switch (EzStartupTuning.PreloadTiming)
            {
                case EzStartupPreloadTiming.PipelineDefault:
                    scheduleSongSelectPreloadWhenBdspFinished();
                    break;

                case EzStartupPreloadTiming.PipelineEarlySongSelect:
                case EzStartupPreloadTiming.ImmediateAll:
                    startSongSelectPreload("offscreen-ready");
                    break;

                case EzStartupPreloadTiming.PipelineLateSongSelect:
                    Scheduler.AddDelayed(() => startSongSelectPreload("offscreen-late"), late_song_select_delay_ms);
                    break;
            }
        }

        private void scheduleSongSelectPreloadWhenBdspFinished()
        {
            if (backgroundDataStoreProcessor == null)
            {
                Scheduler.AddDelayed(scheduleSongSelectPreloadWhenBdspFinished, bdsp_availability_poll_interval_ms);
                ensureSongSelectFallbackScheduled();
                return;
            }

            if (backgroundDataStoreProcessor.IsStartupProcessingFinished)
            {
                Scheduler.AddDelayed(() => startSongSelectPreload("BDSP-already-finished"), bdsp_finished_song_select_buffer_ms);
                return;
            }

            backgroundDataStoreProcessor.StartupProcessingFinished += onBdspStartupProcessingFinished;
            ensureSongSelectFallbackScheduled();
        }

        private void tryStartSongSelectPreloadAfterBdsp(string reason)
        {
            if (songSelectPreloadStarted || !songSelectScheduleRegistered || backgroundDataStoreProcessor == null)
                return;

            if (!backgroundDataStoreProcessor.IsStartupProcessingFinished)
                return;

            songSelectFallbackDelegate?.Cancel();
            songSelectFallbackDelegate = null;
            backgroundDataStoreProcessor.StartupProcessingFinished -= onBdspStartupProcessingFinished;
            Scheduler.AddDelayed(() => startSongSelectPreload($"{reason}-BDSP-already-finished"), bdsp_finished_song_select_buffer_ms);
        }

        private void ensureSongSelectFallbackScheduled()
        {
            if (songSelectFallbackDelegate != null)
                return;

            songSelectFallbackDelegate = Scheduler.AddDelayed(() => startSongSelectPreload("fallback-timeout"), EzStartupTuning.SongSelectPreloadFallbackDelayMs);
        }

        private void onBdspStartupProcessingFinished()
        {
            Scheduler.AddDelayed(() => startSongSelectPreload("BDSP-finished"), bdsp_finished_song_select_buffer_ms);
        }

        private void startSongSelectPreload(string reason)
        {
            if (songSelectPreloadStarted)
                return;

            songSelectPreloadStarted = true;
            songSelectFallbackDelegate?.Cancel();
            songSelectFallbackDelegate = null;

            if (backgroundDataStoreProcessor != null)
                backgroundDataStoreProcessor.StartupProcessingFinished -= onBdspStartupProcessingFinished;

            if (songSelectHost == null)
                return;

            if (mainMenuEntered)
                songSelectHost.ScheduleSongSelectPreloadAfterUiSettle();
            else
                songSelectHost.ScheduleSongSelectPreload();
        }

        private void scheduleDetachWarmupWhenSafe(double delayMs)
        {
            if (detachWarmupScheduled)
                return;

            detachWarmupScheduled = true;

            Scheduler.AddDelayed(() =>
            {
                if (backgroundDataStoreProcessor?.IsEzRealmMetadataBackfillRunning == true)
                {
                    detachWarmupScheduled = false;
                    scheduleDetachWarmupWhenSafe(bdsp_poll_interval_ms);
                    return;
                }

                preloader.ScheduleDetachWarmup();
            }, delayMs);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (backgroundDataStoreProcessor != null)
                backgroundDataStoreProcessor.StartupProcessingFinished -= onBdspStartupProcessingFinished;

            songSelectFallbackDelegate?.Cancel();
            base.Dispose(isDisposing);
        }
    }
}

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
            EzStartupTrace.Log($"Coordinator.OnPrepareMenuLoad timing={EzStartupTuning.PreloadTiming} bdspDelay={EzStartupTuning.BdspStartupBackfillDelaySeconds}s");

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
            songSelectHost?.LogSongSelectPreloadStatus("Intro.LoadMenu");
            preloader.LogStatus("Intro.LoadMenu");
            EzStartupTrace.Log("Coordinator.OnLoadMenu (Intro Push MainMenu imminent)");

            tryStartSongSelectPreloadAfterBdsp("LoadMenu");
        }

        public void OnMainMenuOffscreenReady(IEzStartupSongSelectPreloadHost songSelectHost)
        {
            this.songSelectHost = songSelectHost;

            EzStartupTrace.Log(
                $"Coordinator.OnMainMenuOffscreenReady timing={EzStartupTuning.PreloadTiming} bdspFinished={backgroundDataStoreProcessor?.IsStartupProcessingFinished}");

            songSelectHost.LogSongSelectPreloadStatus("OnMainMenuOffscreenReady");
            registerSongSelectPreloadSchedule();
        }

        public void OnMainMenuEntered(IEzStartupSongSelectPreloadHost songSelectHost)
        {
            this.songSelectHost = songSelectHost;
            mainMenuEntered = true;

            EzStartupTrace.Log(
                $"Coordinator.OnMainMenuEntered timing={EzStartupTuning.PreloadTiming} bdspFinished={backgroundDataStoreProcessor?.IsStartupProcessingFinished} " +
                $"bdspEzBackfillRunning={backgroundDataStoreProcessor?.IsEzRealmMetadataBackfillRunning} songSelectPreloadStarted={songSelectPreloadStarted}");
            preloader.LogStatus("OnMainMenuEntered");
            songSelectHost.LogSongSelectPreloadStatus("OnMainMenuEntered");

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
                    EzStartupTrace.Log($"Coordinator scheduling late song select preload in {late_song_select_delay_ms}ms");
                    Scheduler.AddDelayed(() => startSongSelectPreload("offscreen-late"), late_song_select_delay_ms);
                    break;
            }
        }

        private void scheduleSongSelectPreloadWhenBdspFinished()
        {
            if (backgroundDataStoreProcessor == null)
            {
                EzStartupTrace.Log($"Coordinator BDSP not ready yet; retry in {bdsp_availability_poll_interval_ms}ms");
                Scheduler.AddDelayed(scheduleSongSelectPreloadWhenBdspFinished, bdsp_availability_poll_interval_ms);
                ensureSongSelectFallbackScheduled();
                return;
            }

            if (backgroundDataStoreProcessor.IsStartupProcessingFinished)
            {
                EzStartupTrace.Log($"Coordinator BDSP already finished; song select preload in {bdsp_finished_song_select_buffer_ms}ms");
                Scheduler.AddDelayed(() => startSongSelectPreload("BDSP-already-finished"), bdsp_finished_song_select_buffer_ms);
                return;
            }

            backgroundDataStoreProcessor.StartupProcessingFinished += onBdspStartupProcessingFinished;
            EzStartupTrace.Log($"Coordinator waiting for BDSP startup processing (fallback in {EzStartupTuning.SongSelectPreloadFallbackDelayMs}ms)");
            ensureSongSelectFallbackScheduled();
        }

        private void tryStartSongSelectPreloadAfterBdsp(string reason)
        {
            if (songSelectPreloadStarted || !songSelectScheduleRegistered || backgroundDataStoreProcessor == null)
                return;

            if (!backgroundDataStoreProcessor.IsStartupProcessingFinished)
                return;

            EzStartupTrace.Log($"Coordinator {reason}: BDSP finished before preload started; scheduling song select preload");
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
            EzStartupTrace.Log(
                $"Coordinator BDSP finished → next: songSelect (+{bdsp_finished_song_select_buffer_ms}ms), detach (main menu +{main_menu_detach_delay_ms}ms if not started)");
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
            {
                EzStartupTrace.Log($"Coordinator song select preload skipped ({reason}, no host)");
                return;
            }

            EzStartupTrace.Log($"Coordinator starting song select preload ({reason}) mainMenuEntered={mainMenuEntered}");

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
            EzStartupTrace.Log($"Coordinator scheduling detach warmup check in {delayMs}ms");

            Scheduler.AddDelayed(() =>
            {
                if (backgroundDataStoreProcessor?.IsEzRealmMetadataBackfillRunning == true)
                {
                    EzStartupTrace.Log("Coordinator detach warmup deferred (BDSP Ez backfill still running)");
                    detachWarmupScheduled = false;
                    scheduleDetachWarmupWhenSafe(bdsp_poll_interval_ms);
                    return;
                }

                EzStartupTrace.Log("Coordinator starting detach warmup");
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

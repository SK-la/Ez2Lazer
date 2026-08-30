// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Database;
using osu.Game.Overlays;

namespace osu.Game.EzOsuGame.Startup
{
    /// <summary>
    /// Preloads settings sections and song select during startup.
    /// Dependencies are injected via <see cref="Configure"/> to avoid fragile DI ordering during startup.
    /// Scheduling is driven by <see cref="EzStartupWorkCoordinator"/>.
    /// </summary>
    public partial class EzStartupContentPreloader : CompositeDrawable, IEzStartupContentPreloader
    {
        private SettingsOverlay? settings;
        private BeatmapStore? beatmapStore;

        private bool lightStarted;
        private bool detachWarmupScheduled;
        private bool detachWarmupFinished;

        public EzStartupContentPreloader()
        {
            RelativeSizeAxes = Axes.Both;
        }

        public void Configure(SettingsOverlay settings, BeatmapStore beatmapStore, ISongSelectScreenFactory songSelectScreenFactory)
        {
            this.settings = settings;
            this.beatmapStore = beatmapStore;
        }

        public void BeginLight() => ScheduleSettingsPreload();

        public void ScheduleSettingsPreload()
        {
            if (lightStarted)
            {
                EzStartupTrace.Log("Preloader.ScheduleSettingsPreload skipped (already started)");
                return;
            }

            lightStarted = true;
            EzStartupTrace.Log("Preloader.ScheduleSettingsPreload");
            tryScheduleSettingsPreload();
        }

        private void tryScheduleSettingsPreload()
        {
            if (settings == null || !settings.IsLoaded)
            {
                Scheduler.Add(tryScheduleSettingsPreload);
                return;
            }

            EzStartupTrace.Log("Preloader calling Settings.BeginLoadingSections");
            settings.BeginLoadingSections();
        }

        public void ScheduleDetachWarmup()
        {
            if (detachWarmupScheduled || beatmapStore == null)
                return;

            detachWarmupScheduled = true;
            EzStartupTrace.Log("Preloader.ScheduleDetachWarmup started (background thread)");

            var store = beatmapStore;
            Task.Run(() =>
            {
                try
                {
                    store.GetBeatmapSets(CancellationToken.None);
                    detachWarmupFinished = true;
                    EzStartupTrace.Log("Preloader.ScheduleDetachWarmup finished");
                }
                catch (Exception e)
                {
                    EzStartupTrace.Log($"Preloader.ScheduleDetachWarmup failed: {e.Message}");
                }
            });
        }

        public void LogStatus(string context)
        {
            bool settingsLoaded = settings?.AreSectionsLoaded ?? false;
            bool settingsDisplayReady = settings?.AreSectionsReadyForDisplay ?? false;

            EzStartupTrace.Log(
                $"Preloader[{context}] settingsRequested={lightStarted} settingsLoaded={settingsLoaded} settingsDisplayReady={settingsDisplayReady} " +
                $"detachScheduled={detachWarmupScheduled} detachFinished={detachWarmupFinished}");
            settings?.LogPreloadStatus(context);
        }

        public bool AreSettingsLoaded => settings?.AreSectionsLoaded ?? false;

        public bool AreSettingsReadyForDisplay => settings?.AreSectionsReadyForDisplay ?? false;
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
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

        private bool lightStarted;

        public EzStartupContentPreloader()
        {
            RelativeSizeAxes = Axes.Both;
        }

        public void Configure(SettingsOverlay settings, ISongSelectScreenFactory songSelectScreenFactory)
        {
            this.settings = settings;
        }

        public void BeginLight() => ScheduleSettingsPreload();

        public void ScheduleSettingsPreload()
        {
            if (lightStarted)
                return;

            lightStarted = true;
            tryScheduleSettingsPreload();
        }

        private void tryScheduleSettingsPreload()
        {
            if (settings == null || !settings.IsLoaded)
            {
                Scheduler.Add(tryScheduleSettingsPreload);
                return;
            }

            settings.BeginLoadingSections();
        }

        public void LogStatus(string context)
        {
        }

        public bool AreSettingsLoaded => settings?.AreSectionsLoaded ?? false;

        public bool AreSettingsReadyForDisplay => settings?.AreSectionsReadyForDisplay ?? false;
    }
}

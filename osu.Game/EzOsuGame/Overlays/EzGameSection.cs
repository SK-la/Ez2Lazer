// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics;
using osu.Game.Overlays.Settings;

namespace osu.Game.EzOsuGame.Overlays
{
    public partial class EzGameSection : SettingsSection
    {
        private const int subsections_per_frame = 2;

        private readonly SettingsSubsection[] subsections;
        private int loadedSubsectionCount;

        public bool AreSubsectionsLoaded { get; private set; }

        [Resolved]
        private GameHost gameHost { get; set; } = null!;

        public override LocalisableString Header => EzSettingsStrings.EZ_GAME_SECTION_HEADER;

        public override bool MatchingFilter
        {
            get => base.MatchingFilter;
            set => base.MatchingFilter = value || (FilteringActive && loadedSubsectionCount < subsections.Length);
        }

        public override Drawable CreateIcon() => new SpriteIcon
        {
            Icon = OsuIcon.GameplayB
        };

        protected override bool ShowSubsectionIndex => true;

        public EzGameSection()
        {
            subsections = new SettingsSubsection[]
            {
                new EzUISettings(),
                new EzWeatherSettings(),
                new EzPetSettings(),
                new EzGameplaySettings(),
                new EzEditorSettings(),
                new EzQuickRotationSettings(),
                new EzStdSettings(),
                new EzGameModeSettings(),
                new EzAnalysisSettings(),
                new ServerSettings(),
                new EzExperimentalSettings(),
            };
        }

        protected override void LoadAsyncComplete()
        {
            base.LoadAsyncComplete();
            loadNextSubsectionBatch();
        }

        private void loadNextSubsectionBatch()
        {
            if (IsDisposed)
                return;

            int endIndex = Math.Min(loadedSubsectionCount + subsections_per_frame, subsections.Length);
            var batch = new List<SettingsSubsection>(endIndex - loadedSubsectionCount);

            for (int i = loadedSubsectionCount; i < endIndex; i++)
                batch.Add(subsections[i]);

            try
            {
                LoadComponentsAsync(batch, loaded =>
                {
                    foreach (SettingsSubsection subsection in loaded)
                        Add(subsection);

                    loadedSubsectionCount = endIndex;
                    AreSubsectionsLoaded = loadedSubsectionCount == subsections.Length;
                    RefreshIndexedSubsections();

                    if (loadedSubsectionCount < subsections.Length)
                        gameHost.UpdateThread.Scheduler.AddDelayed(loadNextSubsectionBatch, 1);
                    else
                        RefreshParentSearchFilter();
                }, scheduler: gameHost.UpdateThread.Scheduler);
            }
            catch (ObjectDisposedException)
            {
                // The owning settings panel may expire between the scheduled batch check and component load scheduling.
            }
        }
    }
}

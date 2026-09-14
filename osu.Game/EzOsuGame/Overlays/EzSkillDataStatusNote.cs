// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Logging;
using osu.Game.Database;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.Skills;
using osu.Game.Overlays;
using osu.Game.Overlays.Settings;

namespace osu.Game.EzOsuGame.Overlays
{
    /// <summary>
    /// Readout for the chart / player skill chains. Measures itself once on load (the sweep walks every
    /// mania chart plus the three facet tables, so it stays on a background task) and reports through a
    /// <see cref="SettingsNote"/>: informational when everything is current, warning when a pass would
    /// still have work to do, critical when the measurement itself failed. The note's colour is the
    /// status marker; its text carries the per-facet counts.
    /// </summary>
    public partial class EzSkillDataStatusNote : CompositeDrawable
    {
        private readonly EzSkillStore? skillStore;
        private readonly BackgroundDataStoreProcessor? backgroundDataStoreProcessor;

        private readonly Bindable<SettingsNote.Data?> statusNote = new Bindable<SettingsNote.Data?>();

        /// <summary>
        /// Whether a measurement is currently in flight, so a refresh button can disable itself.
        /// </summary>
        public readonly BindableBool Measuring = new BindableBool();

        private int measuring;

        public EzSkillDataStatusNote(EzSkillStore? skillStore, BackgroundDataStoreProcessor? backgroundDataStoreProcessor)
        {
            this.skillStore = skillStore;
            this.backgroundDataStoreProcessor = backgroundDataStoreProcessor;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            InternalChild = new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Padding = SettingsPanel.CONTENT_PADDING,
                Child = new SettingsNote
                {
                    RelativeSizeAxes = Axes.X,
                    Current = { BindTarget = statusNote },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // No button press needed: the status is measured as soon as the settings panel is shown.
            Measure();
        }

        /// <summary>
        /// Re-measures the chains. A call made while a measurement is already running is ignored.
        /// </summary>
        public void Measure()
        {
            if (skillStore == null || Interlocked.CompareExchange(ref measuring, 1, 0) != 0)
                return;

            Measuring.Value = true;
            statusNote.Value = note(EzSettingsStrings.SKILL_DATA_STATUS_MEASURING, SettingsNote.Type.Informational);

            Task.Run(() =>
            {
                bool failed = false;
                EzSkillDataStatus? status = null;
                int stalePlayerCount = 0;
                bool backfillRunning = false;

                try
                {
                    status = skillStore.GetSkillDataStatus();
                    stalePlayerCount = skillStore.GetStalePlayerSkillUsernames().Count;
                    backfillRunning = backgroundDataStoreProcessor?.IsEzRealmMetadataBackfillRunning == true;
                }
                catch (Exception e)
                {
                    Logger.Log($"[EzSkills] Skill data status measurement failed: {e}",
                        Ez2ConfigManager.LOGGER_NAME, LogLevel.Error);
                    failed = true;
                }

                // Only the Realm reads stay off-thread; the note text is composed back on the update thread
                // so its localisable labels resolve against the UI culture.
                Schedule(() =>
                {
                    Measuring.Value = false;
                    Interlocked.Exchange(ref measuring, 0);

                    statusNote.Value = failed || status == null
                        ? note(EzSettingsStrings.SKILL_DATA_STATUS_FAILED, SettingsNote.Type.Critical)
                        : describe(status, stalePlayerCount, backfillRunning);
                });
            });
        }

        private static SettingsNote.Data note(string text, SettingsNote.Type type)
            => new SettingsNote.Data($"{EzSettingsStrings.SKILL_DATA_STATUS} · {text}", type);

        private static SettingsNote.Data describe(EzSkillDataStatus status, int stalePlayerCount, bool backfillRunning)
        {
            string describeFacet(string name, EzFacetStatus facet)
                => $"{name} {EzSettingsStrings.SKILL_DATA_STATUS_READY} {facet.Ready}"
                   + $" {EzSettingsStrings.SKILL_DATA_STATUS_UNRATEABLE} {facet.Unrateable}"
                   + $" {EzSettingsStrings.SKILL_DATA_STATUS_STALE} {facet.Stale}"
                   + $" {EzSettingsStrings.SKILL_DATA_STATUS_MISSING} {facet.Missing}";

            string pending = status.HasWorkToDo
                ? $"    {EzSettingsStrings.SKILL_DATA_STATUS_PENDING} {status.TotalPending}"
                : EzSettingsStrings.SKILL_DATA_STATUS_ALL_CURRENT;

            // Player-side chain: it has no revision to count against — a play settling after the last skills pass
            // flags the player's rows instead, and the manual compute / chart-chain follow-up refreshes them.
            string players = stalePlayerCount > 0
                ? $"{EzSettingsStrings.SKILL_DATA_STATUS_PLAYERS} {EzSettingsStrings.SKILL_DATA_STATUS_STALE} {stalePlayerCount}"
                : $"{EzSettingsStrings.SKILL_DATA_STATUS_PLAYERS} {EzSettingsStrings.SKILL_DATA_STATUS_PLAYERS_ALL_CURRENT}";

            string line = $"{EzSettingsStrings.SKILL_DATA_STATUS_CHARTS} {status.TotalCharts}"
                          + $"\n · {describeFacet("MSD", status.Msd)}"
                          + $"\n · {describeFacet("CSI", status.ChartSkillInfo)}"
                          + $"\n · {describeFacet("Dan", status.ChartDan)}"
                          + $"\n · {pending}"
                          + $"\n · {players}";

            if (backfillRunning)
                line += EzSettingsStrings.SKILL_DATA_STATUS_RUNNING;

            bool healthy = !status.HasWorkToDo && stalePlayerCount == 0;

            return new SettingsNote.Data($"{EzSettingsStrings.SKILL_DATA_STATUS} · {line}",
                healthy ? SettingsNote.Type.Informational : SettingsNote.Type.Warning);
        }
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
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
using osu.Game.EzOsuGame.LocalProfile;
using osu.Game.EzOsuGame.Skills;
using osu.Game.EzOsuGame.Skills.Dan;
using osu.Game.EzOsuGame.Startup;
using osu.Game.Overlays;
using osu.Game.Overlays.Settings;

namespace osu.Game.EzOsuGame.Overlays
{
    /// <summary>
    /// Readout for the persisted analysis data of every ruleset. The sweep walks the chart chain and the value
    /// tables, so measurement stays on a background task and runs only when the user taps refresh in settings.
    /// Reports through a <see cref="SettingsNote"/>: informational when everything is current, warning when a pass
    /// would still have work to do, critical when the measurement itself failed.
    /// </summary>
    public partial class EzSkillDataStatusNote : CompositeDrawable
    {
        private readonly EzChartSkillStore? skillStore;
        private readonly EzPlayerSkillStore? playerStore;
        private readonly EzLocalProfileStore? profileStore;
        private readonly BackgroundDataStoreProcessor? backgroundDataStoreProcessor;

        private readonly Bindable<SettingsNote.Data?> statusNote = new Bindable<SettingsNote.Data?>();

        /// <summary>
        /// Whether a measurement is currently in flight, so a refresh button can disable itself.
        /// </summary>
        public readonly BindableBool Measuring = new BindableBool();

        private int measuring;

        /// <param name="profileStore">
        /// The SQLite archive, read to name the players whose plays the chart chain has not rated yet. Without it the
        /// note still reports the player rows flagged behind the ledger, just not what they are waiting on.
        /// </param>
        public EzSkillDataStatusNote(EzChartSkillStore? skillStore, EzPlayerSkillStore? playerStore, BackgroundDataStoreProcessor? backgroundDataStoreProcessor,
                                     EzLocalProfileStore? profileStore = null)
        {
            this.skillStore = skillStore;
            this.playerStore = playerStore;
            this.backgroundDataStoreProcessor = backgroundDataStoreProcessor;
            this.profileStore = profileStore;

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

        // Auto-measure on scroll-into-view was removed: use SKILL_DATA_STATUS_REFRESH to avoid stacking Realm scans on settings PopIn.

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
                var stopwatch = Stopwatch.StartNew();
                EzStartupTrace.LogDiagnostic($"SkillDataStatus.Measure begin thread={Environment.CurrentManagedThreadId}");

                bool failed = false;
                IReadOnlyList<EzRulesetAnalysisStatus> statuses = Array.Empty<EzRulesetAnalysisStatus>();
                IReadOnlyList<EzStalePlayerSkill> stalePlayers = Array.Empty<EzStalePlayerSkill>();
                EzChartChainDebt debt = EzChartChainDebt.EMPTY;
                bool backfillRunning = false;

                try
                {
                    statuses = skillStore.CollectAnalysisStatus("settings");
                    stalePlayers = playerStore?.GetStalePlayerSkillDetails() ?? Array.Empty<EzStalePlayerSkill>();

                    // Derived, not stored: the debt is the drill ledger joined against the chain's own coverage, so it
                    // disappears on its own once the chain writes the row instead of needing a flag cleared.
                    if (profileStore != null)
                        debt = EzChartChainDebt.Collect(profileStore.LoadManiaDrillChartPlays(), skillStore.CollectChartChainState("debt"));

                    backfillRunning = backgroundDataStoreProcessor?.IsEzRealmMetadataBackfillRunning == true;
                }
                catch (Exception e)
                {
                    Logger.Log($"[EzSkills] Skill data status measurement failed: {e}",
                        Ez2ConfigManager.LOGGER_NAME, LogLevel.Error);
                    failed = true;
                }

                EzStartupTrace.LogDiagnostic($"SkillDataStatus.Measure worker done {stopwatch.ElapsedMilliseconds}ms failed={failed}");

                // Only the Realm reads stay off-thread; the note text is composed back on the update thread
                // so its localisable labels resolve against the UI culture.
                Schedule(() =>
                {
                    Measuring.Value = false;
                    Interlocked.Exchange(ref measuring, 0);

                    statusNote.Value = failed || statuses.Count == 0
                        ? note(EzSettingsStrings.SKILL_DATA_STATUS_FAILED, SettingsNote.Type.Critical)
                        : describe(statuses, stalePlayers, debt, backfillRunning);
                });
            });
        }

        private static SettingsNote.Data note(string text, SettingsNote.Type type)
            => new SettingsNote.Data($"{EzSettingsStrings.SKILL_DATA_STATUS} · {text}", type);

        private static SettingsNote.Data describe(
            IReadOnlyList<EzRulesetAnalysisStatus> statuses,
            IReadOnlyList<EzStalePlayerSkill> stalePlayers,
            EzChartChainDebt debt,
            bool backfillRunning)
        {
            string describeFacet(EzFacetStatus facet)
                => $"{facet.Id} {EzSettingsStrings.SKILL_DATA_STATUS_READY} {facet.Ready}"
                   + $" {EzSettingsStrings.SKILL_DATA_STATUS_UNRATEABLE} {facet.Settled}"
                   + $" {EzSettingsStrings.SKILL_DATA_STATUS_STALE} {facet.Stale}"
                   + $" {EzSettingsStrings.SKILL_DATA_STATUS_MISSING} {facet.Missing}";

            bool hasWork = statuses.Any(s => s.Report.HasWorkToDo);
            int totalPending = statuses.Sum(s => s.Report.TotalPending);

            string pending = hasWork
                ? $"{EzSettingsStrings.SKILL_DATA_STATUS_PENDING} {totalPending}"
                : EzSettingsStrings.SKILL_DATA_STATUS_ALL_CURRENT;

            // One section per ruleset: each ruleset reads out under its own systems and revisions, so a single
            // library-wide line would mix counts that are not comparable. Only facets with work are listed; a fully
            // current facet is the expected state and would otherwise turn the note into a table of zeroes.
            string charts = string.Concat(statuses.Select(s =>
            {
                string header = $"\n · {s.RulesetName} {s.ChartCount}";

                var pendingFacets = s.Report.Facets.Where(f => f.Pending > 0).ToList();

                if (pendingFacets.Count == 0)
                    return $"{header} · {EzSettingsStrings.SKILL_DATA_STATUS_ALL_CURRENT}";

                return header + string.Concat(pendingFacets.Select(f => $"\n    · {describeFacet(f)}"));
            }));

            // Player-side chain: it has no revision to count against, so it reports two separate things — the rows
            // flagged as trailing the SQLite slice, and (derived) the plays still waiting on the chart chain. Each
            // names its own consumers, and both stop being reported on their own: the flag once a pass covers that
            // player, the debt once the chain writes the row it is waiting for.
            string players = stalePlayers.Count > 0
                ? $"{EzSettingsStrings.SKILL_DATA_STATUS_PLAYERS} {EzSettingsStrings.SKILL_DATA_STATUS_BEHIND} {stalePlayers.Count}"
                : $"{EzSettingsStrings.SKILL_DATA_STATUS_PLAYERS} {EzSettingsStrings.SKILL_DATA_STATUS_PLAYERS_ALL_CURRENT}";

            if (stalePlayers.Count > 0)
            {
                players += "\n    · " + string.Join(
                    "\n    · ",
                    stalePlayers.Select(p => $"{p.Username} {EzSettingsStrings.SKILL_DATA_STATUS_WRITTEN_AT} "
                                             + p.ComputedAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)));
            }

            string waiting = debt.HasDebt
                ? $"{EzSettingsStrings.SKILL_DATA_STATUS_WAITING_CHART} {debt.TotalPlays} · {debt.Usernames.Count} "
                  + $"({EzSettingsStrings.SKILL_DATA_STATUS_WAITING_MSD} {debt.WaitingOnMsdByUser.Values.Sum()})"
                : $"{EzSettingsStrings.SKILL_DATA_STATUS_WAITING_CHART} {EzSettingsStrings.SKILL_DATA_STATUS_ALL_CURRENT}";

            string line = $"{EzSettingsStrings.SKILL_DATA_STATUS_CHARTS} {statuses.Sum(s => s.ChartCount)}"
                          + charts
                          + $"\n · {pending}"
                          + $"\n · {players}"
                          + $"\n · {waiting}";

            if (backfillRunning)
                line += EzSettingsStrings.SKILL_DATA_STATUS_RUNNING;

            bool healthy = !hasWork && stalePlayers.Count == 0 && !debt.HasDebt;

            return new SettingsNote.Data($"{EzSettingsStrings.SKILL_DATA_STATUS} · {line}",
                healthy ? SettingsNote.Type.Informational : SettingsNote.Type.Warning);
        }
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Threading;
using osu.Game.Database;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets;

namespace osu.Game.Beatmaps.ExternalLibraries
{
    public partial class ExternalBeatmapLibraryIndexer : Component
    {
        private static readonly string[] standard_ruleset_short_names = { "osu", "mania", "taiko", "catch" };

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private Storage storage { get; set; } = null!;

        [Resolved]
        private IRulesetStore rulesets { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private INotificationOverlay? notifications { get; set; }

        private ExternalLibrarySettingsStore settingsStore = null!;
        private ScheduledDelegate? scheduledScan;

        [BackgroundDependencyLoader]
        private void load()
        {
            settingsStore = new ExternalLibrarySettingsStore(storage);
            ExternalLibrarySettingsStore.Instance = settingsStore;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            var settings = settingsStore.Load();

            if (!settings.PendingRescan && !settings.HasAnyConfiguredPath())
                return;

            scheduledScan = Scheduler.AddDelayed(runScan, 2000);
        }

        private void runScan()
        {
            scheduledScan = null;

            var settings = settingsStore.Load();
            settings.PendingRescan = false;

            var progress = new ProgressNotification
            {
                Text = "正在扫描外部谱面库…",
                State = ProgressNotificationState.Active,
                Progress = 0,
            };

            notifications?.Post(progress);

            Task.Run(() =>
            {
                try
                {
                    scanAll(settings, progress);
                    settingsStore.Save(settings);
                    Schedule(() => progress.State = ProgressNotificationState.Completed);
                }
                catch (Exception e)
                {
                    Logger.Error(e, "External beatmap library scan failed");
                    Schedule(() =>
                    {
                        progress.Text = "扫描失败";
                        progress.State = ProgressNotificationState.Completed;
                    });
                }
            });
        }

        private void scanAll(ExternalLibrarySettings settings, ProgressNotification progress)
        {
            var writer = new ExternalBeatmapLibraryRealmWriter(realm, storage);
            var standardRulesets = rulesets.AvailableRulesets
                                            .OfType<RulesetInfo>()
                                            .Where(r => standard_ruleset_short_names.Contains(r.ShortName, StringComparer.Ordinal))
                                            .ToDictionary(r => r.ShortName, StringComparer.Ordinal);

            int totalSteps = 0;
            int completedSteps = 0;

            if (settings.Enabled)
                totalSteps += settings.Paths.Count(p => !string.IsNullOrWhiteSpace(p));

            totalSteps += settings.Rulesets.Count(kvp => kvp.Value.Enabled && kvp.Value.Paths.Any(p => !string.IsNullOrWhiteSpace(p)));

            void reportProgress()
            {
                if (totalSteps == 0)
                {
                    Schedule(() => progress.Progress = 1);
                    return;
                }

                completedSteps++;
                Schedule(() => progress.Progress = (float)completedSteps / totalSteps);
            }

            if (settings.Enabled && standardRulesets.Count > 0)
            {
                var setsByRuleset = new Dictionary<string, List<ExternalBeatmapSetImportModel>>(StringComparer.Ordinal);

                foreach (string path in settings.Paths)
                {
                    if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                        continue;

                    string fullPath = Path.GetFullPath(path);

                    foreach (var set in OsuFolderExternalLibraryScanner.Scan(fullPath, standardRulesets, CancellationToken.None))
                    {
                        string shortName = set.Ruleset.ShortName;

                        if (!setsByRuleset.TryGetValue(shortName, out var list))
                        {
                            list = new List<ExternalBeatmapSetImportModel>();
                            setsByRuleset[shortName] = list;
                        }

                        list.Add(set);
                    }

                    reportProgress();
                }

                foreach (var (shortName, sets) in setsByRuleset)
                    writer.UpsertSets(sets, shortName);
            }

            foreach (var (shortName, config) in settings.Rulesets.Where(kvp => kvp.Value.Enabled))
            {
                if (standard_ruleset_short_names.Contains(shortName, StringComparer.Ordinal))
                    continue;

                var rulesetInfo = rulesets.AvailableRulesets.FirstOrDefault(r => string.Equals(r.ShortName, shortName, StringComparison.OrdinalIgnoreCase));

                if (rulesetInfo == null)
                    continue;

                var instance = rulesetInfo.CreateInstance();

                if (instance is not IRulesetExternalBeatmapLibraryContributor contributor)
                    continue;

                var allSets = new List<ExternalBeatmapSetImportModel>();

                foreach (string path in config.Paths)
                {
                    if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                        continue;

                    string fullPath = Path.GetFullPath(path);

                    foreach (var set in contributor.ScanPath(fullPath, CancellationToken.None))
                        allSets.Add(set);

                    reportProgress();
                }

                writer.UpsertSets(allSets, shortName);
            }

            Logger.Log("External beatmap library scan completed.", LoggingTarget.Database);
        }
    }
}

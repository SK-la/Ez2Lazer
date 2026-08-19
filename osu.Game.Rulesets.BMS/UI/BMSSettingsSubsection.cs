// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Game.Collections;
using osu.Game.Database;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.BMS.Beatmaps;
using osu.Game.Rulesets.BMS.Configuration;
using osu.Game.Rulesets.BMS.Localization;
using osu.Game.Rulesets.BMS.UI.BmsSongSelect;
using osu.Game.Rulesets.BMS.UI.BmsSongSelect.Analytics;
using osu.Game.Rulesets.BMS.UI.BmsSongSelect.Tables;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Configuration;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Screens;
using osu.Game.Screens.Menu;
using BmsUiSongSelect = osu.Game.Rulesets.BMS.UI.SongSelect;
using OsuSongSelect = osu.Game.Screens.Select.SongSelect;

namespace osu.Game.Rulesets.BMS.UI
{
    public partial class BMSSettingsSubsection : RulesetSettingsSubsection
    {

        private BMSRulesetConfigManager bmsConfig = null!;
        private Bindable<string> libraryPathsBindable = null!;
        private Bindable<string> legacyRootPathBindable = null!;

        // private OsuTextFlowContainer pathDisplay = null!;
        private SettingsNote cacheStatusNote = null!;
        private SettingsNote tablesHintNote = null!;
        private SettingsNote speedNote = null!;
        private Bindable<double>? maniaScrollSpeed;
        private Bindable<double>? maniaBaseSpeed;
        private Bindable<double>? maniaTimePerSpeed;

        [Resolved(canBeNull: true)]
        private OsuGame? game { get; set; }

        [Resolved(canBeNull: true)]
        private IPerformFromScreenRunner? performFromScreen { get; set; }

        [Resolved]
        private INotificationOverlay? notificationOverlay { get; set; }

        [Resolved]
        private Storage storage { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private EzAnalysisDatabase analysisDatabase { get; set; } = null!;

        [Resolved]
        private AudioManager audioManager { get; set; } = null!;

        [Resolved]
        private IRulesetConfigCache rulesetConfigCache { get; set; } = null!;

        private BMSBeatmapManager? beatmapManager;

        public BMSSettingsSubsection(Ruleset ruleset)
            : base(ruleset)
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            bmsConfig = (BMSRulesetConfigManager)Config;
            libraryPathsBindable = bmsConfig.GetBindable<string>(BMSRulesetSetting.BmsLibraryPaths);
            legacyRootPathBindable = bmsConfig.GetBindable<string>(BMSRulesetSetting.BmsRootPath);

            beatmapManager = BMSBeatmapManager.GetShared(storage);
            bmsConfig.ApplyResolvedLibraryPaths(beatmapManager);
            bindManiaScrollSettings();

            Children = new Drawable[]
            {
                new SettingsButtonV2
                {
                    Text = BmsStrings.SETTINGS_OPEN_CAROUSEL_SONG_SELECT,
                    Action = openStandardBmsSongSelect,
                },
                new SettingsButtonV2
                {
                    Text = BmsStrings.SETTINGS_OPEN_RAJA_SONG_SELECT,
                    Action = openRajaBmsSongSelect,
                },
                new SettingsButtonV2
                {
                    Text = BmsStrings.SETTINGS_OPEN_TABLES_FOLDER,
                    Action = openTablesFolder,
                },
                new SettingsButtonV2
                {
                    Text = BmsStrings.SETTINGS_SYNC_BUILTIN_TABLES,
                    Action = syncBuiltinTables,
                },
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = SettingsPanel.CONTENT_PADDING,
                    Child = tablesHintNote = new SettingsNote
                    {
                        RelativeSizeAxes = Axes.X,
                    },
                },
                new SettingsButtonV2
                {
                    Text = BmsStrings.SETTINGS_BUILD_ANALYTICS_DATABASE,
                    Action = buildAnalyticsDatabase,
                },
                new SettingsButtonV2
                {
                    Text = BmsStrings.SETTINGS_OPEN_PATH_WIZARD,
                    Action = selectPath,
                },
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = SettingsPanel.CONTENT_PADDING,
                    Child = cacheStatusNote = new SettingsNote
                    {
                        RelativeSizeAxes = Axes.X,
                    },
                },
                // new Container
                // {
                //     RelativeSizeAxes = Axes.X,
                //     Height = 110,
                //     Padding = new MarginPadding { Left = SettingsPanel.CONTENT_MARGINS, Right = SettingsPanel.CONTENT_MARGINS, Top = 6 },
                //     Child = new OsuScrollContainer
                //     {
                //         RelativeSizeAxes = Axes.Both,
                //         Child = pathDisplay = new OsuTextFlowContainer(cp => cp.Font = OsuFont.Default.With(size: 13))
                //         {
                //             RelativeSizeAxes = Axes.X,
                //             AutoSizeAxes = Axes.Y,
                //         }
                //     }
                // },
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = RulesetSettingsStrings.ScrollSpeed,
                    Current = maniaScrollSpeed ?? new BindableDouble(200),
                    KeyboardStep = 1,
                    LabelFormat = v =>
                    {
                        double baseSpeed = maniaBaseSpeed?.Value ?? 500;
                        double timePerSpeed = maniaTimePerSpeed?.Value ?? 5;
                        int computedTime = (int)DrawableManiaRuleset.ComputeScrollTime(v, baseSpeed, timePerSpeed);
                        return RulesetSettingsStrings.ScrollSpeedTooltip(computedTime, v).ToString();
                    }
                }),
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = SettingsPanel.CONTENT_PADDING,
                    Child = speedNote = new SettingsNote
                    {
                        RelativeSizeAxes = Axes.X,
                    },
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = BmsStrings.SETTINGS_AUTO_PRELOAD_KEYSOUNDS,
                    Current = bmsConfig.GetBindable<bool>(BMSRulesetSetting.AutoPreloadKeysounds),
                }),
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = BmsStrings.SETTINGS_KEY_SOUND_VOLUME,
                    Current = bmsConfig.GetBindable<double>(BMSRulesetSetting.KeysoundVolume),
                    KeyboardStep = 0.01f,
                    LabelFormat = v => $"{v:P0}",
                }),
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = BmsStrings.SETTINGS_DP_STAGE_SPACING,
                    HintText = BmsStrings.SETTINGS_DP_STAGE_SPACING_HINT,
                    Current = bmsConfig.GetBindable<double>(BMSRulesetSetting.DpStageSpacing),
                    KeyboardStep = 1,
                    LabelFormat = v => $"{v:0}",
                }),
                new SettingsItemV2(new FormEnumDropdown<BMSGameplayRoute>
                {
                    Caption = BmsStrings.SETTINGS_GAMEPLAY_ROUTE,
                    HintText = BmsStrings.SETTINGS_GAMEPLAY_ROUTE_HINT,
                    Current = bmsConfig.GetBindable<BMSGameplayRoute>(BMSRulesetSetting.GameplayRoute),
                }),
            };
        }

        private void bindManiaScrollSettings()
        {
            var maniaConfig = rulesetConfigCache.GetConfigFor(new ManiaRuleset()) as ManiaRulesetConfigManager;

            if (maniaConfig == null)
                return;

            maniaScrollSpeed = maniaConfig.GetBindable<double>(ManiaRulesetSetting.ScrollSpeed);
            maniaBaseSpeed = maniaConfig.GetBindable<double>(ManiaRulesetSetting.ScrollBaseSpeed);
            maniaTimePerSpeed = maniaConfig.GetBindable<double>(ManiaRulesetSetting.ScrollTimePerSpeed);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            libraryPathsBindable.BindValueChanged(_ => updatePathDisplay(), true);
            legacyRootPathBindable.BindValueChanged(_ => updatePathDisplay());

            speedNote.Current.Value = new SettingsNote.Data(BmsStrings.SETTINGS_MANIA_SCROLL_NOTE, SettingsNote.Type.Informational);
            tablesHintNote.Current.Value = new SettingsNote.Data(BmsStrings.SETTINGS_TABLES_HINT, SettingsNote.Type.Informational);

            // Show initial cache status
            if (beatmapManager != null)
            {
                cacheStatusNote.Current.Value = new SettingsNote.Data(
                    BmsStrings.Settings_CachedStatus(beatmapManager.SongCount, beatmapManager.ChartCount),
                    SettingsNote.Type.Informational);
            }
        }

        private IReadOnlyList<string> getConfiguredPaths() => bmsConfig.GetLibraryPaths();

        private void updatePathDisplay()
        {
            // IReadOnlyList<string> paths = getConfiguredPaths();
            //
            // if (paths.Count == 0)
            //     pathDisplay.Text = "未设置路径";
            // else
            //     pathDisplay.Text = $"当前路径 ({paths.Count}):{Environment.NewLine}{string.Join(Environment.NewLine, paths.Select(path => $"- {path}"))}";
        }

        private void openStandardBmsSongSelect() => openSongSelectScreen(new BmsUiSongSelect.BmsSoloSongSelect());

        private void openRajaBmsSongSelect() => openSongSelectScreen(new BmsBmsSongSelect());

        private void openTablesFolder()
        {
            try
            {
                BmsStoragePaths.EnsureInitialized(storage);
                string path = BmsStoragePaths.GetTablesDirectoryPath(storage);
                Directory.CreateDirectory(path);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true,
                });
            }
            catch (Exception ex)
            {
                notificationOverlay?.Post(new SimpleErrorNotification { Text = ex.Message });
            }
        }

        private void syncBuiltinTables()
        {
            notificationOverlay?.Post(new SimpleNotification { Text = BmsStrings.SONG_SELECT_TABLE_SYNCING });

            Task.Run(async () =>
            {
                try
                {
                    var store = new BmsDifficultyTableStore(storage);
                    var result = await store.SyncBuiltinCatalogAsync().ConfigureAwait(false);
                    Schedule(() => notificationOverlay?.Post(new SimpleNotification
                    {
                        Text = BmsStrings.SongSelect_TableSyncComplete(result.Succeeded, result.Failed, result.Skipped),
                    }));
                }
                catch (Exception ex)
                {
                    Schedule(() => notificationOverlay?.Post(new SimpleErrorNotification { Text = ex.Message }));
                }
            });
        }

        private void openSongSelectScreen(IScreen screen)
        {
            var runner = performFromScreen ?? game;

            if (runner == null)
            {
                notificationOverlay?.Post(new SimpleErrorNotification { Text = BmsStrings.SETTINGS_CANNOT_OPEN_SONG_SELECT });
                return;
            }

            runner.PerformFromScreen(s => s.Push(screen), new[] { typeof(MainMenu), typeof(OsuSongSelect) });
        }

        private void buildAnalyticsDatabase()
        {
            if (beatmapManager == null)
                return;

            var analyticsRepository = new BmsAnalyticsSqliteRepository(BmsStoragePaths.GetAnalyticsDatabasePath(storage));

            BmsUiSongSelect.BmsSongSelectAnalyticsOperations.RunAnalyticsBuild(
                Scheduler,
                beatmapManager,
                analyticsRepository,
                audioManager,
                realm,
                notificationOverlay,
                analysisDatabase);
        }

        private void selectPath()
        {
            var runner = performFromScreen ?? game;

            if (runner == null)
            {
                notificationOverlay?.Post(new SimpleErrorNotification { Text = BmsStrings.SETTINGS_CANNOT_OPEN_WIZARD });
                return;
            }

            runner.PerformFromScreen(screen =>
            {
                screen.Push(new BMSDirectorySelectScreen(bmsConfig, applyPathsAndScan));
            }, new[] { typeof(MainMenu), typeof(OsuSongSelect) });
        }

        private void applyPathsAndScan(IReadOnlyList<string> paths)
        {
            bmsConfig.PersistLibraryPaths(paths);
            startScan(paths);
        }

        private void startScan(IReadOnlyList<string>? configuredPaths = null)
        {
            if (beatmapManager == null) return;

            IReadOnlyList<string> paths = configuredPaths ?? getConfiguredPaths();
            beatmapManager.SetRootPaths(paths);

            // Empty list clears the library. Non-empty lists still require at least one existing folder.
            if (paths.Count > 0 && !paths.Any(Directory.Exists))
            {
                notificationOverlay?.Post(new SimpleErrorNotification
                {
                    Text = BmsStrings.SETTINGS_ADD_VALID_PATH_FIRST,
                });
                return;
            }

            bool clearing = paths.Count == 0;

            if (IsLoaded)
            {
                cacheStatusNote.Current.Value = new SettingsNote.Data(
                    clearing ? BmsStrings.SETTINGS_CLEARING_LIBRARY : BmsStrings.SETTINGS_SCANNING_NOTE,
                    SettingsNote.Type.Informational);
            }

            var notification = new ProgressNotification
            {
                Text = clearing ? BmsStrings.SETTINGS_CLEARING_LIBRARY : BmsStrings.SETTINGS_SCANNING_LIBRARY,
                CompletionText = clearing ? BmsStrings.SETTINGS_LIBRARY_CLEARED : BmsStrings.SETTINGS_SCAN_COMPLETE,
                State = ProgressNotificationState.Active,
                Progress = 0 // 初始化进度为 0,显示进度条而不是转圈
            };

            notificationOverlay?.Post(notification);

            BmsLibraryOperationGate.Shared.CancelCurrent();

            // Update the notification directly: Apply runs from the path wizard after settings leave the
            // screen stack, so SettingsSubsection.Schedule may not flush until the overlay is reopened.
            void onScanProgress(ValueChangedEvent<double> e)
            {
                if (notification.State is ProgressNotificationState.Cancelled or ProgressNotificationState.Completed)
                    return;

                notification.Progress = (float)BmsLibraryImportPipeline.MapScanProgress(e.NewValue);
            }

            void onScanStatus(ValueChangedEvent<string> e)
            {
                if (notification.State is ProgressNotificationState.Cancelled or ProgressNotificationState.Completed)
                    return;

                if (!string.IsNullOrEmpty(e.NewValue))
                    notification.Text = e.NewValue;
            }

            beatmapManager.ScanProgress.BindValueChanged(onScanProgress, true);
            beatmapManager.StatusMessage.BindValueChanged(onScanStatus, true);

            Task.Run(async () =>
            {
                try
                {
                    var result = await BmsLibraryImportPipeline.RunAsync(
                        beatmapManager,
                        storage,
                        realm,
                        new BMSRuleset().RulesetInfo,
                        paths,
                        p =>
                        {
                            if (notification.State is ProgressNotificationState.Cancelled or ProgressNotificationState.Completed)
                                return;

                            notification.Progress = (float)p.Progress;
                            notification.Text = p.StatusMessage;
                        },
                        notification.CancellationToken).ConfigureAwait(false);

                    notification.Progress = 1f;
                    notification.State = ProgressNotificationState.Completed;

                    if (IsLoaded)
                    {
                        Schedule(() =>
                        {
                            cacheStatusNote.Current.Value = new SettingsNote.Data(
                                BmsStrings.Settings_ScanCompleteStatus(result.SongCount, result.ChartCount),
                                SettingsNote.Type.Informational);
                        });
                    }

                    if (!clearing)
                        syncCollectionsFromPaths(paths);
                }
                catch (Exception ex)
                {
                    notification.State = ProgressNotificationState.Cancelled;

                    if (IsLoaded)
                    {
                        Schedule(() =>
                        {
                            cacheStatusNote.Current.Value = new SettingsNote.Data(BmsStrings.Settings_ScanFailedStatus(ex.Message), SettingsNote.Type.Critical);
                        });
                    }
                }
                finally
                {
                    beatmapManager.ScanProgress.ValueChanged -= onScanProgress;
                    beatmapManager.StatusMessage.ValueChanged -= onScanStatus;
                }
            });
        }

        private void syncCollectionsFromPaths(IReadOnlyList<string> paths)
        {
            if (paths.Count == 0 || beatmapManager == null)
                return;

            int syncedCount = 0;
            int totalCharts = 0;
            // Merge hashes for paths that share a folder name within this apply.
            var hashesByCollectionName = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (string path in paths)
            {
                if (!Directory.Exists(path))
                    continue;

                var beatmapHashes = new List<string>();
                const int page_size = 256;

                for (int offset = 0; ; offset += page_size)
                {
                    IReadOnlyList<BMSChartCache> charts = beatmapManager.GetChartPage(offset, page_size);

                    foreach (BMSChartCache chart in charts)
                    {
                        if (!chart.FolderPath.StartsWith(path, StringComparison.OrdinalIgnoreCase))
                            continue;

                        string md5Hash = string.IsNullOrEmpty(chart.Md5Hash)
                            ? BmsPathKeys.ComputeChartPathKey(chart.FullPath)
                            : chart.Md5Hash;
                        beatmapHashes.Add(md5Hash);
                    }

                    if (charts.Count < page_size)
                        break;
                }

                if (beatmapHashes.Count == 0)
                    continue;

                string collectionName = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (string.IsNullOrEmpty(collectionName))
                    collectionName = Path.GetFileName(path);
                if (string.IsNullOrEmpty(collectionName))
                    collectionName = BmsStrings.SETTINGS_DEFAULT_COLLECTION_NAME.ToString();

                if (hashesByCollectionName.TryGetValue(collectionName, out List<string>? existingHashes))
                    existingHashes.AddRange(beatmapHashes);
                else
                    hashesByCollectionName[collectionName] = beatmapHashes;
            }

            foreach ((string collectionName, List<string> beatmapHashes) in hashesByCollectionName)
            {
                List<string> distinctHashes = beatmapHashes.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

                realm.Write(r =>
                {
                    BeatmapCollection? existing = r.All<BeatmapCollection>()
                        .FirstOrDefault(c => c.Name == collectionName);

                    if (existing != null)
                    {
                        existing.BeatmapMD5Hashes.Clear();

                        foreach (string hash in distinctHashes)
                            existing.BeatmapMD5Hashes.Add(hash);

                        existing.LastModified = DateTimeOffset.UtcNow;
                    }
                    else
                    {
                        r.Add(new BeatmapCollection(collectionName, distinctHashes));
                    }
                });

                syncedCount++;
                totalCharts += distinctHashes.Count;
            }

            if (syncedCount > 0)
            {
                notificationOverlay?.Post(new SimpleNotification
                {
                    Text = BmsStrings.Settings_CollectionsSynced(syncedCount, totalCharts),
                });
            }
        }
    }
}

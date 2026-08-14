// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Game.Database;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets.BMS.Beatmaps;
using osu.Game.Rulesets.BMS.Configuration;
using osu.Game.Rulesets.BMS.Localization;
using osu.Game.Rulesets.BMS.Scoring.Lamp;
using osu.Game.Rulesets.BMS.Scoring.Lamp.Persistence;
using osu.Game.Rulesets.BMS.UI.BmsSongSelect.Analytics;
using osu.Game.Rulesets.BMS.UI.BmsSongSelect.Bars;
using osu.Game.Rulesets.BMS.UI.BmsSongSelect.Filtering;
using osu.Game.Rulesets.BMS.UI.BmsSongSelect.Tables;
using osu.Game.Rulesets.BMS.UI.SongSelect;
using osu.Game.Screens;
using osu.Game.Screens.Footer;
using osuTK;
using osuTK.Input;

namespace osu.Game.Rulesets.BMS.UI.BmsSongSelect
{
    /// <summary>
    /// Qwilight-inspired BMS song select with independent difficulty tables.
    /// Standard carousel remains <see cref="BmsSoloSongSelect"/>.
    /// </summary>
    public partial class BmsBmsSongSelect : OsuScreen
    {
        private BMSBeatmapManager beatmapManager = null!;
        private BmsSongSelectNavigator navigator = null!;
        private BmsBarContext barContext = null!;
        private BmsSongSelectShell shell = null!;
        private BmsChartPreviewPlayer previewPlayer = null!;
        private TextBox searchTextBox = null!;
        private RulesetInfo bmsRulesetInfo = null!;
        private BmsDifficultyTableStore tableStore = null!;

        private BMSRulesetConfigManager? bmsConfig;

        private BmsLampSqliteRepository? lampRepository;
        private BmsAnalyticsSqliteRepository? analyticsRepository;
        private BmsFilterDatabaseSync? filterSync;
        private CancellationTokenSource? screenWorkCts;

        [Cached(typeof(IBmsLampScheme))]
        private readonly IBmsLampScheme lampScheme = new BeatorajaLampScheme();

        [Cached]
        private readonly BmsLampStore lampStore;

        [Resolved]
        private AudioManager audioManager { get; set; } = null!;

        [Resolved]
        private IRenderer renderer { get; set; } = null!;

        [Resolved]
        private Storage storage { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private EzAnalysisDatabase analysisDatabase { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private INotificationOverlay? notifications { get; set; }

        [Resolved(canBeNull: true)]
        private MusicController? musicController { get; set; }

        [Resolved]
        private IRulesetConfigCache rulesetConfigCache { get; set; } = null!;

        [Resolved]
        private GameHost host { get; set; } = null!;

        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Blue);

        public BmsBmsSongSelect()
        {
            lampStore = new BmsLampStore(lampScheme);
        }

        public override bool ShowFooter => true;

        [BackgroundDependencyLoader]
        private void load()
        {
            var bmsRuleset = new BMSRuleset();
            bmsRulesetInfo = bmsRuleset.RulesetInfo;

            if (rulesetConfigCache.GetConfigFor(bmsRuleset) is BMSRulesetConfigManager config)
            {
                bmsConfig = config;
                bmsConfig.ApplyResolvedLibraryPaths(beatmapManager = BMSBeatmapManager.GetShared(storage));
            }
            else
            {
                beatmapManager = BMSBeatmapManager.GetShared(storage);
            }

            BmsStoragePaths.EnsureInitialized(storage);
            tableStore = new BmsDifficultyTableStore(storage);
            lampRepository = new BmsLampSqliteRepository(BmsStoragePaths.GetLampDatabasePath(storage));
            lampStore.AttachRepository(lampRepository);

            analyticsRepository = new BmsAnalyticsSqliteRepository(BmsStoragePaths.GetAnalyticsDatabasePath(storage));
            filterSync = new BmsFilterDatabaseSync(BmsStoragePaths.GetFilterDatabasePath(storage));

            rebuildBarContext();
            navigator = new BmsSongSelectNavigator(barContext, new BmsDifficultyTableRegistry(tableStore));
            navigator.Reset();

            InternalChild = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Padding = new MarginPadding { Bottom = ScreenFooter.HEIGHT },
                Children = new Drawable[]
                {
                    shell = new BmsSongSelectShell(navigator, barContext)
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                    new Container
                    {
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopRight,
                        Size = new Vector2(380, 40),
                        Margin = new MarginPadding { Top = 12, Right = 20 },
                        Masking = true,
                        Child = searchTextBox = new OsuTextBox
                        {
                            PlaceholderText = BmsStrings.RAJA_SEARCH_PLACEHOLDER,
                            RelativeSizeAxes = Axes.X,
                            Width = 1,
                        },
                    },
                    previewPlayer = new BmsChartPreviewPlayer
                    {
                        EnabledBindable = { Value = true },
                    },
                },
            };

            shell.RequestPlay += tryStartSelectedChart;
            shell.RequestOpenDownload += openSelectedChartDownload;
            navigator.Changed += onSelectionChanged;
            searchTextBox.Current.ValueChanged += e => navigator.SetListFilter(e.NewValue);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (beatmapManager.NeedsRealmSynchronization && beatmapManager.HasIndexedCharts)
            {
                try
                {
                    BMSOsuLibrarySynchronizer.Synchronize(beatmapManager, storage, realm, bmsRulesetInfo);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "[BMS] Raja initial library sync failed");
                }
            }

            refreshFilterDatabase();
            tableStore.Invalidate();
            navigator.Reset();
        }

        public override void OnEntering(ScreenTransitionEvent e)
        {
            base.OnEntering(e);
            stopGlobalMusic();
        }

        public override void OnResuming(ScreenTransitionEvent e)
        {
            base.OnResuming(e);
            stopGlobalMusic();
            onSelectionChanged();
        }

        public override bool OnBackButton() => navigator.TryGoBack();

        public override void OnSuspending(ScreenTransitionEvent e)
        {
            cancelBackgroundWork();
            previewPlayer.StopPreview();
            base.OnSuspending(e);
        }

        public override bool OnExiting(ScreenExitEvent e)
        {
            cancelBackgroundWork();
            previewPlayer.StopPreview();
            return base.OnExiting(e);
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (e.Key == Key.F5 && e.ControlPressed)
            {
                refreshLibrary();
                return true;
            }

            if (e.Key == Key.Number1)
            {
                barContext.KeyModeFilter.CycleNext();
                navigator.Reset();
                return true;
            }

            if (e.Key == Key.Number2)
            {
                barContext.SortPolicy.CycleNext();
                navigator.Reset();
                return true;
            }

            if (e.Key == Key.Enter && searchTextBox.HasFocus)
            {
                // Global search when not inside a table level; otherwise list filter already applied.
                if (navigator.ActiveLevel == null)
                    navigator.AddSearch(searchTextBox.Text);
                searchTextBox.Text = string.Empty;
                return true;
            }

            if (e.Key == Key.Enter && !searchTextBox.HasFocus)
            {
                // Shell handles Enter for navigation/play when focused.
            }

            return base.OnKeyDown(e);
        }

        public override IReadOnlyList<ScreenFooterButton> CreateFooterButtons()
        {
            return new[]
            {
                new ScreenFooterButton
                {
                    Text = BmsStrings.SONG_SELECT_BACK,
                    Action = () =>
                    {
                        if (!navigator.TryGoBack())
                            this.Exit();
                    },
                },
                new ScreenFooterButton { Text = BmsStrings.SONG_SELECT_REFRESH_LIBRARY, Action = refreshLibrary },
                new ScreenFooterButton { Text = BmsStrings.SONG_SELECT_BUILD_ANALYTICS_SHORT, Action = buildAnalytics },
                new ScreenFooterButton { Text = BmsStrings.SONG_SELECT_SYNC_BUILTIN_TABLES, Action = syncBuiltinTables },
                new ScreenFooterButton { Text = BmsStrings.SONG_SELECT_ADD_TABLE_URL, Action = promptAddTableUrl },
                new ScreenFooterButton { Text = BmsStrings.SONG_SELECT_OPEN_CHART_DOWNLOAD, Action = openSelectedChartDownload },
                new ScreenFooterButton { Text = BmsStrings.SONG_SELECT_OPEN_TABLES_FOLDER, Action = openTablesFolder },
            };
        }

        private void onSelectionChanged()
        {
            previewPlayer.StopPreview();

            if (navigator.GetSelectedSong() is not BmsSongBar song)
                return;

            try
            {
                if (!beatmapManager.TryGetChart(song.BeatmapId, out BMSChartCache chart))
                    return;

                var working = new BMSWorkingBeatmap(chart.FullPath, audioManager, renderer, chart);
                int previewTime = chart.PreviewTime;
                previewPlayer.OverridePreviewStartTime = previewTime >= 0 ? previewTime : 0;
                previewPlayer.StartPreview(working);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[BMS] Raja preview failed");
            }
        }

        private void tryStartSelectedChart()
        {
            var song = navigator.GetSelectedSong();

            if (song == null)
            {
                notifications?.Post(new SimpleNotification { Text = BmsStrings.SONG_SELECT_SELECT_CHART_TO_PLAY });
                return;
            }

            previewPlayer.StopPreview();
            cancelBackgroundWork();

            if (beatmapManager.TryGetChart(song.BeatmapId, out BMSChartCache chart))
                BmsSongSelectPlayHelper.TryLaunchFromChart(this, chart.FullPath, chart, null, audioManager, renderer, musicController, notifications);
        }

        private void refreshLibrary()
        {
            syncConfiguredPaths();
            screenWorkCts?.Cancel();
            screenWorkCts?.Dispose();
            screenWorkCts = new CancellationTokenSource();

            BmsSongSelectLibraryOperations.RunLibraryRefresh(
                Scheduler,
                beatmapManager,
                storage,
                realm,
                bmsRulesetInfo,
                notifications,
                operationId =>
                {
                    if (!this.IsCurrentScreen())
                        return;

                    navigator.Changed -= onSelectionChanged;
                    rebuildBarContext();
                    tableStore.Invalidate();
                    navigator = new BmsSongSelectNavigator(barContext, new BmsDifficultyTableRegistry(tableStore));
                    navigator.Changed += onSelectionChanged;
                    shell.Rebind(navigator, barContext);
                    refreshFilterDatabase();
                    navigator.Reset();
                },
                screenWorkCts.Token);
        }

        private void buildAnalytics()
        {
            if (analyticsRepository == null)
                return;

            screenWorkCts?.Cancel();
            screenWorkCts?.Dispose();
            screenWorkCts = new CancellationTokenSource();

            BmsSongSelectAnalyticsOperations.RunAnalyticsBuild(
                Scheduler,
                beatmapManager,
                analyticsRepository,
                audioManager,
                realm,
                notifications,
                analysisDatabase,
                onComplete: () => Schedule(() =>
                {
                    if (this.IsCurrentScreen())
                        navigator.Reset();
                }),
                cancellationToken: screenWorkCts.Token);
        }

        private void promptAddTableUrl()
        {
            // Lightweight prompt via notification + clipboard-less popup text box is heavy;
            // use a dedicated overlay popup dialog with text input when available.
            // Fallback: read from search box if it looks like a URL.
            string candidate = searchTextBox.Text.Trim();

            if (!candidate.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                notifications?.Post(new SimpleNotification
                {
                    Text = BmsStrings.SONG_SELECT_TABLE_URL_HINT,
                });
                return;
            }

            importTableUrl(candidate);
            searchTextBox.Text = string.Empty;
        }

        private void syncBuiltinTables()
        {
            screenWorkCts ??= new CancellationTokenSource();
            CancellationToken token = screenWorkCts.Token;
            notifications?.Post(new SimpleNotification { Text = BmsStrings.SONG_SELECT_TABLE_SYNCING });

            Task.Run(async () =>
            {
                BmsBuiltinTableSyncResult result = await tableStore.SyncBuiltinCatalogAsync(force: false, token).ConfigureAwait(false);

                Schedule(() =>
                {
                    if (token.IsCancellationRequested || !this.IsCurrentScreen())
                        return;

                    notifications?.Post(new SimpleNotification
                    {
                        Text = BmsStrings.SongSelect_TableSyncComplete(result.Succeeded, result.Failed, result.Skipped),
                    });
                    tableStore.Invalidate();
                    navigator.Reset();
                });
            }, token);
        }

        private void openSelectedChartDownload()
        {
            var missing = navigator.GetSelectedMissingChart();

            if (missing == null || !missing.Entry.HasDownloadUrl)
            {
                notifications?.Post(new SimpleNotification { Text = BmsStrings.SONG_SELECT_NO_DOWNLOAD_URL });
                return;
            }

            try
            {
                host.OpenUrlExternally(missing.Entry.PreferredDownloadUrl);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[BMS] Open chart download URL failed");
            }
        }

        private void importTableUrl(string url)
        {
            screenWorkCts ??= new CancellationTokenSource();
            CancellationToken token = screenWorkCts.Token;
            notifications?.Post(new SimpleNotification { Text = BmsStrings.SONG_SELECT_TABLE_IMPORTING });

            Task.Run(async () =>
            {
                BmsDifficultyTable? table = await tableStore.ImportFromUrlAsync(url, token).ConfigureAwait(false);

                Schedule(() =>
                {
                    if (token.IsCancellationRequested || !this.IsCurrentScreen())
                        return;

                    if (table == null)
                    {
                        notifications?.Post(new SimpleNotification { Text = BmsStrings.SONG_SELECT_TABLE_IMPORT_FAILED });
                        return;
                    }

                    notifications?.Post(new SimpleNotification { Text = BmsStrings.SongSelect_TableImported(table.Name) });
                    tableStore.Invalidate();
                    navigator.Reset();
                });
            }, token);
        }

        private void openTablesFolder()
        {
            try
            {
                string path = tableStore.TablesDirectory;
                Directory.CreateDirectory(path);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true,
                });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[BMS] Open tables folder failed");
            }
        }

        private void refreshFilterDatabase()
        {
            if (filterSync == null || lampRepository == null)
                return;

            screenWorkCts ??= new CancellationTokenSource();
            CancellationToken token = screenWorkCts.Token;

            Task.Run(() =>
            {
                try
                {
                    filterSync.ApplyPendingDelta(beatmapManager, lampRepository, realm, analyticsRepository, token);
                    Schedule(() =>
                    {
                        if (token.IsCancellationRequested || !this.IsCurrentScreen())
                            return;

                        navigator.Reset();
                    });
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "[BMS] filter database sync failed");
                }
            }, token);
        }

        private void cancelBackgroundWork()
        {
            screenWorkCts?.Cancel();
            beatmapManager.CancelScan();
            BmsLibraryOperationGate.Shared.CancelCurrent();
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                cancelBackgroundWork();
                screenWorkCts?.Dispose();
                screenWorkCts = null;

                if (navigator != null)
                    navigator.Changed -= onSelectionChanged;

                if (shell != null)
                {
                    shell.RequestPlay -= tryStartSelectedChart;
                    shell.RequestOpenDownload -= openSelectedChartDownload;
                }
            }

            base.Dispose(isDisposing);
        }

        private void rebuildBarContext()
        {
            syncConfiguredPaths();

            var folderTree = BmsFolderTree.Build(beatmapManager.RootPaths);
            string filterPath = BmsStoragePaths.GetFilterDatabasePath(storage);

            barContext = new BmsBarContext
            {
                BeatmapManager = beatmapManager,
                FolderTree = folderTree,
                SqlQuery = new BmsSqlSongQuery(filterPath, beatmapManager),
                FilterDatabasePath = filterPath,
                Analytics = analyticsRepository ?? new BmsAnalyticsSqliteRepository(BmsStoragePaths.GetAnalyticsDatabasePath(storage)),
                LampStore = lampStore,
                Realm = realm,
            };
        }

        private void syncConfiguredPaths() => bmsConfig?.ApplyResolvedLibraryPaths(beatmapManager);

        private void stopGlobalMusic()
        {
            musicController?.ResetTrackAdjustments();
            musicController?.Stop();
            if (musicController != null)
                musicController.AllowTrackControl.Value = false;
        }
    }
}

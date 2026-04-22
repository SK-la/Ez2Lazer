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
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets.BMS.Beatmaps;
using osu.Game.Rulesets.BMS.Configuration;
using osu.Game.Rulesets.BMS.Scoring.Lamp;
using osu.Game.Rulesets.BMS.Scoring.Lamp.Persistence;
using osu.Game.Rulesets.BMS.UI.BmsSongSelect.Analytics;
using osu.Game.Rulesets.BMS.UI.BmsSongSelect.Bars;
using osu.Game.Rulesets.BMS.UI.BmsSongSelect.Filtering;
using osu.Game.Rulesets.BMS.UI.SongSelect;
using osu.Game.Screens;
using osu.Game.Screens.Footer;
using osuTK;
using osuTK.Input;

namespace osu.Game.Rulesets.BMS.UI.BmsSongSelect
{
    /// <summary>
    /// beatoraja-style BMS song select (Bar navigation). Standard carousel remains <see cref="BmsSoloSongSelect"/>.
    /// </summary>
    public partial class BmsBmsSongSelect : OsuScreen
    {
        private BMSBeatmapManager beatmapManager = null!;
        private BmsBarManager barManager = null!;
        private BmsBarContext barContext = null!;
        private BmsChartPreviewPlayer previewPlayer = null!;
        private TextBox searchTextBox = null!;
        private RulesetInfo bmsRulesetInfo = null!;

        private Bindable<string> libraryPathsBindable = null!;
        private Bindable<string> legacyRootPathBindable = null!;

        private BmsLampSqliteRepository? lampRepository;
        private BmsAnalyticsSqliteRepository? analyticsRepository;
        private BmsFilterDatabaseSync? filterSync;

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

        [Resolved(canBeNull: true)]
        private INotificationOverlay? notifications { get; set; }

        [Resolved(canBeNull: true)]
        private MusicController? musicController { get; set; }

        [Resolved]
        private IRulesetConfigCache rulesetConfigCache { get; set; } = null!;

        public BmsBmsSongSelect()
        {
            lampStore = new BmsLampStore(lampScheme);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var bmsRuleset = new BMSRuleset();
            bmsRulesetInfo = bmsRuleset.RulesetInfo;

            if (rulesetConfigCache.GetConfigFor(bmsRuleset) is BMSRulesetConfigManager bmsConfig)
            {
                libraryPathsBindable = bmsConfig.GetBindable<string>(BMSRulesetSetting.BmsLibraryPaths);
                legacyRootPathBindable = bmsConfig.GetBindable<string>(BMSRulesetSetting.BmsRootPath);
            }
            else
            {
                libraryPathsBindable = new Bindable<string>(string.Empty);
                legacyRootPathBindable = new Bindable<string>(string.Empty);
            }

            beatmapManager = BMSBeatmapManager.GetShared(storage);
            syncConfiguredPaths();

            BmsStoragePaths.EnsureInitialized(storage);
            lampRepository = new BmsLampSqliteRepository(BmsStoragePaths.GetLampDatabasePath(storage));
            lampStore.AttachRepository(lampRepository);

            analyticsRepository = new BmsAnalyticsSqliteRepository(BmsStoragePaths.GetAnalyticsDatabasePath(storage));
            filterSync = new BmsFilterDatabaseSync(BmsStoragePaths.GetFilterDatabasePath(storage));

            rebuildBarContext();
            barManager = new BmsBarManager(barContext);
            barManager.ResetToRoot();

            InternalChild = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    new BmsBarRenderer(barManager, barContext)
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                    new Container
                    {
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopRight,
                        Size = new Vector2(380, 32),
                        Margin = new MarginPadding { Top = 78, Right = 20 },
                        Child = searchTextBox = new OsuTextBox
                        {
                            PlaceholderText = "搜索曲目 (Enter)",
                            RelativeSizeAxes = Axes.Both,
                        },
                    },
                    previewPlayer = new BmsChartPreviewPlayer
                    {
                        EnabledBindable = { Value = true },
                    },
                },
            };

            barManager.Changed += onBarSelectionChanged;
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
            barManager.ResetToRoot();
        }

        public override void OnEntering(ScreenTransitionEvent e)
        {
            base.OnEntering(e);
            musicController?.Stop();
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
                barManager.ResetToRoot();
                return true;
            }

            if (e.Key == Key.Number2)
            {
                barContext.SortPolicy.CycleNext();
                barManager.ResetToRoot();
                return true;
            }

            if (e.Key == Key.Number8)
            {
                barManager.ShowSameFolder();
                return true;
            }

            if (e.Key == Key.Enter && searchTextBox.HasFocus)
            {
                barManager.AddSearch(searchTextBox.Text);
                searchTextBox.Text = string.Empty;
                return true;
            }

            if (e.Key == Key.Enter)
            {
                tryStartSelectedChart();
                return true;
            }

            return base.OnKeyDown(e);
        }

        public override IReadOnlyList<ScreenFooterButton> CreateFooterButtons()
        {
            return new[]
            {
                new ScreenFooterButton { Text = "返回", Action = this.Exit },
                new ScreenFooterButton { Text = "刷新曲库", Action = refreshLibrary },
                new ScreenFooterButton { Text = "构建分析库", Action = buildAnalytics },
            };
        }

        private void onBarSelectionChanged()
        {
            previewPlayer.StopPreview();

            if (barManager.GetSelectedBar() is not BmsSongBar song)
                return;

            try
            {
                var working = new BMSWorkingBeatmap(song.Chart.FullPath, audioManager, renderer, song.Chart);
                int previewTime = song.Chart.PreviewTime;
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
            var song = barManager.GetSelectedSong();

            if (song == null)
            {
                notifications?.Post(new SimpleNotification { Text = "请选择一个 BMS 谱面" });
                return;
            }

            previewPlayer.StopPreview();
            BmsSongSelectPlayHelper.TryLaunchFromChart(this, song.Chart.FullPath, song.Chart, null, audioManager, renderer, musicController, notifications);
        }

        private void refreshLibrary()
        {
            syncConfiguredPaths();
            BmsSongSelectLibraryOperations.RunLibraryRefresh(
                Scheduler,
                beatmapManager,
                storage,
                realm,
                bmsRulesetInfo,
                notifications,
                () =>
                {
                    rebuildBarContext();
                    barManager = new BmsBarManager(barContext);
                    barManager.Changed += onBarSelectionChanged;
                    refreshFilterDatabase();
                    barManager.ResetToRoot();
                });
        }

        private void buildAnalytics()
        {
            if (analyticsRepository == null)
                return;

            BmsSongSelectAnalyticsOperations.RunAnalyticsBuild(
                Scheduler,
                beatmapManager,
                analyticsRepository,
                audioManager,
                realm,
                notifications,
                onComplete: () => Schedule(() => barManager.ResetToRoot()));
        }

        private void refreshFilterDatabase()
        {
            if (filterSync == null || lampRepository == null)
                return;

            Task.Run(() =>
            {
                try
                {
                    filterSync.Rebuild(beatmapManager, lampRepository, realm, analyticsRepository);
                    Schedule(() => barManager.ResetToRoot());
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "[BMS] filter database rebuild failed");
                }
            });
        }

        private void rebuildBarContext()
        {
            var roots = BMSRulesetConfigManager.ParseLibraryPaths(libraryPathsBindable.Value, legacyRootPathBindable.Value);
            beatmapManager.SetRootPaths(roots);

            var folderTree = BmsFolderTree.Build(roots, beatmapManager.LibraryCache?.Songs ?? Enumerable.Empty<BMSSongCache>());
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

        private void syncConfiguredPaths() => beatmapManager.SetRootPaths(BMSRulesetConfigManager.ParseLibraryPaths(libraryPathsBindable.Value, legacyRootPathBindable.Value));
    }
}

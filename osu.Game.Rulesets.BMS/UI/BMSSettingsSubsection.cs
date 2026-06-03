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
        protected override LocalisableString Header => "BMS";

        private BMSRulesetConfigManager bmsConfig = null!;
        private Bindable<string> libraryPathsBindable = null!;
        private Bindable<string> legacyRootPathBindable = null!;

        // private OsuTextFlowContainer pathDisplay = null!;
        private SettingsNote cacheStatusNote = null!;
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
            beatmapManager.SetRootPaths(getConfiguredPaths());
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

            // Show initial cache status
            if (beatmapManager?.LibraryCache != null)
            {
                cacheStatusNote.Current.Value = new SettingsNote.Data(
                    BmsStrings.Settings_CachedStatus(beatmapManager.LibraryCache.Songs.Count, beatmapManager.LibraryCache.TotalCharts),
                    SettingsNote.Type.Informational);
            }
        }

        private IReadOnlyList<string> getConfiguredPaths() => BMSRulesetConfigManager.ParseLibraryPaths(libraryPathsBindable.Value, legacyRootPathBindable.Value);

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
                screen.Push(new BMSDirectorySelectScreen(libraryPathsBindable, legacyRootPathBindable, applyPathsAndScan));
            }, new[] { typeof(MainMenu), typeof(OsuSongSelect) });
        }

        private void applyPathsAndScan(IReadOnlyList<string> paths)
        {
            libraryPathsBindable.Value = BMSRulesetConfigManager.SerialiseLibraryPaths(paths);
            legacyRootPathBindable.Value = paths.FirstOrDefault() ?? string.Empty;
            startScan(paths);
        }

        private void startScan(IReadOnlyList<string>? configuredPaths = null)
        {
            if (beatmapManager == null) return;

            IReadOnlyList<string> paths = configuredPaths ?? getConfiguredPaths();
            beatmapManager.SetRootPaths(paths);

            if (paths.Count == 0 || !paths.Any(Directory.Exists))
            {
                notificationOverlay?.Post(new SimpleErrorNotification
                {
                    Text = BmsStrings.SETTINGS_ADD_VALID_PATH_FIRST,
                });
                return;
            }

            cacheStatusNote.Current.Value = new SettingsNote.Data(BmsStrings.SETTINGS_SCANNING_NOTE, SettingsNote.Type.Informational);

            var notification = new ProgressNotification
            {
                Text = BmsStrings.SETTINGS_SCANNING_LIBRARY,
                CompletionText = BmsStrings.SETTINGS_SCAN_COMPLETE,
                State = ProgressNotificationState.Active,
                Progress = 0 // 初始化进度为 0,显示进度条而不是转圈
            };

            notificationOverlay?.Post(notification);

            void onScanProgress(ValueChangedEvent<double> e) => Schedule(() => notification.Progress = (float)BmsLibraryImportPipeline.MapScanProgress(e.NewValue));

            void onScanStatus(ValueChangedEvent<string> e) => Schedule(() => notification.Text = e.NewValue);

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
                        p => Schedule(() =>
                        {
                            notification.Progress = (float)p.Progress;
                            notification.Text = p.StatusMessage;
                        })).ConfigureAwait(false);

                    Schedule(() =>
                    {
                        notification.Progress = 1f;
                        notification.State = ProgressNotificationState.Completed;
                        cacheStatusNote.Current.Value = new SettingsNote.Data(
                            BmsStrings.Settings_ScanCompleteStatus(result.SongCount, result.ChartCount),
                            SettingsNote.Type.Informational);

                        // 扫描完成后，为每个路径创建独立的收藏夹
                        createCollectionsFromPaths(paths);
                    });
                }
                catch (Exception ex)
                {
                    Schedule(() =>
                    {
                        notification.State = ProgressNotificationState.Cancelled;
                        cacheStatusNote.Current.Value = new SettingsNote.Data(BmsStrings.Settings_ScanFailedStatus(ex.Message), SettingsNote.Type.Critical);
                    });
                }
                finally
                {
                    Schedule(() =>
                    {
                        beatmapManager.ScanProgress.ValueChanged -= onScanProgress;
                        beatmapManager.StatusMessage.ValueChanged -= onScanStatus;
                    });
                }
            });
        }

        private void createCollectionsFromPaths(IReadOnlyList<string> paths)
        {
            if (paths.Count == 0 || beatmapManager?.LibraryCache == null)
                return;

            int createdCount = 0;
            int totalCharts = 0;
            // 用于跟踪已使用的收藏夹名称，避免重复
            var usedCollectionNames = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (string path in paths)
            {
                if (!Directory.Exists(path))
                    continue;

                // 获取该路径下的所有谱面哈希值
                var beatmapHashes = new List<string>();

                // 从 LibraryCache 中查找属于该路径的谱面
                foreach (var song in beatmapManager.LibraryCache.Songs)
                {
                    // 检查歌曲是否在该路径下
                    if (song.FolderPath.StartsWith(path, StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (var chart in song.Charts)
                        {
                            string md5Hash = BmsPathKeys.ComputeChartPathKey(chart.FullPath);
                            beatmapHashes.Add(md5Hash);
                        }
                    }
                }

                if (beatmapHashes.Count == 0)
                    continue;

                // 使用路径的文件夹名称作为收藏夹名称
                string collectionName = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (string.IsNullOrEmpty(collectionName))
                    collectionName = Path.GetFileName(path); // 尝试再次获取
                if (string.IsNullOrEmpty(collectionName))
                    collectionName = BmsStrings.SETTINGS_DEFAULT_COLLECTION_NAME.ToString(); // 最后的默认值

                // 检查名称是否已使用，如果已使用则追加序号
                if (usedCollectionNames.TryGetValue(collectionName, out int value))
                {
                    // 增加该名称的计数器
                    usedCollectionNames[collectionName] = ++value;
                    collectionName = $"{collectionName} ({value})";
                }
                else
                {
                    // 首次使用该名称，计数器设为0
                    usedCollectionNames[collectionName] = 0;
                }

                // 创建收藏夹
                realm.Write(r =>
                {
                    var collection = new BeatmapCollection(collectionName, beatmapHashes);
                    r.Add(collection);
                });

                createdCount++;
                totalCharts += beatmapHashes.Count;
            }

            if (createdCount > 0)
            {
                notificationOverlay?.Post(new SimpleNotification
                {
                    Text = BmsStrings.Settings_CollectionsCreated(createdCount, totalCharts),
                });
            }
        }
    }
}

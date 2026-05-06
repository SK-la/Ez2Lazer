// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.EzOsuGame.Audio;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets.BMS.Beatmaps;
using osu.Game.Rulesets.BMS.Configuration;
using osu.Game.Screens;
using osu.Game.Tests.Visual;
using osuTK;

namespace osu.Game.Rulesets.BMS.UI.SongSelect
{
    public enum BMSSongSelectScreenMode
    {
        RulesetEntry,
        SpecialEntry,
    }

    /// <summary>
    /// V2-style song select screen for BMS ruleset.
    /// Loads songs from BMSBeatmapManager cache with improved layout.
    /// Left panel: Song info + difficulty selector + scores
    /// Right panel: Song list
    ///
    /// Usage: Access this screen through:
    /// 1. BMS Settings (in game settings) -> "Select BMS Root Path" button -> After setting path, use button to open
    /// 2. Direct code: game.PerformFromScreen(s => s.Push(new BMSSongSelectScreen()));
    /// </summary>
    public partial class BMSSongSelectScreen : OsuScreen
    {
        public override bool ShowFooter => false;

        protected override bool InitialBackButtonVisibility => true;

        private readonly BMSSongSelectScreenMode mode;

        private BMSBeatmapManager beatmapManager = null!;
        private OsuSpriteText statusText = null!;
        private FillFlowContainer<BMSSongCardV2> songList = null!;
        private LoadingSpinner loadingSpinner = null!;
        private SearchTextBox searchBox = null!;
        private EzPreviewTrackManager previewManager = null!;
        private BMSWorkingBeatmap? previewBeatmap;
        private Track? fallbackPreviewTrack;

        private readonly Bindable<BMSSongCache?> selectedSong = new Bindable<BMSSongCache?>();
        private readonly Bindable<BMSChartCache?> selectedChart = new Bindable<BMSChartCache?>();
        private readonly Dictionary<string, BeatmapInfo> virtualBeatmapByHash = new Dictionary<string, BeatmapInfo>(StringComparer.OrdinalIgnoreCase);
        private BeatmapInfo? selectedVirtualBeatmap;
        private Bindable<string> libraryPathsBindable = null!;
        private Bindable<string> legacyRootPathBindable = null!;

        [Resolved]
        private AudioManager audioManager { get; set; } = null!;

        [Resolved]
        private TextureStore textures { get; set; } = null!;

        [Resolved]
        private Storage storage { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private INotificationOverlay? notifications { get; set; }

        [Resolved]
        private IRulesetConfigCache rulesetConfigCache { get; set; } = null!;

        public BMSSongSelectScreen(BMSSongSelectScreenMode mode = BMSSongSelectScreenMode.RulesetEntry)
        {
            this.mode = mode;
        }

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));

            // Create and cache OverlayColourProvider for child components (required for BMSInfoPanel)
            dependencies.Cache(new OverlayColourProvider(OverlayColourScheme.Blue));

            return dependencies;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            // Get config for BMS ruleset
            var ruleset = new BMSRuleset();
            var config = rulesetConfigCache.GetConfigFor(ruleset);

            if (config is BMSRulesetConfigManager bmsConfig)
            {
                libraryPathsBindable = bmsConfig.GetBindable<string>(BMSRulesetSetting.BmsLibraryPaths);
                legacyRootPathBindable = bmsConfig.GetBindable<string>(BMSRulesetSetting.BmsRootPath);
            }
            else
            {
                libraryPathsBindable = new Bindable<string>(string.Empty);
                legacyRootPathBindable = new Bindable<string>(string.Empty);
            }

            string cacheDir = storage.GetFullPath("bms_cache");
            beatmapManager = BMSBeatmapManager.GetShared(cacheDir);
            syncConfiguredPaths();

            // Register OverlayColourProvider for child components (required for BMSInfoPanel)
            var colourProvider = new OverlayColourProvider(OverlayColourScheme.Blue);
            var childDependencies = new DependencyContainer(Dependencies);
            childDependencies.CacheAs(colourProvider);

            // V2风格布局 - 左侧信息面板，右侧歌曲列表
            InternalChildren = new Drawable[]
            {
                new DependencyProvidingContainer
                {
                    CachedDependencies = new[] { (typeof(OverlayColourProvider), (object)colourProvider) },
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = Colour4.Black.Opacity(0.9f),
                        },
                        new GridContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            ColumnDimensions = new[]
                            {
                                new Dimension(GridSizeMode.Absolute, 450), // 左侧信息面板固定宽度
                                new Dimension(), // 右侧列表占据剩余空间
                            },
                            Content = new[]
                            {
                                new Drawable[]
                                {
                                    // Left panel - 谱面信息、难度选择器和成绩
                                    new Container
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Padding = new MarginPadding { Left = 10, Top = 10, Bottom = 10, Right = 5 },
                                        Children = new Drawable[]
                                        {
                                            new BMSInfoPanel
                                            {
                                                RelativeSizeAxes = Axes.Both,
                                                SelectedSong = { BindTarget = selectedSong },
                                                SelectedChart = { BindTarget = selectedChart },
                                            },
                                            // 游戏按钮覆盖层
                                            new Container
                                            {
                                                Anchor = Anchor.BottomCentre,
                                                Origin = Anchor.BottomCentre,
                                                RelativeSizeAxes = Axes.X,
                                                AutoSizeAxes = Axes.Y,
                                                Padding = new MarginPadding { Bottom = 20 },
                                                Child = new FillFlowContainer
                                                {
                                                    Anchor = Anchor.Centre,
                                                    Origin = Anchor.Centre,
                                                    AutoSizeAxes = Axes.Both,
                                                    Direction = FillDirection.Horizontal,
                                                    Spacing = new Vector2(10, 0),
                                                    Children = new Drawable[]
                                                    {
                                                        new RoundedButton
                                                        {
                                                            Text = "开始游戏",
                                                            Width = 150,
                                                            Height = 50,
                                                            Action = startGame,
                                                        },
                                                        new RoundedButton
                                                        {
                                                            Text = "刷新曲库",
                                                            Width = 120,
                                                            Height = 50,
                                                            Action = refreshLibrary,
                                                        },
                                                    },
                                                },
                                            },
                                        },
                                    },
                                    // Right panel - 歌曲列表
                                    new Container
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Padding = new MarginPadding { Left = 5, Top = 10, Bottom = 10, Right = 10 },
                                        Children = new Drawable[]
                                        {
                                            new Box
                                            {
                                                RelativeSizeAxes = Axes.Both,
                                                Colour = colours.Gray2,
                                                Alpha = 0.5f,
                                            },
                                            new FillFlowContainer
                                            {
                                                RelativeSizeAxes = Axes.Both,
                                                Direction = FillDirection.Vertical,
                                                Padding = new MarginPadding(10),
                                                Spacing = new Vector2(0, 10),
                                                Children = new Drawable[]
                                                {
                                                    new FillFlowContainer
                                                    {
                                                        RelativeSizeAxes = Axes.X,
                                                        AutoSizeAxes = Axes.Y,
                                                        Direction = FillDirection.Vertical,
                                                        Spacing = new Vector2(0, 8),
                                                        Children = new Drawable[]
                                                        {
                                                            new OsuSpriteText
                                                            {
                                                                Text = mode == BMSSongSelectScreenMode.SpecialEntry ? "BMS 特殊选歌" : "BMS 选歌",
                                                                Font = OsuFont.GetFont(size: 24, weight: FontWeight.Bold),
                                                            },
                                                            searchBox = new SearchTextBox
                                                            {
                                                                RelativeSizeAxes = Axes.X,
                                                                Height = 40,
                                                                PlaceholderText = "搜索歌曲...",
                                                            },
                                                            statusText = new OsuSpriteText
                                                            {
                                                                Text = string.Empty,
                                                                Font = OsuFont.GetFont(size: 14),
                                                                Colour = colours.Yellow,
                                                            },
                                                        }
                                                    },
                                                    new OsuScrollContainer
                                                    {
                                                        RelativeSizeAxes = Axes.Both,
                                                        Child = songList = new FillFlowContainer<BMSSongCardV2>
                                                        {
                                                            RelativeSizeAxes = Axes.X,
                                                            AutoSizeAxes = Axes.Y,
                                                            Direction = FillDirection.Vertical,
                                                            Spacing = new Vector2(0, 5),
                                                        },
                                                    },
                                                },
                                            },
                                        },
                                    },
                                }
                            }
                        },
                        // Loading spinner overlay
                        loadingSpinner = new LoadingSpinner(true)
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            State = { Value = Visibility.Hidden },
                        },
                        previewManager = new EzPreviewTrackManager(),
                    }
                },
            };

            previewManager.EnabledBindable.Value = true;
            previewManager.OverridePreviewStartTime = 0;

            // Bind events
            searchBox.Current.BindValueChanged(e => filterSongs(e.NewValue));
            selectedChart.BindValueChanged(e =>
            {
                selectedVirtualBeatmap = e.NewValue != null && virtualBeatmapByHash.TryGetValue(e.NewValue.Md5Hash, out BeatmapInfo? beatmapInfo)
                    ? beatmapInfo
                    : null;
                updatePreviewTrack(e.NewValue);
            }, true);

            // Load songs
            refreshSongList();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (mode == BMSSongSelectScreenMode.RulesetEntry)
                Ruleset.BindValueChanged(onRulesetChanged);
        }

        private void onRulesetChanged(ValueChangedEvent<RulesetInfo> ruleset)
        {
            if (!this.IsCurrentScreen() || string.Equals(ruleset.NewValue.ShortName, "bms", StringComparison.OrdinalIgnoreCase))
                return;

            this.Exit();
        }

        private void syncConfiguredPaths()
            => beatmapManager.SetRootPaths(BMSRulesetConfigManager.ParseLibraryPaths(libraryPathsBindable.Value, legacyRootPathBindable.Value));

        private void updatePreviewTrack(BMSChartCache? chart)
        {
            previewManager.StopPreview();
            stopFallbackPreviewTrack();
            previewBeatmap = null;

            if (chart == null)
                return;

            try
            {
                previewBeatmap = new BMSWorkingBeatmap(chart.FullPath, audioManager, textures, chart);
                previewBeatmap.LoadTrack();
                previewManager.OverridePreviewStartTime = chart.PreviewTime >= 0 ? chart.PreviewTime : 0;
                bool enhancedStarted = previewManager.StartPreview(previewBeatmap, true);

                if (!enhancedStarted)
                    startFallbackPreviewTrack(previewBeatmap, chart);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to start BMS enhanced preview");
            }
        }

        private void startFallbackPreviewTrack(BMSWorkingBeatmap beatmap, BMSChartCache chart)
        {
            fallbackPreviewTrack = beatmap.Track;

            if (fallbackPreviewTrack == null)
                return;

            beatmap.PrepareTrackForPreview(true);

            double startTime = chart.PreviewTime >= 0 ? chart.PreviewTime : 0;
            fallbackPreviewTrack.Seek(startTime);
            fallbackPreviewTrack.RestartPoint = startTime;
            fallbackPreviewTrack.Looping = true;
            fallbackPreviewTrack.Start();
        }

        private void stopFallbackPreviewTrack()
        {
            if (fallbackPreviewTrack == null)
                return;

            fallbackPreviewTrack.Stop();
            fallbackPreviewTrack = null;
        }

        private void refreshSongList()
        {
            syncConfiguredPaths();
            songList.Clear();
            rebuildVirtualCatalog();
            BMSOsuLibrarySynchronizer.Synchronize(beatmapManager, storage, realm, new BMSRuleset().RulesetInfo);

            if (beatmapManager.LibraryCache == null || beatmapManager.LibraryCache.Songs.Count == 0)
            {
                statusText.Text = "曲库为空，请先扫描 BMS 文件夹";
                return;
            }

            foreach (var song in beatmapManager.LibraryCache.Songs.OrderBy(s => s.Title))
            {
                songList.Add(new BMSSongCardV2(song)
                {
                    Action = () => selectSong(song),
                });
            }

            statusText.Text = $"共 {beatmapManager.LibraryCache.Songs.Count} 首歌曲";
            filterSongs(searchBox.Current.Value);

            if (selectedSong.Value != null)
            {
                var existingSong = beatmapManager.LibraryCache.Songs.FirstOrDefault(song => string.Equals(song.FolderPath, selectedSong.Value.FolderPath, StringComparison.OrdinalIgnoreCase));

                if (existingSong != null)
                {
                    selectSong(existingSong);
                    return;
                }
            }

            if (beatmapManager.LibraryCache.Songs.Count > 0)
                selectSong(beatmapManager.LibraryCache.Songs.OrderBy(song => song.Title).First());
        }

        private void filterSongs(string filter)
        {
            foreach (var card in songList)
            {
                var song = card.Song;
                bool matches = string.IsNullOrEmpty(filter) ||
                               song.Title.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                               song.Artist.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                               song.Genre.Contains(filter, StringComparison.OrdinalIgnoreCase);
                card.Alpha = matches ? 1 : 0;
            }
        }

        private void selectSong(BMSSongCache song)
        {
            selectedSong.Value = song;

            // Auto-select first chart
            if (song.Charts.Count > 0)
            {
                selectedChart.Value = song.Charts.OrderBy(chart => chart.PlayLevel).ThenBy(chart => chart.FileName).First();
            }
            else
            {
                selectedChart.Value = null;
                selectedVirtualBeatmap = null;
            }

            // Update song card selection state
            foreach (var card in songList)
            {
                card.Selected = card.Song == song;
            }
        }

        private void refreshLibrary()
        {
            if (beatmapManager.RootPaths.Count == 0)
            {
                notifications?.Post(new SimpleNotification
                {
                    Text = "请先在设置中设置 BMS 路径",
                    Icon = FontAwesome.Solid.ExclamationTriangle,
                });
                return;
            }

            loadingSpinner.Show();
            statusText.Text = "正在扫描曲库...";

            var notification = new ProgressNotification
            {
                Text = "正在扫描 BMS 曲库...",
                Progress = 0,
            };

            notifications?.Post(notification);

            Action<ValueChangedEvent<double>> onProgress = e => Schedule(() => notification.Progress = (float)e.NewValue);
            Action<ValueChangedEvent<string>> onStatus = e => Schedule(() => notification.Text = e.NewValue);
            beatmapManager.ScanProgress.BindValueChanged(onProgress, true);
            beatmapManager.StatusMessage.BindValueChanged(onStatus, true);

            _ = System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    await beatmapManager.ScanLibraryAsync(beatmapManager.RootPaths).ConfigureAwait(false);

                    Schedule(() =>
                    {
                        try
                        {
                            BMSOsuLibrarySynchronizer.Synchronize(beatmapManager, storage, realm, new BMSRuleset().RulesetInfo);
                            notification.Progress = 1f;
                            notification.State = ProgressNotificationState.Completed;
                            refreshSongList();
                        }
                        catch (Exception ex)
                        {
                            Logger.Error(ex, "BMS synchronize failed after scan");
                            notification.State = ProgressNotificationState.Cancelled;
                            notifications?.Post(new SimpleNotification
                            {
                                Text = $"同步失败: {ex.Message}",
                                Icon = FontAwesome.Solid.ExclamationTriangle,
                            });
                        }
                        finally
                        {
                            loadingSpinner.Hide();
                            beatmapManager.ScanProgress.ValueChanged -= onProgress;
                            beatmapManager.StatusMessage.ValueChanged -= onStatus;
                        }
                    });
                }
                catch (Exception ex)
                {
                    Schedule(() =>
                    {
                        Logger.Error(ex, "BMS library scan failed");
                        loadingSpinner.Hide();
                        notification.State = ProgressNotificationState.Cancelled;
                        beatmapManager.ScanProgress.ValueChanged -= onProgress;
                        beatmapManager.StatusMessage.ValueChanged -= onStatus;
                    });
                }
            });
        }

        private void startGame()
        {
            if (selectedChart.Value == null)
            {
                notifications?.Post(new SimpleNotification
                {
                    Text = "请先选择一个谱面",
                    Icon = FontAwesome.Solid.ExclamationTriangle,
                });
                return;
            }

            try
            {
                BMSChartCache chart = selectedChart.Value;
                string chartPath = chart.FullPath;
                string resolveSource = "direct-chart-path";

                if (selectedVirtualBeatmap != null && beatmapManager.TryGetSourceReference(selectedVirtualBeatmap.ID, out BMSSourceReference sourceReference))
                {
                    chartPath = sourceReference.ChartPath;
                    resolveSource = $"source-map-by-id:{selectedVirtualBeatmap.ID}";
                }
                else if (beatmapManager.TryGetSourceReferenceByHash(chart.Md5Hash, out BMSSourceReference sourceByHash))
                {
                    chartPath = sourceByHash.ChartPath;
                    resolveSource = $"source-map-by-hash:{chart.Md5Hash}";
                }

                Logger.Log($"[BMS] StartGame chart resolve: title={chart.Title}, file={chart.FileName}, md5={chart.Md5Hash}, mode={BMSChartDisplayFormatter.GetModeText(chart)}, source={resolveSource}, path={chartPath}", LoggingTarget.Runtime, LogLevel.Debug);

                var workingBeatmap = new BMSWorkingBeatmap(chartPath, audioManager, textures, chart);

                // Push to player
                var route = mode == BMSSongSelectScreenMode.SpecialEntry
                    ? BMSGameplayRoute.BmsNative
                    : BMSGameplayRoute.ManiaCompatibility;
                this.Push(new BMSPlayerLoader(workingBeatmap, route));
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to load BMS beatmap");
                notifications?.Post(new SimpleNotification
                {
                    Text = $"加载谱面失败: {ex.Message}",
                    Icon = FontAwesome.Solid.ExclamationTriangle,
                });
            }
        }

        private void rebuildVirtualCatalog()
        {
            virtualBeatmapByHash.Clear();

            var rulesetInfo = new BMSRuleset().RulesetInfo;
            var virtualSets = beatmapManager.BuildVirtualBeatmapCatalog(rulesetInfo);

            foreach (BeatmapSetInfo set in virtualSets)
            {
                foreach (BeatmapInfo beatmap in set.Beatmaps)
                {
                    if (!string.IsNullOrWhiteSpace(beatmap.MD5Hash))
                        virtualBeatmapByHash[beatmap.MD5Hash] = beatmap;
                }
            }
        }

        public override void OnEntering(ScreenTransitionEvent e)
        {
            base.OnEntering(e);
            refreshSongList();
            this.FadeInFromZero(300);
        }

        public override bool OnExiting(ScreenExitEvent e)
        {
            previewManager.StopPreview();
            stopFallbackPreviewTrack();
            previewBeatmap = null;
            this.FadeOut(200);
            return base.OnExiting(e);
        }
    }

    /// <summary>
    /// V2-style song card for song list.
    /// Simplified card with title, artist, and chart count.
    /// </summary>
    public partial class BMSSongCardV2 : CompositeDrawable
    {
        public BMSSongCache Song { get; }
        public Action? Action { get; set; }

        private bool selected;

        public bool Selected
        {
            get => selected;
            set
            {
                if (selected == value)
                    return;

                selected = value;

                if (IsLoaded)
                    updateState();
            }
        }

        private Box background = null!;
        private Box hoverBox = null!;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        public BMSSongCardV2(BMSSongCache song)
        {
            Song = song;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.X;
            Height = 96;
            Masking = true;
            CornerRadius = 5;

            InternalChildren = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colourProvider.Background4,
                },
                hoverBox = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colourProvider.Light1,
                    Alpha = 0,
                },
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Horizontal = 15, Vertical = 10 },
                    Child = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 5),
                        Children = new Drawable[]
                        {
                            new TruncatingSpriteText
                            {
                                Text = string.IsNullOrEmpty(Song.Title) ? "(无标题)" : Song.Title,
                                Font = OsuFont.GetFont(size: 18, weight: FontWeight.Bold),
                                RelativeSizeAxes = Axes.X,
                            },
                            new TruncatingSpriteText
                            {
                                Text = string.IsNullOrEmpty(Song.Artist) ? "(未知艺术家)" : Song.Artist,
                                Font = OsuFont.GetFont(size: 14),
                                RelativeSizeAxes = Axes.X,
                                Colour = colourProvider.Content2,
                            },
                            new FillFlowContainer
                            {
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Horizontal,
                                Spacing = new Vector2(15, 0),
                                Children = new Drawable[]
                                {
                                    new OsuSpriteText
                                    {
                                        Text = BMSChartDisplayFormatter.GetSongSummaryText(Song),
                                        Font = OsuFont.GetFont(size: 14),
                                        Colour = colourProvider.Content2,
                                    },
                                    new OsuSpriteText
                                    {
                                        Text = $"BPM {BMSChartDisplayFormatter.GetSongBpmText(Song.Charts)}",
                                        Font = OsuFont.GetFont(size: 14),
                                        Colour = colourProvider.Content2,
                                    },
                                    new OsuSpriteText
                                    {
                                        Text = string.IsNullOrEmpty(Song.Genre) ? "Unknown Genre" : Song.Genre,
                                        Font = OsuFont.GetFont(size: 14),
                                        Colour = colourProvider.Content2,
                                    },
                                },
                            },
                        },
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            updateState();
        }

        protected override bool OnHover(HoverEvent e)
        {
            hoverBox.FadeTo(0.1f, 200);
            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            hoverBox.FadeOut(200);
            base.OnHoverLost(e);
        }

        protected override bool OnClick(ClickEvent e)
        {
            Action?.Invoke();
            return true;
        }

        private void updateState()
        {
            if (Selected)
            {
                background.FadeColour(colourProvider.Highlight1, 200);
            }
            else
            {
                background.FadeColour(colourProvider.Background4, 200);
            }
        }
    }

    internal static class BMSChartDisplayFormatter
    {
        public static string GetDifficultyTitle(BMSChartCache chart)
        {
            if (!string.IsNullOrWhiteSpace(chart.SubTitle))
                return chart.SubTitle;

            if (!string.IsNullOrWhiteSpace(chart.Title))
                return chart.Title;

            return Path.GetFileNameWithoutExtension(chart.FileName);
        }

        public static string GetModeText(BMSChartCache chart)
        {
            int scratchCount = chart.HasScratch ? (chart.KeyCount >= 10 ? 2 : 1) : 0;
            return scratchCount > 0 ? $"{Math.Max(1, chart.KeyCount)}K{scratchCount}S" : $"{Math.Max(1, chart.KeyCount)}K";
        }

        public static string GetLevelText(BMSChartCache chart)
            => chart.PlayLevel > 0 ? $"Lv {chart.PlayLevel}" : "Lv ?";

        public static string GetFlagsText(BMSChartCache chart)
        {
            List<string> flags = new List<string>();

            if (chart.HasScratch)
                flags.Add(chart.KeyCount >= 10 ? "双盘" : "单盘");

            if (chart.HasLongNotes)
                flags.Add("LN");

            if (chart.LnType >= 2)
                flags.Add($"LNT{chart.LnType}");

            if (chart.HasStopSequence)
                flags.Add("STOP");

            if (chart.HasScrollChanges)
                flags.Add("SCROLL");

            if (chart.HasBgaLayer)
                flags.Add("BGA");

            if (flags.Count == 0)
                flags.Add("标准");

            return string.Join(" / ", flags);
        }

        public static string GetBpmText(BMSChartCache chart)
        {
            double minBpm = chart.MinBpm > 0 ? chart.MinBpm : chart.Bpm;
            double maxBpm = chart.MaxBpm > 0 ? chart.MaxBpm : chart.Bpm;

            if (Math.Abs(maxBpm - minBpm) < 0.01)
                return $"{chart.Bpm:F0}";

            return $"{minBpm:F0}-{maxBpm:F0}";
        }

        public static string GetSongBpmText(IEnumerable<BMSChartCache> charts)
        {
            List<double> bpmValues = charts.SelectMany(chart => new[]
            {
                chart.MinBpm > 0 ? chart.MinBpm : chart.Bpm,
                chart.MaxBpm > 0 ? chart.MaxBpm : chart.Bpm,
            }).Where(bpm => bpm > 0).ToList();

            if (bpmValues.Count == 0)
                return "?";

            double minBpm = bpmValues.Min();
            double maxBpm = bpmValues.Max();

            if (Math.Abs(maxBpm - minBpm) < 0.01)
                return $"{minBpm:F0}";

            return $"{minBpm:F0}-{maxBpm:F0}";
        }

        public static string GetDurationText(double duration)
        {
            if (duration <= 0)
                return "--:--";

            TimeSpan time = TimeSpan.FromMilliseconds(duration);
            return time.TotalHours >= 1 ? time.ToString(@"h\:mm\:ss") : time.ToString(@"m\:ss");
        }

        public static string GetSongSummaryText(BMSSongCache song)
        {
            if (song.Charts.Count == 0)
                return "暂无难度";

            return $"{song.Charts.Count} 难度 | {getLevelRangeText(song.Charts)} | {getModeSummaryText(song.Charts)}";
        }

        private static string getLevelRangeText(IEnumerable<BMSChartCache> charts)
        {
            List<int> levels = charts.Select(chart => chart.PlayLevel).Where(level => level > 0).OrderBy(level => level).ToList();

            if (levels.Count == 0)
                return "Lv ?";

            return levels[0] == levels[^1] ? $"Lv {levels[0]}" : $"Lv {levels[0]}-{levels[^1]}";
        }

        private static string getModeSummaryText(IEnumerable<BMSChartCache> charts)
            => string.Join("/", charts.Select(GetModeText).Distinct());
    }
}

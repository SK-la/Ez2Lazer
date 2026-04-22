// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
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
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets.BMS.Beatmaps;
using osu.Game.Rulesets.BMS.Configuration;
using osu.Game.Rulesets.Configuration;
using osu.Game.Screens;
using osu.Game.Screens.Play;
using osu.Game.Tests.Visual;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.BMS.UI.SongSelect
{
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
        protected override bool InitialBackButtonVisibility => true;

        private BMSBeatmapManager beatmapManager = null!;
        private OsuScrollContainer scrollContainer = null!;
        private OsuSpriteText statusText = null!;
        private FillFlowContainer<BMSSongCardV2> songList = null!;
        private LoadingSpinner loadingSpinner = null!;
        private SearchTextBox searchBox = null!;
        private BMSInfoPanel infoPanel = null!;
        private Track? previewTrack;

        private readonly Bindable<BMSSongCache?> selectedSong = new Bindable<BMSSongCache?>();
        private readonly Bindable<BMSChartCache?> selectedChart = new Bindable<BMSChartCache?>();
        private Bindable<string> libraryPathsBindable = null!;
        private Bindable<string> legacyRootPathBindable = null!;

        [Resolved]
        private AudioManager audioManager { get; set; } = null!;

        [Resolved]
        private TextureStore textures { get; set; } = null!;

        [Resolved]
        private Storage storage { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private INotificationOverlay? notifications { get; set; }

        [Resolved(canBeNull: true)]
        private IPerformFromScreenRunner? performer { get; set; }

        [Resolved]
        private IRulesetConfigCache rulesetConfigCache { get; set; } = null!;

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

            // Initialize manager
            var cacheDir = storage.GetStorageForDirectory("bms").GetFullPath(string.Empty);
            beatmapManager = new BMSBeatmapManager(cacheDir);
            beatmapManager.LoadCache();

            // Sync root path from config to manager
            beatmapManager.SetRootPaths(BMSRulesetConfigManager.ParseLibraryPaths(libraryPathsBindable.Value, legacyRootPathBindable.Value));

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
                                            infoPanel = new BMSInfoPanel
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
                                                            Action = StartGame,
                                                        },
                                                        new RoundedButton
                                                        {
                                                            Text = "刷新曲库",
                                                            Width = 120,
                                                            Height = 50,
                                                            Action = RefreshLibrary,
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
                                                                Text = "BMS曲库",
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
                                                                Text = beatmapManager.StatusMessage.Value,
                                                                Font = OsuFont.GetFont(size: 14),
                                                                Colour = colours.Yellow,
                                                            },
                                                        }
                                                    },
                                                    scrollContainer = new OsuScrollContainer
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
                    }
                },
            };

            // Bind events
            searchBox.Current.BindValueChanged(e => FilterSongs(e.NewValue));
            beatmapManager.StatusMessage.BindValueChanged(e => statusText.Text = e.NewValue);
            selectedChart.BindValueChanged(e => updatePreviewTrack(e.NewValue), true);

            // Load songs
            RefreshSongList();
        }

        private void updatePreviewTrack(BMSChartCache? chart)
        {
            previewTrack?.Stop();
            previewTrack?.Dispose();
            previewTrack = null;

            if (chart == null)
                return;

            string? audioPath = BMSWorkingBeatmap.ResolveAudioPath(chart.FolderPath, chart.AudioFile);

            if (string.IsNullOrEmpty(audioPath))
                return;

            previewTrack = audioManager.Tracks.Get(audioPath);
            previewTrack.Looping = true;

            if (!previewTrack.IsLoaded)
                previewTrack.Seek(previewTrack.CurrentTime);

            double restartPoint = chart.PreviewTime;

            if (restartPoint < 0 || restartPoint > previewTrack.Length)
                restartPoint = previewTrack.Length * 0.4;

            previewTrack.RestartPoint = restartPoint;
            previewTrack.Seek(restartPoint);
            previewTrack.Start();
        }

        private void RefreshSongList()
        {
            songList.Clear();

            if (beatmapManager.LibraryCache == null || beatmapManager.LibraryCache.Songs.Count == 0)
            {
                statusText.Text = "曲库为空，请先扫描 BMS 文件夹";
                return;
            }

            foreach (var song in beatmapManager.LibraryCache.Songs.OrderBy(s => s.Title))
            {
                songList.Add(new BMSSongCardV2(song)
                {
                    Action = () => SelectSong(song),
                });
            }

            statusText.Text = $"共 {beatmapManager.LibraryCache.Songs.Count} 首歌曲";
        }

        private void FilterSongs(string filter)
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

        private void SelectSong(BMSSongCache song)
        {
            selectedSong.Value = song;

            // Auto-select first chart
            if (song.Charts.Count > 0)
            {
                selectedChart.Value = song.Charts[0];
            }
            else
            {
                selectedChart.Value = null;
            }

            // Update song card selection state
            foreach (var card in songList)
            {
                card.Selected = card.Song == song;
            }
        }

        private void RefreshLibrary()
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

            var notification = new ProgressNotification
            {
                Text = "正在扫描 BMS 曲库...",
            };

            notifications?.Post(notification);

            // Bind progress
            beatmapManager.ScanProgress.BindValueChanged(e =>
            {
                notification.Progress = (float)e.NewValue;
            });

            beatmapManager.StatusMessage.BindValueChanged(e =>
            {
                notification.Text = e.NewValue;
            });

            _ = beatmapManager.ScanLibraryAsync(beatmapManager.RootPaths).ContinueWith(_ =>
            {
                Schedule(() =>
                {
                    loadingSpinner.Hide();
                    notification.State = ProgressNotificationState.Completed;
                    RefreshSongList();
                });
            });
        }

        private void StartGame()
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
                var workingBeatmap = new BMSWorkingBeatmap(
                    selectedChart.Value.FullPath,
                    audioManager,
                    textures);

                // Push to player
                this.Push(new BMSPlayerLoader(workingBeatmap));
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

        public override void OnEntering(ScreenTransitionEvent e)
        {
            base.OnEntering(e);
            this.FadeInFromZero(300);
        }

        public override bool OnExiting(ScreenExitEvent e)
        {
            previewTrack?.Stop();
            previewTrack?.Dispose();
            previewTrack = null;
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
            Height = 70;
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
                            new OsuSpriteText
                            {
                                Text = string.IsNullOrEmpty(Song.Title) ? "(无标题)" : Song.Title,
                                Font = OsuFont.GetFont(size: 18, weight: FontWeight.Bold),
                                Truncate = true,
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
                                        Text = string.IsNullOrEmpty(Song.Artist) ? "(未知艺术家)" : Song.Artist,
                                        Font = OsuFont.GetFont(size: 14),
                                        Colour = colourProvider.Content2,
                                    },
                                    new OsuSpriteText
                                    {
                                        Text = $"{Song.Charts.Count} 个难度",
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
}

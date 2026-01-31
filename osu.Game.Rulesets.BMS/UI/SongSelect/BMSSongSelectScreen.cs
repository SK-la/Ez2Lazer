// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Audio;
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
using osu.Game.Screens;
using osu.Game.Screens.Play;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.BMS.UI.SongSelect
{
    /// <summary>
    /// Independent song select screen for BMS ruleset.
    /// Loads songs from BMSBeatmapManager cache instead of Realm database.
    /// </summary>
    public partial class BMSSongSelectScreen : OsuScreen
    {
        protected override bool InitialBackButtonVisibility => true;

        private BMSBeatmapManager beatmapManager = null!;
        private FillFlowContainer<BMSSongCard> songList = null!;
        private OsuScrollContainer scrollContainer = null!;
        private Container detailPanel = null!;
        private OsuSpriteText titleText = null!;
        private OsuSpriteText artistText = null!;
        private OsuSpriteText statusText = null!;
        private FillFlowContainer<BMSChartCard> chartList = null!;
        private LoadingSpinner loadingSpinner = null!;
        private SearchTextBox searchBox = null!;

        private BMSSongCache? selectedSong;
        private BMSChartCache? selectedChart;

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

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            // Initialize manager
            var cacheDir = storage.GetStorageForDirectory("bms").GetFullPath(string.Empty);
            beatmapManager = new BMSBeatmapManager(cacheDir);
            beatmapManager.LoadCache();

            InternalChildren = new Drawable[]
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
                        new Dimension(GridSizeMode.Relative, 0.4f),
                        new Dimension(),
                    },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            // Left panel - Song list
                            new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Padding = new MarginPadding(10),
                                Children = new Drawable[]
                                {
                                    new Box
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Colour = colours.Gray2,
                                    },
                                    new FillFlowContainer
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Direction = FillDirection.Vertical,
                                        Padding = new MarginPadding(10),
                                        Spacing = new Vector2(0, 10),
                                        Children = new Drawable[]
                                        {
                                            new OsuSpriteText
                                            {
                                                Text = "BMS 曲库",
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
                                            scrollContainer = new OsuScrollContainer
                                            {
                                                RelativeSizeAxes = Axes.Both,
                                                Child = songList = new FillFlowContainer<BMSSongCard>
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
                            // Right panel - Detail view
                            new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Padding = new MarginPadding(10),
                                Child = detailPanel = new Container
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Children = new Drawable[]
                                    {
                                        new Box
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            Colour = colours.Gray3,
                                        },
                                        new FillFlowContainer
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            Direction = FillDirection.Vertical,
                                            Padding = new MarginPadding(20),
                                            Spacing = new Vector2(0, 10),
                                            Children = new Drawable[]
                                            {
                                                titleText = new OsuSpriteText
                                                {
                                                    Text = "选择一首歌曲",
                                                    Font = OsuFont.GetFont(size: 32, weight: FontWeight.Bold),
                                                },
                                                artistText = new OsuSpriteText
                                                {
                                                    Text = "",
                                                    Font = OsuFont.GetFont(size: 20),
                                                    Colour = colours.Yellow,
                                                },
                                                new OsuSpriteText
                                                {
                                                    Text = "难度列表",
                                                    Font = OsuFont.GetFont(size: 18, weight: FontWeight.SemiBold),
                                                    Margin = new MarginPadding { Top = 20 },
                                                },
                                                new OsuScrollContainer
                                                {
                                                    RelativeSizeAxes = Axes.X,
                                                    Height = 300,
                                                    Child = chartList = new FillFlowContainer<BMSChartCard>
                                                    {
                                                        RelativeSizeAxes = Axes.X,
                                                        AutoSizeAxes = Axes.Y,
                                                        Direction = FillDirection.Vertical,
                                                        Spacing = new Vector2(0, 5),
                                                    },
                                                },
                                                new FillFlowContainer
                                                {
                                                    AutoSizeAxes = Axes.Both,
                                                    Direction = FillDirection.Horizontal,
                                                    Spacing = new Vector2(10, 0),
                                                    Margin = new MarginPadding { Top = 20 },
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
                                },
                            },
                        },
                    },
                },
                loadingSpinner = new LoadingSpinner
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(60),
                },
            };

            // Bind events
            searchBox.Current.BindValueChanged(e => FilterSongs(e.NewValue));
            beatmapManager.StatusMessage.BindValueChanged(e => statusText.Text = e.NewValue);

            // Load songs
            RefreshSongList();
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
                songList.Add(new BMSSongCard(song)
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
            selectedSong = song;
            selectedChart = null;

            titleText.Text = string.IsNullOrEmpty(song.Title) ? "(无标题)" : song.Title;
            artistText.Text = string.IsNullOrEmpty(song.Artist) ? "(未知艺术家)" : song.Artist;

            chartList.Clear();

            foreach (var chart in song.Charts.OrderBy(c => c.PlayLevel))
            {
                chartList.Add(new BMSChartCard(chart)
                {
                    Action = () => SelectChart(chart),
                });
            }

            // Auto-select first chart
            if (song.Charts.Count > 0)
            {
                SelectChart(song.Charts[0]);
            }

            // Update song card selection state
            foreach (var card in songList)
            {
                card.Selected = card.Song == song;
            }
        }

        private void SelectChart(BMSChartCache chart)
        {
            selectedChart = chart;

            // Update chart card selection state
            foreach (var card in chartList)
            {
                card.Selected = card.Chart == chart;
            }
        }

        private void RefreshLibrary()
        {
            if (string.IsNullOrEmpty(beatmapManager.RootPath.Value))
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

            _ = beatmapManager.ScanLibraryAsync(beatmapManager.RootPath.Value).ContinueWith(_ =>
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
            if (selectedChart == null)
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
                    selectedChart.FullPath,
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
            this.FadeOut(200);
            return base.OnExiting(e);
        }
    }
}

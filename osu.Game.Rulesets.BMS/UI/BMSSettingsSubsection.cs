// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Overlays.Settings;
using osu.Game.Localisation;
using osu.Game.Rulesets.BMS.Beatmaps;
using osu.Game.Rulesets.BMS.Configuration;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Configuration;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.BMS.UI.SongSelect;
using osu.Game.Screens;
using osuTK;

namespace osu.Game.Rulesets.BMS.UI
{
    public partial class BMSSettingsSubsection : RulesetSettingsSubsection
    {
        protected override LocalisableString Header => "BMS";

        private BMSRulesetConfigManager bmsConfig = null!;
        private Bindable<string> libraryPathsBindable = null!;
        private Bindable<string> legacyRootPathBindable = null!;

        private OsuTextFlowContainer pathDisplay = null!;
        private OsuTextFlowContainer statusDisplay = null!;
        private Bindable<double>? maniaScrollSpeed;
        private Bindable<double>? maniaBaseSpeed;
        private Bindable<double>? maniaTimePerSpeed;

        [Resolved]
        private OsuGame? game { get; set; }

        [Resolved]
        private INotificationOverlay? notificationOverlay { get; set; }

        [Resolved]
        private Storage storage { get; set; } = null!;

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

            string cacheDir = storage.GetFullPath("bms_cache");
            beatmapManager = BMSBeatmapManager.GetShared(cacheDir);
            beatmapManager.SetRootPaths(getConfiguredPaths());
            bindManiaScrollSettings();

            Children = new Drawable[]
            {
                new SettingsButton
                {
                    Text = "进入 BMS 专用选歌界面（辅助入口）",
                    Action = openBmsSongSelect,
                },
                new SettingsButton
                {
                    Text = "打开 BMS 曲库设置向导",
                    Action = selectPath,
                },
                new SettingsItemV2(new FormTextBox
                {
                    Caption = "滚速说明",
                    Current = new Bindable<string>("BMS 当前直接复用 mania 的滚速参数、方向和快捷键（含 LAlt 加速步进）。"),
                    ReadOnly = true,
                }),
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 110,
                    Padding = new MarginPadding { Left = SettingsPanel.CONTENT_MARGINS, Right = SettingsPanel.CONTENT_MARGINS, Top = 6 },
                    Child = new OsuScrollContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Child = pathDisplay = new OsuTextFlowContainer(cp => cp.Font = OsuFont.Default.With(size: 13))
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                        }
                    }
                },
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = new MarginPadding { Left = SettingsPanel.CONTENT_MARGINS, Right = SettingsPanel.CONTENT_MARGINS, Top = 4, Bottom = 6 },
                    Child = statusDisplay = new OsuTextFlowContainer(cp => cp.Font = OsuFont.Default.With(size: 12))
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                    }
                },
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
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = "自动预加载 Keysound",
                    Current = bmsConfig.GetBindable<bool>(BMSRulesetSetting.AutoPreloadKeysounds),
                }),
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = "Keysound 音量",
                    Current = bmsConfig.GetBindable<double>(BMSRulesetSetting.KeysoundVolume),
                    KeyboardStep = 0.01f,
                    LabelFormat = v => $"{v:P0}",
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

            // Show initial cache status
            if (beatmapManager?.LibraryCache != null)
            {
                statusDisplay.Text = $"已缓存 {beatmapManager.LibraryCache.Songs.Count} 首歌曲, {beatmapManager.LibraryCache.TotalCharts} 张谱面";
            }
        }

        private IReadOnlyList<string> getConfiguredPaths()
            => BMSRulesetConfigManager.ParseLibraryPaths(libraryPathsBindable.Value, legacyRootPathBindable.Value);

        private void updatePathDisplay()
        {
            IReadOnlyList<string> paths = getConfiguredPaths();

            if (paths.Count == 0)
                pathDisplay.Text = "未设置路径";
            else
                pathDisplay.Text = $"当前路径 ({paths.Count}):{Environment.NewLine}{string.Join(Environment.NewLine, paths.Select(path => $"- {path}"))}";
        }

        private void openBmsSongSelect()
        {
            game?.PerformFromScreen(screen =>
            {
                screen.Push(new BMSSongSelectScreen(BMSSongSelectScreenMode.RulesetEntry));
            });
        }

        private void selectPath()
        {
            game?.PerformFromScreen(screen =>
            {
                screen.Push(new BMSDirectorySelectScreen(libraryPathsBindable, legacyRootPathBindable, applyPathsAndScan));
            });
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
                    Text = "请先在向导中添加至少一个有效的 BMS 文件夹路径"
                });
                return;
            }

            statusDisplay.Text = "正在扫描...";

            var notification = new ProgressNotification
            {
                Text = "正在扫描 BMS 歌曲库...",
                CompletionText = "BMS 歌曲库扫描完成!",
                State = ProgressNotificationState.Active
            };

            notificationOverlay?.Post(notification);

            // Subscribe to progress updates
            beatmapManager.ScanProgress.BindValueChanged(e =>
            {
                Schedule(() => notification.Progress = (float)e.NewValue);
            });

            beatmapManager.StatusMessage.BindValueChanged(e =>
            {
                Schedule(() => notification.Text = e.NewValue);
            });

            Task.Run(async () =>
            {
                try
                {
                    await beatmapManager.ScanLibraryAsync(paths).ConfigureAwait(false);

                    Schedule(() =>
                    {
                        notification.State = ProgressNotificationState.Completed;

                        if (beatmapManager.LibraryCache != null)
                        {
                            statusDisplay.Text = $"扫描完成! {beatmapManager.LibraryCache.Songs.Count} 首歌曲, {beatmapManager.LibraryCache.TotalCharts} 张谱面";
                        }
                    });
                }
                catch (Exception ex)
                {
                    Schedule(() =>
                    {
                        notification.State = ProgressNotificationState.Cancelled;
                        statusDisplay.Text = $"扫描失败: {ex.Message}";
                    });
                }
            });
        }
    }

    /// <summary>
    /// Screen for selecting BMS root directory.
    /// </summary>
    public partial class BMSDirectorySelectScreen : OsuScreen
    {
        private readonly Bindable<string> libraryPathsBindable;
        private readonly Bindable<string> legacyRootPathBindable;
        private readonly List<string> stagedPaths;
        private readonly Action<IReadOnlyList<string>>? applyAction;
        private OsuDirectorySelector directorySelector = null!;
        private FillFlowContainer pathList = null!;

        [Cached]
        private OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Purple);

        public BMSDirectorySelectScreen(Bindable<string> libraryPathsBindable, Bindable<string> legacyRootPathBindable, Action<IReadOnlyList<string>>? applyAction = null)
        {
            this.libraryPathsBindable = libraryPathsBindable;
            this.legacyRootPathBindable = legacyRootPathBindable;
            this.applyAction = applyAction;
            stagedPaths = BMSRulesetConfigManager.ParseLibraryPaths(libraryPathsBindable.Value, legacyRootPathBindable.Value).ToList();
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            string? initialPath = stagedPaths.LastOrDefault();

            InternalChild = new Container
            {
                Masking = true,
                CornerRadius = 10,
                RelativeSizeAxes = Axes.Both,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(0.7f, 0.8f),
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colourProvider.Background4,
                    },
                    new GridContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        RowDimensions = new[]
                        {
                            new Dimension(GridSizeMode.AutoSize),
                            new Dimension(),
                            new Dimension(GridSizeMode.AutoSize),
                        },
                        Content = new[]
                        {
                            new Drawable[]
                            {
                                new FillFlowContainer
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Direction = FillDirection.Vertical,
                                    Spacing = new Vector2(0, 6),
                                    Margin = new MarginPadding(10),
                                    Children = new Drawable[]
                                    {
                                        new OsuTextFlowContainer(cp => cp.Font = OsuFont.Default.With(size: 24))
                                        {
                                            Text = "BMS 曲库设置向导",
                                            TextAnchor = Anchor.TopCentre,
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                        },
                                        new OsuTextFlowContainer(cp => cp.Font = OsuFont.Default.With(size: 14))
                                        {
                                            Text = "先添加任意数量的文件夹路径。应用会立即保存并重建扫描，确定只关闭当前向导。",
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                        }
                                    }
                                }
                            },
                            new Drawable[]
                            {
                                new GridContainer
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    RowDimensions = new[]
                                    {
                                        new Dimension(GridSizeMode.Absolute, 260),
                                        new Dimension(),
                                    },
                                    Content = new[]
                                    {
                                        new Drawable[]
                                        {
                                            directorySelector = new OsuDirectorySelector(initialPath)
                                            {
                                                RelativeSizeAxes = Axes.Both,
                                            }
                                        },
                                        new Drawable[]
                                        {
                                            new FillFlowContainer
                                            {
                                                RelativeSizeAxes = Axes.Both,
                                                Direction = FillDirection.Vertical,
                                                Spacing = new Vector2(0, 8),
                                                Children = new Drawable[]
                                                {
                                                    new FillFlowContainer
                                                    {
                                                        RelativeSizeAxes = Axes.X,
                                                        AutoSizeAxes = Axes.Y,
                                                        Direction = FillDirection.Horizontal,
                                                        Spacing = new Vector2(10, 0),
                                                        Children = new Drawable[]
                                                        {
                                                            new RoundedButton
                                                            {
                                                                Width = 180,
                                                                Text = "添加当前路径",
                                                                Action = addSelectedPath,
                                                            },
                                                            new RoundedButton
                                                            {
                                                                Width = 140,
                                                                Text = "清空列表",
                                                                Action = () =>
                                                                {
                                                                    stagedPaths.Clear();
                                                                    refreshPathList();
                                                                },
                                                            },
                                                        },
                                                    },
                                                    new OsuTextFlowContainer(cp => cp.Font = OsuFont.Default.With(size: 16))
                                                    {
                                                        Text = "已添加的路径",
                                                        RelativeSizeAxes = Axes.X,
                                                        AutoSizeAxes = Axes.Y,
                                                    },
                                                    new OsuScrollContainer
                                                    {
                                                        RelativeSizeAxes = Axes.Both,
                                                        Child = pathList = new FillFlowContainer
                                                        {
                                                            RelativeSizeAxes = Axes.X,
                                                            AutoSizeAxes = Axes.Y,
                                                            Direction = FillDirection.Vertical,
                                                            Spacing = new Vector2(0, 8),
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            },
                            new Drawable[]
                            {
                                new FillFlowContainer
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Direction = FillDirection.Horizontal,
                                    Spacing = new Vector2(10),
                                    Padding = new MarginPadding(10),
                                    Children = new Drawable[]
                                    {
                                        new RoundedButton
                                        {
                                            Width = 200,
                                            Text = "确定",
                                            Action = this.Exit,
                                        },
                                        new RoundedButton
                                        {
                                            Width = 200,
                                            Text = "应用",
                                            Action = applyPaths,
                                        },
                                    }
                                }
                            }
                        }
                    }
                }
            };

            refreshPathList();
        }

        private void addSelectedPath()
        {
            string? selectedPath = directorySelector.CurrentPath.Value?.FullName;

            if (string.IsNullOrWhiteSpace(selectedPath) || !Directory.Exists(selectedPath))
                return;

            if (stagedPaths.Any(path => string.Equals(path, selectedPath, StringComparison.OrdinalIgnoreCase)))
                return;

            stagedPaths.Add(selectedPath);
            refreshPathList();
        }

        private void applyPaths()
        {
            libraryPathsBindable.Value = BMSRulesetConfigManager.SerialiseLibraryPaths(stagedPaths);
            legacyRootPathBindable.Value = stagedPaths.FirstOrDefault() ?? string.Empty;
            applyAction?.Invoke(stagedPaths.ToArray());
        }

        private void refreshPathList()
        {
            pathList.Clear();

            if (stagedPaths.Count == 0)
            {
                pathList.Add(new OsuTextFlowContainer(cp => cp.Font = OsuFont.Default.With(size: 14))
                {
                    Text = "暂未添加路径。",
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                });
                return;
            }

            foreach (string path in stagedPaths)
            {
                pathList.Add(new GridContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    ColumnDimensions = new[]
                    {
                        new Dimension(),
                        new Dimension(GridSizeMode.Absolute, 100),
                    },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            new OsuTextFlowContainer(cp => cp.Font = OsuFont.Default.With(size: 13))
                            {
                                Text = path,
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                            },
                            new RoundedButton
                            {
                                Width = 90,
                                Text = "移除",
                                Action = () =>
                                {
                                    stagedPaths.Remove(path);
                                    refreshPathList();
                                },
                            },
                        }
                    }
                });
            }
        }

        public override void OnSuspending(ScreenTransitionEvent e)
        {
            base.OnSuspending(e);
            this.FadeOut(250);
        }
    }
}

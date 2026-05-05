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
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Database;
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

namespace osu.Game.Rulesets.BMS.UI
{
    public partial class BMSSettingsSubsection : RulesetSettingsSubsection
    {
        protected override LocalisableString Header => "BMS";

        private BMSRulesetConfigManager bmsConfig = null!;
        private Bindable<string> libraryPathsBindable = null!;
        private Bindable<string> legacyRootPathBindable = null!;

        private OsuTextFlowContainer pathDisplay = null!;
        private SettingsNote cacheStatusNote = null!;
        private SettingsNote speedNote = null!;
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
        private RealmAccess realm { get; set; } = null!;

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
                new SettingsButtonV2
                {
                    Text = "进入 BMS 专用选歌界面（辅助入口）",
                    Action = openBmsSongSelect,
                },
                new SettingsButtonV2
                {
                    Text = "打开 BMS 曲库设置向导",
                    Action = selectPath,
                },
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
                    Padding = SettingsPanel.CONTENT_PADDING,
                    Child = cacheStatusNote = new SettingsNote
                    {
                        RelativeSizeAxes = Axes.X,
                    },
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
                    Caption = "自动预加载 Key-sound",
                    Current = bmsConfig.GetBindable<bool>(BMSRulesetSetting.AutoPreloadKeysounds),
                }),
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = "Key-sound 音量",
                    Current = bmsConfig.GetBindable<double>(BMSRulesetSetting.KeysoundVolume),
                    KeyboardStep = 0.01f,
                    LabelFormat = v => $"{v:P0}",
                }),
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = "DP-Stage 间距",
                    HintText = "DP模式(10k+)，控制左右面板之间的间距。",
                    Current = bmsConfig.GetBindable<double>(BMSRulesetSetting.DpStageSpacing),
                    KeyboardStep = 1,
                    LabelFormat = v => $"{v:0}",
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

            speedNote.Current.Value = new SettingsNote.Data("BMS 复用 mania 设置及快捷键（含 LAlt 加速步进）。", SettingsNote.Type.Informational);

            // Show initial cache status
            if (beatmapManager?.LibraryCache != null)
            {
                cacheStatusNote.Current.Value = new SettingsNote.Data($"已缓存 {beatmapManager.LibraryCache.Songs.Count} 首歌曲, {beatmapManager.LibraryCache.TotalCharts} 张谱面", SettingsNote.Type.Informational);
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

            cacheStatusNote.Current.Value = new SettingsNote.Data("正在扫描...", SettingsNote.Type.Informational);

            var notification = new ProgressNotification
            {
                Text = "正在扫描 BMS 歌曲库...",
                CompletionText = "BMS 歌曲库扫描完成!",
                State = ProgressNotificationState.Active,
                Progress = 0 // 初始化进度为 0,显示进度条而不是转圈
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
                        // 确保进度条达到 100%
                        notification.Progress = 1.0f;
                        notification.State = ProgressNotificationState.Completed;

                        BMSOsuLibrarySynchronizer.Synchronize(beatmapManager, storage, realm, new BMSRuleset().RulesetInfo);

                        if (beatmapManager.LibraryCache != null)
                        {
                            cacheStatusNote.Current.Value = new SettingsNote.Data($"扫描完成! {beatmapManager.LibraryCache.Songs.Count} 首歌曲, {beatmapManager.LibraryCache.TotalCharts} 张谱面", SettingsNote.Type.Informational);
                        }
                    });
                }
                catch (Exception ex)
                {
                    Schedule(() =>
                    {
                        notification.State = ProgressNotificationState.Cancelled;
                        cacheStatusNote.Current.Value = new SettingsNote.Data($"扫描失败: {ex.Message}", SettingsNote.Type.Critical);
                    });
                }
            });
        }
    }
}

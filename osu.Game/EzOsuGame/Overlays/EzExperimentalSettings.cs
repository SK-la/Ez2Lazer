// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Database;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.LocalProfile;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets;

namespace osu.Game.EzOsuGame.Overlays
{
    public partial class EzExperimentalSettings : SettingsSubsection
    {
        protected override LocalisableString Header => EZ_EXPERIMENTAL_SECTION_HEADER;

        [BackgroundDependencyLoader]
        private void load(Ez2ConfigManager ezConfig,
                          BackgroundDataStoreProcessor? backgroundDataStoreProcessor,
                          EzAnalysisWarmupProcessor? analysisWarmupProcessor,
                          IDialogOverlay? dialogOverlay,
                          INotificationOverlay? notifications,
                          EzLocalProfileService? localProfileService,
                          EzLocalProfileOnlinePullService? onlinePullService,
                          RulesetStore? rulesetStore,
                          EzExternalRulesetManagerDialog? externalRulesetManager)
        {
            EzDataRebuildSettingsSection.AddTo(this, backgroundDataStoreProcessor, analysisWarmupProcessor, dialogOverlay, notifications);

            Add(new SettingsButtonV2
            {
                Text = EzSettingsStrings.LOCAL_PROFILE_COMPUTE,
                TooltipText = EzSettingsStrings.LOCAL_PROFILE_COMPUTE_TOOLTIP,
                Keywords = new[] { "local", "profile", "stats", "kps", "个人", "本地", "统计", "成绩" },
                Action = () => requestComputeLocalProfile(localProfileService, dialogOverlay, notifications),
            });

            Add(new SettingsButtonV2
            {
                Text = EzSettingsStrings.LOCAL_PROFILE_ONLINE_PULL,
                TooltipText = EzSettingsStrings.LOCAL_PROFILE_ONLINE_PULL_TOOLTIP,
                Keywords = new[] { "online", "bp", "most played", "osr", "下载", "拉取", "线上", "成绩", "谱面", "回放" },
                Action = () => requestOnlinePull(onlinePullService, localProfileService, rulesetStore, dialogOverlay, notifications),
            });

            Add(new SettingsButtonV2
            {
                Text = EzSettingsStrings.EXTERNAL_RULESET_MANAGER,
                TooltipText = EzSettingsStrings.EXTERNAL_RULESET_MANAGER_TOOLTIP,
                Keywords = new[] { "ruleset", "external", "mapping", "onlineid", "第三方", "规则集", "映射", "外部" },
                Action = () => externalRulesetManager?.ShowManager(),
            });

            AddRange(new Drawable[]
            {
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = EzSettingsStrings.EZ_ANALYSIS_REC_ENABLED,
                    HintText = EzSettingsStrings.EZ_ANALYSIS_REC_ENABLED_TOOLTIP,
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.EzAnalysisRecEnabled),
                })
                {
                    Keywords = new[] { "analysis", "ez", "song select", "kps", "kpc" }
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = EzSettingsStrings.EZ_ANALYSIS_SQLITE_ENABLED,
                    HintText = EzSettingsStrings.EZ_ANALYSIS_SQLITE_ENABLED_TOOLTIP,
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.EzAnalysisSqliteEnabled),
                })
                {
                    Keywords = new[] { "analysis", "sqlite", "cache", "warmup", "persistent" }
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.EzSubFrameCorrectionEnabled),
                    Caption = EZ_SUB_FRAME_CORRECTION_ENABLED,
                    HintText = EZ_SUB_FRAME_CORRECTION_ENABLED_TOOLTIP,
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.EzJudgmentDiagEnabled),
                    Caption = EZ_JUDGMENT_DIAG_ENABLED,
                    HintText = EZ_JUDGMENT_DIAG_ENABLED_TOOLTIP,
                }),

                new SettingsItemV2(new FormCheckBox
                {
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.EzTimingTraceEnabled),
                    Caption = EZ_TIMING_TRACE_ENABLED,
                    HintText = EZ_TIMING_TRACE_ENABLED_TOOLTIP,
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.InputAudioLatencyTracker),
                    Caption = INPUT_AUDIO_LATENCY_TRACKER,
                    HintText = INPUT_AUDIO_LATENCY_TRACKER_TOOLTIP,
                })
                {
                    Keywords = new[] { "latency", "audio", "input" }
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.EzScoreRaceServiceEnabled),
                    Caption = EZ_SCORE_RACE_SERVICE_ENABLED,
                    HintText = EZ_SCORE_RACE_SERVICE_ENABLED_TOOLTIP,
                })
                {
                    Keywords = new[] { "race", "timeline", "角逐", "时间线", "fps" }
                },
                new SettingsItemV2(new FormEnumDropdown<EzReplayFeedMode>
                {
                    Current = ezConfig.GetBindable<EzReplayFeedMode>(Ez2Setting.EzScoreRaceFeedMode),
                    Caption = EZ_SCORE_RACE_FEED_MODE,
                    HintText = EZ_SCORE_RACE_FEED_MODE_TOOLTIP,
                })
                {
                    Keywords = new[] { "race", "feed", "batch", "stream", "角逐", "预建" }
                },
            });
        }

        private void requestComputeLocalProfile(
            EzLocalProfileService? localProfileService,
            IDialogOverlay? dialogOverlay,
            INotificationOverlay? notifications)
        {
            if (localProfileService == null || dialogOverlay == null)
            {
                notifications?.Post(new SimpleErrorNotification { Text = EzSettingsStrings.LOCAL_PROFILE_COMPUTE_FAILED });
                return;
            }

            if (localProfileService.IsComputing.Value)
            {
                notifications?.Post(new SimpleNotification { Text = EzSettingsStrings.LOCAL_PROFILE_COMPUTE_STARTED });
                return;
            }

            var counts = localProfileService.ScanUsernameCounts();

            if (counts.Count == 0)
            {
                if (!localProfileService.HasOnlineScoreContributions())
                {
                    notifications?.Post(new SimpleNotification { Text = EzSettingsStrings.LOCAL_PROFILE_NO_SCORES });
                    return;
                }

                runCompute(localProfileService, localProfileService.GetPreviouslyIncludedUsernames(), replaceIncludedUsernames: false, notifications);
                return;
            }

            dialogOverlay.Push(new EzLocalProfileImportDialog(
                counts,
                localProfileService.GetPreviouslyIncludedUsernames(),
                (selected, replaceMode) =>
                {
                    if (selected.Count == 0 && !localProfileService.HasOnlineScoreContributions()
                                            && (replaceMode || localProfileService.GetPreviouslyIncludedUsernames().Count == 0))
                    {
                        notifications?.Post(new SimpleNotification { Text = EzSettingsStrings.LOCAL_PROFILE_NONE_SELECTED });
                        return;
                    }

                    runCompute(localProfileService, selected, replaceMode, notifications);
                }));
        }

        private void runCompute(
            EzLocalProfileService localProfileService,
            IReadOnlyCollection<string> selected,
            bool replaceIncludedUsernames,
            INotificationOverlay? notifications)
        {
            if (notifications == null)
            {
                localProfileService.ComputeAsync(selected, replaceIncludedUsernames).ContinueWith(t => Schedule(() =>
                {
                    if (!t.IsFaulted && !t.IsCanceled)
                        localProfileService.ReloadFromDisk();
                }));
                return;
            }

            var notification = new ProgressNotification
            {
                Text = EzSettingsStrings.LOCAL_PROFILE_COMPUTE_STARTED,
                CompletionText = EzSettingsStrings.LOCAL_PROFILE_COMPUTE_DONE,
                State = ProgressNotificationState.Active,
            };

            notifications.Post(notification);

            // Do not use Progress<T> (SyncContext flood). Update the notification directly —
            // its Text/Progress/State setters marshal via the notification's own Scheduler,
            // which keeps running even if the settings panel is closed.
            var progress = new DirectLocalProfileComputeProgress(notification);

            localProfileService.ComputeAsync(selected, replaceIncludedUsernames, progress, notification.CancellationToken)
                               .ContinueWith(t => finishComputeNotification(t, localProfileService, notification, notifications));
        }

        /// <summary>
        /// Forwards compute progress to a <see cref="ProgressNotification"/> from any thread.
        /// </summary>
        private sealed class DirectLocalProfileComputeProgress : IProgress<EzLocalProfileComputeProgress>
        {
            private readonly ProgressNotification notification;

            public DirectLocalProfileComputeProgress(ProgressNotification notification)
            {
                this.notification = notification;
            }

            public void Report(EzLocalProfileComputeProgress value)
            {
                if (notification.State is ProgressNotificationState.Cancelled or ProgressNotificationState.Completed)
                    return;

                if (value.Saving)
                {
                    notification.Text = EzSettingsStrings.LOCAL_PROFILE_COMPUTE_SAVING;
                    notification.Progress = 0.99f;
                    return;
                }

                int total = Math.Max(1, value.Total);
                int processed = Math.Clamp(value.Processed, 0, total);

                // Keep bar under 100% until we explicitly Complete — avoids a stuck spinner at 1.0 Active.
                notification.Text = LocalisableString.Format(
                    EzSettingsStrings.LOCAL_PROFILE_COMPUTE_PROGRESS.ToString(),
                    processed,
                    total);
                notification.Progress = Math.Min(0.99f, (float)processed / total);
            }
        }

        private static void finishComputeNotification(
            Task computeTask,
            EzLocalProfileService localProfileService,
            ProgressNotification notification,
            INotificationOverlay notifications)
        {
            if (notification.State == ProgressNotificationState.Cancelled)
                return;

            if (computeTask.IsFaulted)
            {
                notification.State = ProgressNotificationState.Cancelled;
                notifications.Post(new SimpleErrorNotification { Text = EzSettingsStrings.LOCAL_PROFILE_COMPUTE_FAILED });
                return;
            }

            if (computeTask.IsCanceled)
            {
                notification.State = ProgressNotificationState.Cancelled;
                return;
            }

            localProfileService.ReloadFromDisk();
            notification.Progress = 1f;
            notification.CompletionText = EzSettingsStrings.LOCAL_PROFILE_COMPUTE_DONE;
            notification.State = ProgressNotificationState.Completed;
        }

        private void requestOnlinePull(
            EzLocalProfileOnlinePullService? onlinePullService,
            EzLocalProfileService? localProfileService,
            RulesetStore? rulesetStore,
            IDialogOverlay? dialogOverlay,
            INotificationOverlay? notifications)
        {
            if (onlinePullService == null || rulesetStore == null || dialogOverlay == null)
            {
                notifications?.Post(new SimpleErrorNotification { Text = EzSettingsStrings.LOCAL_PROFILE_ONLINE_PULL_FAILED });
                return;
            }

            if (onlinePullService.IsPulling.Value)
            {
                notifications?.Post(new SimpleNotification { Text = EzSettingsStrings.LOCAL_PROFILE_ONLINE_PULL_BUSY });
                return;
            }

            dialogOverlay.Push(new EzLocalProfileOnlinePullDialog(
                rulesetStore,
                onlinePullService.PeekPullOffset,
                request =>
                {
                    if (onlinePullService.IsPulling.Value)
                    {
                        notifications?.Post(new SimpleNotification { Text = EzSettingsStrings.LOCAL_PROFILE_ONLINE_PULL_BUSY });
                        return;
                    }

                    notifications?.Post(new SimpleNotification { Text = EzSettingsStrings.LOCAL_PROFILE_ONLINE_PULL_BUSY });

                    onlinePullService.PullAsync(request).ContinueWith(t => Schedule(() =>
                    {
                        if (t.IsFaulted)
                        {
                            notifications?.Post(new SimpleErrorNotification { Text = EzSettingsStrings.LOCAL_PROFILE_ONLINE_PULL_FAILED });
                            return;
                        }

                        if (t.IsCanceled)
                            return;

                        var result = t.GetResultSafely();

                        if (result.ErrorMessage == "need_online")
                        {
                            notifications?.Post(new SimpleErrorNotification { Text = EzSettingsStrings.LOCAL_PROFILE_ONLINE_PULL_NEED_ONLINE });
                            return;
                        }

                        if (result.ErrorMessage == "already_pulling")
                        {
                            notifications?.Post(new SimpleNotification { Text = EzSettingsStrings.LOCAL_PROFILE_ONLINE_PULL_BUSY });
                            return;
                        }

                        if (!string.IsNullOrEmpty(result.ErrorMessage) && result.ErrorMessage != "cancelled")
                        {
                            notifications?.Post(new SimpleErrorNotification { Text = EzSettingsStrings.LOCAL_PROFILE_ONLINE_PULL_FAILED });
                            return;
                        }

                        if (result.ErrorMessage == "cancelled")
                            return;

                        notifications?.Post(new SimpleNotification
                        {
                            Text = string.Format(
                                EzSettingsStrings.LOCAL_PROFILE_ONLINE_PULL_DONE.ToString(),
                                result.Candidates,
                                result.Imported,
                                result.AlreadyOwned,
                                result.NoReplay,
                                result.MissingBeatmap,
                                result.Failed,
                                result.StatsRecorded,
                                result.MapsDownloaded,
                                result.MapsAlreadyLocal,
                                result.CollectionAdds),
                        });

                        if (result.StatsRecorded > 0 && localProfileService is not null && !localProfileService.IsComputing.Value)
                            runCompute(localProfileService, localProfileService.GetPreviouslyIncludedUsernames(), replaceIncludedUsernames: false, notifications);
                    }));
                }));
        }

        internal static readonly LocalisableString EZ_EXPERIMENTAL_SECTION_HEADER = new EzLocalizationManager.EzLocalisableString(
            "实验性功能", "Experimental Features");

        internal static readonly LocalisableString EZ_SUB_FRAME_CORRECTION_ENABLED = new EzLocalizationManager.EzLocalisableString(
            "启用 Ez 子帧时序校正", "Enable Ez Sub-frame Timing Correction");

        internal static readonly LocalisableString EZ_SUB_FRAME_CORRECTION_ENABLED_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "子帧时序校正：利用前一帧的时钟值来补偿判断。"
            + "\n按键在上一次 FSC 时钟刷新和现在之间被按下；插值到实际按键时间。"
            + "\n可以理解为改用相对于上一帧的时间进行判定，而不是主时轴绝对时间。",
            "Sub-frame timing correction: compensate for judgment using previous frame's clock value."
            + "\nThe key was pressed between the last FSC clock update and now; interpolate to the actual press time."
            + "\nThis can be understood as using time relative to the previous frame for judgment, rather than the absolute time of the main timeline.");

        internal static readonly LocalisableString EZ_JUDGMENT_DIAG_ENABLED = new EzLocalizationManager.EzLocalisableString(
            "启用 Ez 判定诊断", "Enable Ez Judgment Diagnostics");

        internal static readonly LocalisableString EZ_JUDGMENT_DIAG_ENABLED_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "(研究功能)在游戏结束后，输出本局判定信息到.csv文件。"
            + "\n默认输出路径：桌面/EzDiag/",
            "(Testing feature) Output judgment information to a .csv file after the game ends."
            + "\nDefault output path: Desktop/EzDiag/");

        internal static readonly LocalisableString EZ_TIMING_TRACE_ENABLED = new EzLocalizationManager.EzLocalisableString(
            "启用 Ez 时序追踪", "Enable Ez Timing Trace");

        internal static readonly LocalisableString EZ_TIMING_TRACE_ENABLED_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "(研究功能)在游戏结束后，输出本局判定信息到.csv文件。"
            + "\n追踪按键输入与音频的时序关系, 用于检查打击音效的时序。"
            + "\n默认输出路径：桌面/EzDiag/",
            "(Testing feature) Output judgment information to a .csv file after the game ends."
            + "\nTrack the timing relationship between key input and audio, used to check the timing of hit sounds."
            + "\nDefault output path: Desktop/EzDiag/");

        internal static readonly LocalisableString INPUT_AUDIO_LATENCY_TRACKER = new EzLocalizationManager.EzLocalisableString(
            "输入音频延迟追踪器", "Input Audio Latency Tracker");

        internal static readonly LocalisableString INPUT_AUDIO_LATENCY_TRACKER_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "(测试功能)启用后可追踪按键输入与音频的延迟, 用于调试和优化打击音效的同步性。在游戏结束后会弹出一个统计窗口。更详细的内容可以查看runtime.log文件。"
            + "\n延迟检测管线：按键 → 检查打击并应用 → 应用判定结果 → 播放note音频",
            "(Testing feature) When enabled, it can track the latency between key input and audio, used for debugging and optimizing the synchronization of hit sound effects. "
            + "A statistics window will pop up after the game ends. More detailed information can be found in the runtime.log file."
            + "\nLatency detection pipeline: Key Press → Check Hit and Apply → Apply Hit Result → Play Note Audio");

        internal static readonly LocalisableString EZ_SCORE_RACE_SERVICE_ENABLED = new EzLocalizationManager.EzLocalisableString(
            "启用角逐/时间线全局服务", "Enable Score Race / Timeline Global Service");

        internal static readonly LocalisableString EZ_SCORE_RACE_SERVICE_ENABLED_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "关闭后不再进行选歌界面的本地成绩查询，以及进局时的 ghost 时间线构建。"
            + "\n角逐排行榜 / 分数对比 HUD 将不可用。"
            + "\n用于排查启动间全程帧率异常时，可跨多次冷启动做 A/B 对比。",
            "When disabled, skips local score queries on song select and ghost timeline builds when entering play."
            + "\nScore race / compare HUD components will not work."
            + "\nUse for A/B isolation of startup-wide FPS regressions across cold launches.");

        internal static readonly LocalisableString EZ_SCORE_RACE_FEED_MODE = new EzLocalizationManager.EzLocalisableString(
            "角逐时间线喂入模式", "Score Race Timeline Feed Mode");

        internal static readonly LocalisableString EZ_SCORE_RACE_FEED_MODE_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "BatchAllEvents：进局前阻塞直至 ghost timeline 预建完成（默认）。"
            + "\nStreamByClock：进局不阻塞；timeline 后台就绪后 HUD 再按时钟插值。"
            + "\n仅在启用角逐服务时生效。",
            "BatchAllEvents: block PlayerLoader until ghost timelines are prebuilt (default)."
            + "\nStreamByClock: do not block entering play; HUD interpolates once timelines arrive."
            + "\nOnly applies when the score-race service is enabled.");
    }
}

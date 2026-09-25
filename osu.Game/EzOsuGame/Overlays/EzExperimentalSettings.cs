// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
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
using osu.Game.EzOsuGame.Skills;
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
                          EzSkillStore? skillStore,
                          IDialogOverlay? dialogOverlay,
                          INotificationOverlay? notifications,
                          EzLocalProfileService? localProfileService,
                          EzLocalProfileOnlinePullService? onlinePullService,
                          RulesetStore? rulesetStore,
                          EzExternalRulesetManagerDialog? externalRulesetManager)
        {
            EzDataRebuildSettingsSection.AddTo(this, backgroundDataStoreProcessor, analysisWarmupProcessor, skillStore, dialogOverlay, notifications, localProfileService?.Store);

            Add(new SettingsButtonV2
            {
                Text = EzSettingsProfile.LOCAL_PROFILE_COMPUTE,
                TooltipText = EzSettingsProfile.LOCAL_PROFILE_COMPUTE_TOOLTIP,
                Keywords = new[] { "local", "profile", "stats", "kps", "个人", "本地", "统计", "成绩" },
                Action = () => requestComputeLocalProfile(localProfileService, dialogOverlay, notifications),
            });

            Add(new SettingsButtonV2
            {
                Text = EzSettingsProfile.LOCAL_PROFILE_ONLINE_PULL,
                TooltipText = EzSettingsProfile.LOCAL_PROFILE_ONLINE_PULL_TOOLTIP,
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
                    Caption = EZ_DIAG_SUITE_ENABLED,
                    HintText = EZ_DIAG_SUITE_ENABLED_TOOLTIP,
                })
                {
                    Keywords = new[] { "diagnostics", "probe", "csv", "诊断", "探针" }
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.EzDiagProbeJudgment),
                    Caption = EZ_DIAG_PROBE_JUDGMENT,
                    HintText = EZ_DIAG_PROBE_JUDGMENT_TOOLTIP,
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.EzDiagProbePress),
                    Caption = EZ_DIAG_PROBE_PRESS,
                    HintText = EZ_DIAG_PROBE_PRESS_TOOLTIP,
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.EzDiagProbeFrame),
                    Caption = EZ_DIAG_PROBE_FRAME,
                    HintText = EZ_DIAG_PROBE_FRAME_TOOLTIP,
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.EzDiagProbeJudgeHotPath),
                    Caption = EZ_DIAG_PROBE_JUDGE_HOT_PATH,
                    HintText = EZ_DIAG_PROBE_JUDGE_HOT_PATH_TOOLTIP,
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Current = ezConfig.GetBindable<bool>(Ez2Setting.EzDiagProbeTimingTrace),
                    Caption = EZ_DIAG_PROBE_TIMING_TRACE,
                    HintText = EZ_DIAG_PROBE_TIMING_TRACE_TOOLTIP,
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
                notifications?.Post(new SimpleErrorNotification { Text = EzSettingsProfile.LOCAL_PROFILE_COMPUTE_FAILED });
                return;
            }

            if (localProfileService.IsComputing.Value)
            {
                notifications?.Post(new SimpleNotification { Text = EzSettingsProfile.LOCAL_PROFILE_COMPUTE_STARTED });
                return;
            }

            var counts = localProfileService.ScanUsernameCounts();

            if (counts.Count == 0)
            {
                if (!localProfileService.HasOnlineScoreContributions())
                {
                    notifications?.Post(new SimpleNotification { Text = EzSettingsProfile.LOCAL_PROFILE_NO_SCORES });
                    return;
                }

                runCompute(localProfileService, localProfileService.GetPreviouslyIncludedUsernames(), clearRebuild: false, notifications);
                return;
            }

            dialogOverlay.Push(new EzLocalProfileImportDialog(
                counts,
                localProfileService.GetPreviouslyIncludedUsernames(),
                (selected, clearRebuild) =>
                {
                    if (selected.Count == 0 && !localProfileService.HasOnlineScoreContributions()
                                            && (clearRebuild || localProfileService.GetPreviouslyIncludedUsernames().Count == 0))
                    {
                        notifications?.Post(new SimpleNotification { Text = EzSettingsProfile.LOCAL_PROFILE_NONE_SELECTED });
                        return;
                    }

                    runCompute(localProfileService, selected, clearRebuild, notifications);
                },
                selected => requestDeleteLocalProfile(localProfileService, dialogOverlay, notifications, selected)));
        }

        /// <summary>
        /// Confirm then run the exclusion. The import dialog has already closed by the time this is invoked
        /// (its button hides it), so cancelling the confirmation means reopening the dialog.
        /// </summary>
        private void requestDeleteLocalProfile(
            EzLocalProfileService localProfileService,
            IDialogOverlay dialogOverlay,
            INotificationOverlay? notifications,
            IReadOnlyList<string> selected)
        {
            if (selected.Count == 0)
                return;

            dialogOverlay.Push(new EzLocalProfileDeleteConfirmDialog(selected,
                () =>
                {
                    notifications?.Post(new SimpleNotification { Text = EzSettingsProfile.LOCAL_PROFILE_DELETE_BUSY });

                    localProfileService.ExcludeUsernamesAsync(selected).ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                        {
                            notifications?.Post(new SimpleErrorNotification { Text = EzSettingsProfile.LOCAL_PROFILE_DELETE_FAILED });
                            return;
                        }

                        if (t.IsCanceled)
                            return;

                        var deleted = t.GetResultSafely();

                        notifications?.Post(new SimpleNotification
                        {
                            Text = deleted.Count > 0
                                ? LocalisableString.Format(EzSettingsProfile.LOCAL_PROFILE_DELETE_DONE.ToString(), string.Join(", ", deleted))
                                : EzSettingsProfile.LOCAL_PROFILE_DELETE_NONE,
                        });
                    });
                }));
        }

        private void runCompute(
            EzLocalProfileService localProfileService,
            IReadOnlyCollection<string> selected,
            bool clearRebuild,
            INotificationOverlay? notifications)
        {
            if (notifications == null)
            {
                // Clear & rebuild also prunes unchecked names; the incremental backfill leaves them alone.
                localProfileService.ComputeAsync(selected, clearRebuild, clearRebuild).ContinueWith(t => Schedule(() =>
                {
                    if (!t.IsFaulted && !t.IsCanceled)
                        localProfileService.ReloadFromDisk();
                }));
                return;
            }

            var notification = EzLocalProfileComputeNotification.Create();

            notifications.Post(notification);

            var progress = new EzLocalProfileComputeNotification.Forwarder(notification);

            localProfileService.ComputeAsync(selected, clearRebuild, clearRebuild, progress, notification.CancellationToken)
                               .ContinueWith(t => EzLocalProfileComputeNotification.Finish(t, localProfileService, notification, notifications));
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
                notifications?.Post(new SimpleErrorNotification { Text = EzSettingsProfile.LOCAL_PROFILE_ONLINE_PULL_FAILED });
                return;
            }

            if (onlinePullService.IsPulling.Value)
            {
                notifications?.Post(new SimpleNotification { Text = EzSettingsProfile.LOCAL_PROFILE_ONLINE_PULL_BUSY });
                return;
            }

            dialogOverlay.Push(new EzLocalProfileOnlinePullDialog(
                rulesetStore,
                onlinePullService.PeekPullOffset,
                request =>
                {
                    if (onlinePullService.IsPulling.Value)
                    {
                        notifications?.Post(new SimpleNotification { Text = EzSettingsProfile.LOCAL_PROFILE_ONLINE_PULL_BUSY });
                        return;
                    }

                    notifications?.Post(new SimpleNotification { Text = EzSettingsProfile.LOCAL_PROFILE_ONLINE_PULL_BUSY });

                    onlinePullService.PullAsync(request).ContinueWith(t => Schedule(() =>
                    {
                        if (t.IsFaulted)
                        {
                            notifications?.Post(new SimpleErrorNotification { Text = EzSettingsProfile.LOCAL_PROFILE_ONLINE_PULL_FAILED });
                            return;
                        }

                        if (t.IsCanceled)
                            return;

                        var result = t.GetResultSafely();

                        if (result.ErrorMessage == "need_online")
                        {
                            notifications?.Post(new SimpleErrorNotification { Text = EzSettingsProfile.LOCAL_PROFILE_ONLINE_PULL_NEED_ONLINE });
                            return;
                        }

                        if (result.ErrorMessage == "already_pulling")
                        {
                            notifications?.Post(new SimpleNotification { Text = EzSettingsProfile.LOCAL_PROFILE_ONLINE_PULL_BUSY });
                            return;
                        }

                        if (!string.IsNullOrEmpty(result.ErrorMessage) && result.ErrorMessage != "cancelled")
                        {
                            notifications?.Post(new SimpleErrorNotification { Text = EzSettingsProfile.LOCAL_PROFILE_ONLINE_PULL_FAILED });
                            return;
                        }

                        if (result.ErrorMessage == "cancelled")
                            return;

                        notifications?.Post(new SimpleNotification
                        {
                            Text = string.Format(
                                EzSettingsProfile.LOCAL_PROFILE_ONLINE_PULL_DONE.ToString(),
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
                            runCompute(localProfileService, localProfileService.GetPreviouslyIncludedUsernames(), clearRebuild: false, notifications);
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

        internal static readonly LocalisableString EZ_DIAG_SUITE_ENABLED = new EzLocalizationManager.EzLocalisableString(
            "启用 Ez 诊断套件", "Enable Ez Diagnostics Suite");

        internal static readonly LocalisableString EZ_DIAG_SUITE_ENABLED_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "(研究功能)总开关。开启后由下面各子项决定跑哪些探针，每局结束把本局数据写成 CSV。"
            + "\n**重启游戏后生效**（关闭时为零开销，热路径上不留任何探针逻辑）。"
            + "\n输出目录：仓库内 diagnostics/（找不到仓库时退回桌面 EzDiag/）。",
            "(Testing feature) Master switch. The items below choose which probes run; each enabled probe writes a CSV after every play."
            + "\nTakes effect after restarting the game (zero cost while off)."
            + "\nOutput: diagnostics/ inside the repository (falls back to Desktop/EzDiag/).");

        internal static readonly LocalisableString EZ_DIAG_PROBE_JUDGMENT = new EzLocalizationManager.EzLocalisableString(
            "诊断：判定探针", "Diagnostics: judgment probe");

        internal static readonly LocalisableString EZ_DIAG_PROBE_JUDGMENT_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "judgment_*.csv：判定的时钟漂移，以及 InputToJudgeMs（按键 → 判定检查的 wall 耗时）。"
            + "\n需先开启上面的「Ez 诊断套件」。",
            "judgment_*.csv: clock drift per judgment plus InputToJudgeMs (key → judgment-check wall time)."
            + "\nRequires the diagnostics suite above.");

        internal static readonly LocalisableString EZ_DIAG_PROBE_PRESS = new EzLocalizationManager.EzLocalisableString(
            "诊断：按键延迟探针", "Diagnostics: press-latency probe");

        internal static readonly LocalisableString EZ_DIAG_PROBE_PRESS_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "presslatency_*.csv：一次按键在 PreColumn（入队 → 本列入口）与 Column（本列全程）两段各花多少 wall 时间；空按同样采样。"
            + "\n需先开启上面的「Ez 诊断套件」。",
            "presslatency_*.csv: wall time a press spends in PreColumn (queued → column entry) and Column (the whole column pass); empty presses are sampled too."
            + "\nRequires the diagnostics suite above.");

        internal static readonly LocalisableString EZ_DIAG_PROBE_FRAME = new EzLocalizationManager.EzLocalisableString(
            "诊断：帧卡顿探针（最贵）", "Diagnostics: frame-stall probe (most expensive)");

        internal static readonly LocalisableString EZ_DIAG_PROBE_FRAME_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "framestall_*.csv + .summary.txt：每帧耗时直方图，分「含按键的帧」与「不含按键的帧」两组。"
            + "\n套件里开销最大的一个（每帧两次时钟读取 + GC 读数）；与按键探针同时开启时，这些开销会落进同帧按键的 PreColumnMs。"
            + "\n需先开启上面的「Ez 诊断套件」。",
            "framestall_*.csv + .summary.txt: per-frame duration histograms, split into frames with and without presses."
            + "\nThe heaviest probe in the suite (two clock reads + GC reads per frame); when run alongside the press probe, that cost lands in the same frame's PreColumnMs."
            + "\nRequires the diagnostics suite above.");

        internal static readonly LocalisableString EZ_DIAG_PROBE_JUDGE_HOT_PATH = new EzLocalizationManager.EzLocalisableString(
            "诊断：判定热路径计数", "Diagnostics: judgment hot-path counters");

        internal static readonly LocalisableString EZ_DIAG_PROBE_JUDGE_HOT_PATH_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "统计判定 / 按键 / LN 扫描等热路径的调用次数，只写进日志与探针摘要，不落 CSV。"
            + "\n需先开启上面的「Ez 诊断套件」。",
            "Counts calls of the judgment / press / LN-scan hot paths; goes to the log and the probe summary, no CSV."
            + "\nRequires the diagnostics suite above.");

        internal static readonly LocalisableString EZ_DIAG_PROBE_TIMING_TRACE = new EzLocalizationManager.EzLocalisableString(
            "诊断：时序追踪", "Diagnostics: timing trace");

        internal static readonly LocalisableString EZ_DIAG_PROBE_TIMING_TRACE_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "trace_*.csv：玩法生命周期关键节点的时序（结算卡住 / HasCompleted 翻转等）。"
            + "\n需先开启上面的「Ez 诊断套件」。",
            "trace_*.csv: timing of gameplay lifecycle milestones (stuck results, HasCompleted flips)."
            + "\nRequires the diagnostics suite above.");

        internal static readonly LocalisableString INPUT_AUDIO_LATENCY_TRACKER = new EzLocalizationManager.EzLocalisableString(
            "输入音频延迟追踪器", "Input Audio Latency Tracker");

        internal static readonly LocalisableString INPUT_AUDIO_LATENCY_TRACKER_TOOLTIP = new EzLocalizationManager.EzLocalisableString(
            "(测试功能) 追踪按键 → Sample.Play → 输出路径 PCM 过阈值的闭环延迟（不含判定）。"
            + "Default / Shared / Exclusive / ASIO 均可用；局末弹统计，明细见 ez_runtime。"
            + "\nIn→Play：软件段；Play→Acou / In→Acou：混音进入当前输出驱动时过阈值（非麦克风）。"
            + "\n判定耗时请开「Ez 判定诊断」CSV。阈值：AcousticRmsThreshold（可热重载）。",
            "(Testing feature) Tracks key → Sample.Play → output-path PCM threshold latency (not judgment). "
            + "Works on Default / Shared / Exclusive / ASIO; summary after play, details in ez_runtime."
            + "\nIn→Play: software; Play→Acou / In→Acou: mixer PCM crosses threshold as it enters the active output driver (not a mic)."
            + "\nFor key→judgment latency, enable Ez Judgment Diagnostics CSV. "
            + "Threshold: AcousticRmsThreshold (hot-reloadable).");

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

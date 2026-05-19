// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.EzOsuGame.Overlays;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets.BMS.Beatmaps;
using osu.Game.Rulesets.BMS.Configuration;
using osu.Game.Rulesets.BMS.Scoring.Lamp;
using osu.Game.Rulesets.BMS.Scoring.Lamp.Persistence;
using osu.Game.Rulesets.BMS.UI.BmsSongSelect.Analytics;
using osu.Game.Screens.Footer;
using osu.Game.Screens.Select;

namespace osu.Game.Rulesets.BMS.UI.SongSelect
{
    /// <summary>
    /// BMS 选歌屏：派生自 osu.Game 的 <see cref="SoloSongSelect"/>，复用全部官方 carousel/wedge/details/filter UI。
    ///
    /// 关键不同：
    /// 1. 进入时强制 <c>Ruleset.Value</c> = BMS，借助 <see cref="BeatmapInfoExtensions.AllowGameplayWithRuleset"/>
    ///    对 <c>bms-ext:set:</c> 前缀的特殊处理，让 carousel 只显示 BMS 谱面（其他 ruleset 的曲在 BMS 下不显示）。
    /// 2. 进入时刷一次 <see cref="BMSOsuLibrarySynchronizer.Synchronize"/>，把外部 BMS 文件夹 sync 到 osu Realm，
    ///    让官方 <c>BeatmapStore</c> / <c>BeatmapCarousel</c> 能查询到。
    /// 3. 重写 <see cref="OnStart"/>：不走 <c>SoloSongSelect</c> 的标准 <c>PlayerLoader</c>，从选中 BeatmapInfo 反查
    ///    <see cref="BMSSourceReference"/> → 构造 <see cref="BMSWorkingBeatmap"/> → push <see cref="BMSPlayerLoader"/>。
    /// 4. <c>ControlGlobalMusic</c> 关闭，自接 <see cref="BmsChartPreviewPlayer"/>：用键音重建预览，
    ///    BMS 资源不进 Realm 文件存储所以走 <see cref="BMSWorkingBeatmap"/> 自带的外部目录解析。
    /// 5. footer 多一颗"刷新曲库"，触发 <c>BMSBeatmapManager.ScanLibraryAsync</c> + Synchronize。
    /// </summary>
    public partial class BmsSoloSongSelect : SoloSongSelect
    {
        private BMSBeatmapManager beatmapManager = null!;
        private BmsChartPreviewPlayer previewPlayer = null!;
        private Bindable<string> libraryPathsBindable = null!;
        private Bindable<string> legacyRootPathBindable = null!;
        private RulesetInfo bmsRulesetInfo = null!;

        // Lamp template wiring: scheme picks lamp-from-context rules, store keeps the best lamp per
        // beatmap, and the IPanelAccentColourProvider bridge feeds those colours into osu.Game's
        // panel accent strip. All three are [Cached] below so child UI (PanelBeatmap and any future
        // lamp HUD widgets) can [Resolved] them. Swap schemes here later — nothing else needs to change.
        // If osu.Game panels omit IPanelAccentColourProvider resolution, they use [Resolved(canBeNull: true)]
        // and fall back to star colours; BMS gameplay and this screen still run — lamp strip is optional UI only.
        [Cached(typeof(IBmsLampScheme))]
        private readonly IBmsLampScheme lampScheme = new BeatorajaLampScheme();

        [Cached]
        private readonly BmsLampStore lampStore;

        [Cached(typeof(IPanelAccentColourProvider))]
        private readonly BmsLampAccentColourProvider lampAccentColourProvider;

        [Cached(typeof(IPanelEzAnalysisProvider))]
        private readonly BmsPanelEzAnalysisProvider panelEzAnalysisProvider;

        // Backed by a private SQLite file under the BMS cache dir. Attached in BDL once we have
        // access to the resolved Storage. osu.Game's Realm is intentionally untouched — that
        // would force a schema-version bump and break clients without the BMS ruleset installed.
        private BmsLampSqliteRepository? lampRepository;
        private BmsAnalyticsSqliteRepository? analyticsRepository;

        // Snapshot of the global MusicController state at entry; restored on exit so the main-menu music
        // resumes naturally after the user backs out of song select.
        private bool capturedMusicControllerState;
        private bool savedAllowTrackControl;

        [Resolved]
        private AudioManager audioManager { get; set; } = null!;

        [Resolved]
        private IRenderer renderer { get; set; } = null!;

        [Resolved]
        private Storage storage { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private MusicController? musicController { get; set; }

        [Resolved(canBeNull: true)]
        private INotificationOverlay? notifications { get; set; }

        [Resolved]
        private BeatmapManager beatmaps { get; set; } = null!;

        public BmsSoloSongSelect()
        {
            lampStore = new BmsLampStore(lampScheme);
            lampAccentColourProvider = new BmsLampAccentColourProvider(lampStore);
            panelEzAnalysisProvider = new BmsPanelEzAnalysisProvider();

            // BMS audio is not stored in osu's RealmFileStore. Disable SongSelect's standard MusicController
            // preview loop and drive previews through BmsChartPreviewPlayer instead, which knows how to read
            // BMS folders + key-sound samples directly.
            ControlGlobalMusic = false;
        }

        [BackgroundDependencyLoader]
        private void load(IRulesetConfigCache configCache)
        {
            var bmsRuleset = new BMSRuleset();
            bmsRulesetInfo = bmsRuleset.RulesetInfo;
            var config = configCache.GetConfigFor(bmsRuleset);

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

            beatmapManager = BMSBeatmapManager.GetShared(storage);
            syncConfiguredPaths();

            panelEzAnalysisProvider.BindBeatmapManager(beatmapManager);

            // Lamp DB lives under EzBMS next to the chart index. Repository init is internally try/catch so
            // a corrupt sqlite file degrades to "no lamps" rather than blocking song-select from opening.
            string lampDbPath = BmsStoragePaths.GetLampDatabasePath(storage);
            lampRepository = new BmsLampSqliteRepository(lampDbPath);
            lampStore.AttachRepository(lampRepository);

            string analyticsDbPath = BmsStoragePaths.GetAnalyticsDatabasePath(storage);
            analyticsRepository = new BmsAnalyticsSqliteRepository(analyticsDbPath);
            panelEzAnalysisProvider.AttachRepository(analyticsRepository);

            // BDL runs on the load thread, NOT the update thread. We deliberately don't mutate any global
            // Bindable here (Ruleset / Beatmap / etc) — doing so would synchronously invoke ValueChanged
            // subscribers whose update paths may transform Loaded drawables, throwing
            // InvalidThreadForMutationException. All such mutations are deferred to LoadComplete.
            AddInternal(previewPlayer = new BmsChartPreviewPlayer
            {
                EnabledBindable = { Value = true },
            });
        }

        protected override void LoadComplete()
        {
            // Ruleset must be BMS before base.LoadComplete builds the carousel; bms-ext sets return false
            // from AllowGameplayWithRuleset for non-BMS rulesets and would be filtered out entirely.
            Ruleset.Value = bmsRulesetInfo;

            syncConfiguredPaths();

            // Sync before base.LoadComplete so the carousel binds to up-to-date Realm rows (stable beatmap IDs).
            if (beatmapManager.NeedsRealmSynchronization && beatmapManager.HasIndexedCharts)
            {
                try
                {
                    BMSOsuLibrarySynchronizer.Synchronize(beatmapManager, storage, realm, bmsRulesetInfo);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "[BMS] initial library sync failed");
                }
            }

            // BMS does not use EzKeyModeSelector; ignore any residual CircleSize range from FilterControl.
            FilterControl.ApplyRequiredCriteria = criteria => criteria.CircleSize = default;

            base.LoadComplete();

            ensureBeatmapInfoExistsInRealm();
            Scheduler.AddOnce(requestCarouselCriteriaRefresh);

            Beatmap.BindValueChanged(_ => updatePreview(), true);
            Ruleset.BindValueChanged(onRulesetChanged);
        }

        /// <summary>
        /// Rebind the global working beatmap to a Realm-backed instance when the current selection's
        /// <see cref="BeatmapInfo.ID"/> is missing (stale carousel item after catalog re-sync).
        /// </summary>
        private void ensureBeatmapInfoExistsInRealm()
        {
            var info = Beatmap.Value?.BeatmapInfo;

            if (info == null || info.Ruleset.ShortName != "bms")
                return;

            bool exists = realm.Run(r => r.Find<BeatmapInfo>(info.ID) != null);

            if (exists)
                return;

            if (beatmapManager.TryGetSourceReference(info.ID, out BMSSourceReference sourceRef))
            {
                var replacement = realm.Run(r => r.All<BeatmapInfo>()
                                                  .FirstOrDefault(b => b.BeatmapSet != null
                                                                       && b.BeatmapSet.Hash.StartsWith("bms-ext:set:", StringComparison.Ordinal)
                                                                       && string.Equals(b.Path, Path.GetFileName(sourceRef.ChartPath), StringComparison.OrdinalIgnoreCase)));

                if (replacement != null)
                    Beatmap.Value = beatmaps.GetWorkingBeatmap(replacement, true);
            }
        }

        public override void OnEntering(ScreenTransitionEvent e)
        {
            base.OnEntering(e);
            suspendGlobalMusicController();

            if (!beatmapManager.HasIndexedCharts)
            {
                notifications?.Post(new SimpleNotification
                {
                    Text = "BMS 曲库索引为空，请在设置中添加路径或点「刷新曲库」扫描",
                    Icon = FontAwesome.Solid.InfoCircle,
                });
            }
        }

        public override void OnResuming(ScreenTransitionEvent e)
        {
            // ManiaCompatibility gameplay pushes ManiaConvertedWorkingBeatmap into the global Beatmap bindable and
            // switches Ruleset to mania. Song select HUD (EzHUDRadarPanel, difficulty cache, …) may call
            // GetPlayableBeatmap from background threads on that shared wrapper → concurrent ApplyDefaults on the
            // same ManiaBeatmap → InvalidOperationException ("Collection was modified") or lifetime manager NRE.
            // Restore Realm-backed WorkingBeatmap + BMS ruleset before base.OnResuming runs ensureGlobalBeatmapValid /
            // external radar tasks.
            restoreBmsSelectionStateAfterGameplay();

            panelEzAnalysisProvider.ReloadFromRepository();

            base.OnResuming(e);
            suspendGlobalMusicController();
            updatePreview();
        }

        /// <summary>
        /// After returning from <see cref="BMSPlayerLoader"/> / Player, global Beatmap may still reference
        /// <see cref="ManiaConvertedWorkingBeatmap"/> or <see cref="BMSWorkingBeatmap"/>; Ruleset may still be mania.
        /// Reset so SongSelect and overlays always see the standard library working beatmap for the selected chart.
        /// </summary>
        private void restoreBmsSelectionStateAfterGameplay()
        {
            Ruleset.Value = bmsRulesetInfo;

            var info = Beatmap.Value?.BeatmapInfo;
            if (info == null)
                return;

            Beatmap.Value = beatmaps.GetWorkingBeatmap(info, true);
        }

        public override void OnSuspending(ScreenTransitionEvent e)
        {
            previewPlayer.StopPreview();
            base.OnSuspending(e);
        }

        public override bool OnExiting(ScreenExitEvent e)
        {
            previewPlayer.StopPreview();
            restoreGlobalMusicController();
            return base.OnExiting(e);
        }

        protected override void OnStart()
        {
            var beatmapInfo = Beatmap.Value?.BeatmapInfo;

            if (beatmapInfo == null || beatmapInfo.Ruleset.ShortName != "bms")
            {
                notifications?.Post(new SimpleNotification
                {
                    Text = "请先选择一个 BMS 谱面",
                    Icon = FontAwesome.Solid.ExclamationTriangle,
                });
                return;
            }

            previewPlayer.StopPreview();
            BmsSongSelectPlayHelper.TryLaunchFromBeatmapInfo(this, beatmapInfo, beatmapManager, audioManager, renderer, musicController, notifications);
        }

        public override IReadOnlyList<ScreenFooterButton> CreateFooterButtons()
        {
            // Strip osu/Ez2-only buttons that don't apply to BMS:
            // - FooterButtonEzExport: exports beatmaps as .osz, but BMS charts aren't packaged into osu's Realm files.
            // - FooterButtonEzPreView: opens an osu-format chart preview overlay; BMS uses BmsChartPreviewPlayer instead.
            var buttons = base.CreateFooterButtons()
                              .Where(button => button is not FooterButtonEzExport && button is not FooterButtonEzPreView)
                              .ToList();

            buttons.Add(new ScreenFooterButton
            {
                Text = "刷新曲库",
                Icon = FontAwesome.Solid.SyncAlt,
                Action = refreshLibrary,
            });

            return buttons;
        }

        private void onRulesetChanged(ValueChangedEvent<RulesetInfo> e)
        {
            if (!this.IsCurrentScreen())
                return;

            if (string.Equals(e.NewValue.ShortName, "bms", StringComparison.OrdinalIgnoreCase))
                return;

            // External ruleset switch (e.g. via toolbar): exit out of BMS selection cleanly.
            this.Exit();
        }

        private void updatePreview()
        {
            previewPlayer.StopPreview();

            if (!this.IsCurrentScreen())
                return;

            if (BmsAnalyticsScanService.IsRunning)
                return;

            var info = Beatmap.Value?.BeatmapInfo;

            if (info == null || info.Ruleset.ShortName != "bms")
                return;

            if (!tryResolveBmsSource(info, out string chartPath, out var chartCache))
                return;

            try
            {
                var bmsWorking = new BMSWorkingBeatmap(chartPath, audioManager, renderer, chartCache);

                int previewTime = chartCache?.PreviewTime ?? -1;
                previewPlayer.OverridePreviewStartTime = previewTime >= 0 ? previewTime : 0;
                previewPlayer.StartPreview(bmsWorking);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[BMS] preview start failed");
            }
        }

        private bool tryResolveBmsSource(BeatmapInfo info, out string chartPath, out BMSChartCache? chartCache) =>
            BmsSongSelectPlayHelper.TryResolveSource(beatmapManager, info, out chartPath, out chartCache);

        private void syncConfiguredPaths() => beatmapManager.SetRootPaths(BMSRulesetConfigManager.ParseLibraryPaths(libraryPathsBindable.Value, legacyRootPathBindable.Value));

        /// <summary>
        /// Re-run carousel filtering after Realm catalog changes without adding APIs to <see cref="FilterControl"/>.
        /// </summary>
        private void requestCarouselCriteriaRefresh()
        {
            const string sentinel = "\u200b";
            FilterControl.Search(sentinel);
            FilterControl.Search(string.Empty);
        }

        private void refreshLibrary()
        {
            syncConfiguredPaths();

            if (beatmapManager.RootPaths.Count == 0)
            {
                notifications?.Post(new SimpleNotification
                {
                    Text = "请先在 BMS 设置中添加曲库路径",
                    Icon = FontAwesome.Solid.ExclamationTriangle,
                });
                return;
            }

            var notification = new ProgressNotification
            {
                Text = "正在扫描 BMS 曲库...",
                Progress = 0,
            };

            notifications?.Post(notification);

            void onScanProgress(ValueChangedEvent<double> e) => Schedule(() => notification.Progress = (float)BmsLibraryImportPipeline.MapScanProgress(e.NewValue));

            void onScanStatus(ValueChangedEvent<string> e) => Schedule(() => notification.Text = e.NewValue);

            beatmapManager.ScanProgress.BindValueChanged(onScanProgress, true);
            beatmapManager.StatusMessage.BindValueChanged(onScanStatus, true);

            _ = Task.Run(async () =>
            {
                try
                {
                    await BmsLibraryImportPipeline.RunAsync(
                        beatmapManager,
                        storage,
                        realm,
                        bmsRulesetInfo,
                        beatmapManager.RootPaths,
                        p => Schedule(() =>
                        {
                            notification.Progress = (float)p.Progress;
                            notification.Text = p.StatusMessage;
                        })).ConfigureAwait(false);

                    Schedule(() =>
                    {
                        ensureBeatmapInfoExistsInRealm();
                        requestCarouselCriteriaRefresh();
                        notification.Progress = 1f;
                        notification.State = ProgressNotificationState.Completed;
                    });
                }
                catch (Exception ex)
                {
                    Schedule(() =>
                    {
                        Logger.Error(ex, "[BMS] library import failed");
                        notification.State = ProgressNotificationState.Cancelled;
                        notifications?.Post(new SimpleNotification
                        {
                            Text = $"刷新失败：{ex.Message}",
                            Icon = FontAwesome.Solid.ExclamationTriangle,
                        });
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

        private void suspendGlobalMusicController()
        {
            if (musicController == null)
                return;

            if (!capturedMusicControllerState)
            {
                savedAllowTrackControl = musicController.AllowTrackControl.Value;
                capturedMusicControllerState = true;
            }

            // Stop without flipping UserPauseRequested — we want main-menu auto-resume after exit.
            musicController.Stop();
            musicController.AllowTrackControl.Value = false;
        }

        private void restoreGlobalMusicController()
        {
            if (musicController == null || !capturedMusicControllerState)
                return;

            musicController.AllowTrackControl.Value = savedAllowTrackControl;
            capturedMusicControllerState = false;
        }
    }
}

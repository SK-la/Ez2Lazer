// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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

        // Backed by a private SQLite file under the BMS cache dir. Attached in BDL once we have
        // access to the resolved Storage. osu.Game's Realm is intentionally untouched — that
        // would force a schema-version bump and break clients without the BMS ruleset installed.
        private BmsLampSqliteRepository? lampRepository;

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

            // Lamp DB lives under EzBMS next to the chart index. Repository init is internally try/catch so
            // a corrupt sqlite file degrades to "no lamps" rather than blocking song-select from opening.
            string lampDbPath = BmsStoragePaths.GetLampDatabasePath(storage);
            lampRepository = new BmsLampSqliteRepository(lampDbPath);
            lampStore.AttachRepository(lampRepository);

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
            // Sync before base.LoadComplete so the carousel binds to up-to-date Realm rows (stable beatmap IDs).
            // Running sync after base would leave FooterButtonOptions / carousel on stale detached BeatmapInfo
            // whose IDs were removed during re-import.
            if (beatmapManager.NeedsRealmSynchronization)
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

            base.LoadComplete();

            // Lock ruleset to BMS so carousel filtering scopes to BMS panels via AllowGameplayWithRuleset's
            // bms-ext: short-circuit. Must happen on the update thread because subscribers (FilterControl,
            // BeatmapDifficultyCache, …) animate UI synchronously off this change.
            Ruleset.Value = bmsRulesetInfo;

            ensureBeatmapInfoExistsInRealm();

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
            // Mirror the relevant tail of SoloSongSelect.OnStart but route into BMSPlayerLoader rather than
            // PlayerLoader(SoloPlayer). We deliberately skip mods snapshot/restore here — BMS is not changing
            // mods inside song select like SoloSongSelect does for autoplay.
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

            if (!tryResolveBmsSource(beatmapInfo, out string chartPath, out var chartCache))
            {
                notifications?.Post(new SimpleNotification
                {
                    Text = "未能定位 BMS 源文件，请刷新曲库",
                    Icon = FontAwesome.Solid.ExclamationTriangle,
                });
                return;
            }

            try
            {
                previewPlayer.StopPreview();

                // Hand the realm-managed BeatmapInfo (detached copy keeps the same ID) to BMSWorkingBeatmap so the
                // resulting WorkingBeatmap — including the ManiaConvertedWorkingBeatmap wrapper used in compatibility
                // mode — exposes a BeatmapInfo whose ID can be looked up via realm.Find. Without this override
                // BMSWorkingBeatmap synthesises a fresh Guid that's not in Realm, breaking consumers like
                // FooterButtonOptions.beatmapChanged() which call realm.Find(...).ToLive(...) and would NRE on null.
                var workingBeatmapInfo = beatmapInfo.Detach();
                var workingBeatmap = new BMSWorkingBeatmap(chartPath, audioManager, renderer, chartCache, workingBeatmapInfo);

                Logger.Log(
                    $"[BMS] StartGame chart resolve: title={chartCache?.Title}, file={chartCache?.FileName}, md5={beatmapInfo.MD5Hash}, path={chartPath}",
                    LoggingTarget.Runtime, LogLevel.Debug);

                // Swap the global Beatmap to our BMSWorkingBeatmap *before* pushing BMSPlayerLoader.
                // SoloSongSelect's SelectAndRun already set Beatmap.Value to a Realm-backed BeatmapManagerWorkingBeatmap,
                // whose GetBeatmapTrack() resolves the chart's audio file from the BMS folder and returns a real Track.
                // During the ~600ms BMSPlayerLoader prep phase that real track was being picked up by background screens
                // / MusicController, producing the "fixed audio overlay" the user heard at gameplay start. BMSWorkingBeatmap.GetBeatmapTrack()
                // returns a virtual silent track, so once Beatmap.Value points at it nothing else can play through.
                Beatmap.Value = workingBeatmap;
                musicController?.Stop();

                this.Push(new BMSPlayerLoader(workingBeatmap));
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to launch BMS gameplay");
                notifications?.Post(new SimpleNotification
                {
                    Text = $"加载谱面失败：{ex.Message}",
                    Icon = FontAwesome.Solid.ExclamationTriangle,
                });
            }
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

        private bool tryResolveBmsSource(BeatmapInfo info, out string chartPath, out BMSChartCache? chartCache)
        {
            chartPath = string.Empty;
            chartCache = null;

            // ID-based lookup is the cleanest path: BuildVirtualBeatmapCatalog uses deterministic guids.
            if (beatmapManager.TryGetSourceReference(info.ID, out BMSSourceReference byId))
            {
                chartPath = byId.ChartPath;
            }
            else if (!string.IsNullOrEmpty(info.MD5Hash) && beatmapManager.TryGetSourceReferenceByHash(info.MD5Hash, out BMSSourceReference byHash))
            {
                chartPath = byHash.ChartPath;
            }
            else
            {
                return false;
            }

            // chartCache is optional but lets BMSWorkingBeatmap skip a parse round to learn key count etc.
            if (beatmapManager.LibraryCache != null)
            {
                chartCache = beatmapManager.LibraryCache.Songs
                                           .SelectMany(s => s.Charts)
                                           .FirstOrDefault(c => string.Equals(c.Md5Hash, info.MD5Hash, StringComparison.OrdinalIgnoreCase));
            }

            return !string.IsNullOrEmpty(chartPath);
        }

        private void syncConfiguredPaths() =>
            beatmapManager.SetRootPaths(BMSRulesetConfigManager.ParseLibraryPaths(libraryPathsBindable.Value, legacyRootPathBindable.Value));

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

            void onProgress(ValueChangedEvent<double> e) => Schedule(() => notification.Progress = (float)e.NewValue);
            void onStatus(ValueChangedEvent<string> e) => Schedule(() => notification.Text = e.NewValue);

            beatmapManager.ScanProgress.BindValueChanged(onProgress, true);
            beatmapManager.StatusMessage.BindValueChanged(onStatus, true);

            _ = Task.Run(async () =>
            {
                try
                {
                    await beatmapManager.ScanLibraryAsync(beatmapManager.RootPaths).ConfigureAwait(false);

                    Schedule(() =>
                    {
                        try
                        {
                            BMSOsuLibrarySynchronizer.Synchronize(beatmapManager, storage, realm, new BMSRuleset().RulesetInfo);
                            ensureBeatmapInfoExistsInRealm();
                            notification.Progress = 1f;
                            notification.State = ProgressNotificationState.Completed;
                            // BeatmapStore subscribes to Realm and pushes new sets into the carousel automatically.
                        }
                        catch (Exception ex)
                        {
                            Logger.Error(ex, "[BMS] synchronize failed after scan");
                            notification.State = ProgressNotificationState.Cancelled;
                            notifications?.Post(new SimpleNotification
                            {
                                Text = $"同步失败：{ex.Message}",
                                Icon = FontAwesome.Solid.ExclamationTriangle,
                            });
                        }
                        finally
                        {
                            beatmapManager.ScanProgress.ValueChanged -= onProgress;
                            beatmapManager.StatusMessage.ValueChanged -= onStatus;
                        }
                    });
                }
                catch (Exception ex)
                {
                    Schedule(() =>
                    {
                        Logger.Error(ex, "[BMS] library scan failed");
                        notification.State = ProgressNotificationState.Cancelled;
                        beatmapManager.ScanProgress.ValueChanged -= onProgress;
                        beatmapManager.StatusMessage.ValueChanged -= onStatus;
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

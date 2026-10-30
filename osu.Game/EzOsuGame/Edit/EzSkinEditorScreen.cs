// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Edit.Components;
using osu.Game.EzOsuGame.Edit.Note;
using osu.Game.EzOsuGame.Layout;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.Overlays.Preview;
using osu.Game.EzOsuGame.Playback;
using osu.Game.EzOsuGame.ScriptedSkin;
using osu.Game.EzOsuGame.UI;
using osu.Game.Graphics.Cursor;
using osu.Game.IO;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets;
using osu.Game.Screens;
using osu.Game.Screens.Play;
using osu.Game.Skinning;
using osuTK;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.EzOsuGame.Edit
{
    // Milestone index — see EzSkinEditor refactor plan:
    // M1: layout shell + scene strategies + sidebar groups
    // M2: live vs snapshot comparison + Note/LN preview (skin.ini keeps Saved/Draft)
    // M3: Skin.ini editor + backup + save footer
    // M4: top-bar preview controls + static/beatmap scene backends
    // M5: EzSkin.json + advanced colouring + config menu (json/colour→ini)
    // M6: size→ini, .osk export
    // M7: preview toolbar + skin popover + virtual comparison on size/colour scenes
    // M8: size/colour virtual provider registration fix
    // M9: config snapshot comparison + create/restore snapshot + auto-save on navigate
    // M10: Note scene with independent note-edit snapshot comparison + export

    /// <summary>
    /// Ez 皮肤编辑器工作区（菜单栏、场景栏、游玩洞、侧栏）。
    /// 由 <see cref="EzSkinEditorOverlay"/> 托管；当前游玩/选歌屏经缩放插拔，不进本树。
    /// </summary>
    public partial class EzSkinEditorScreen : CompositeDrawable
    {
        public Action? RequestClose { get; set; }

        public Action? OpenOfficialHudEditor { get; set; }

        public Action<EzSkinEditorSceneType>? SceneApplied { get; set; }

        public Func<EzSkinEditorSceneType, bool>? CanPresentScene { get; set; }

        /// <summary>
        /// Raised when the user picks a preview mode. The workspace only resolves which beatmap to use;
        /// the owning overlay switches the global ruleset/beatmap and re-presents the scene, because the
        /// visible preview is a real gameplay screen bound to those globals, not this workspace's state.
        /// </summary>
        public Action<RulesetInfo, WorkingBeatmap>? BeatmapPreviewRequested { get; set; }

        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Blue);

        [Resolved]
        private SkinManager skinManager { get; set; } = null!;

        [Resolved]
        private Ez2ConfigManager ezSkinConfig { get; set; } = null!;

        [Resolved]
        private BeatmapManager beatmapManager { get; set; } = null!;

        [Resolved]
        private IBindable<WorkingBeatmap> gameBeatmap { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private GameHost host { get; set; } = null!;

        [Resolved]
        private Storage storage { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private IDialogOverlay? dialogOverlay { get; set; }

        [Resolved(canBeNull: true)]
        private INotificationOverlay? notifications { get; set; }

        [Resolved]
        private MusicController musicController { get; set; } = null!;

        [Resolved]
        private EzResourceStore resource { get; set; } = null!;

        [Resolved]
        private EzPlaybackController playback { get; set; } = null!;

        private DependencyContainer dependencies = null!;
        private Container gameplayHole = null!;
        private Drawable? activeSceneHost;
        private EzSkinEditorHudLayer hudLayer = null!;
        private EzSkinEditorMenuBar menuBar = null!;
        private EzSkinEditorSceneBar sceneBar = null!;
        private EzSkinEditorSidebar sidebar = null!;
        private EzPlaybackBar playbackBar = null!;

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
            => dependencies = new DependencyContainer(base.CreateChildDependencies(parent));

        private EzSkinEditorBeatmapPicker? beatmapPicker;

        private bool attemptedGlobalBeatmapPreview;
        private bool comparisonSnapshotInitialized;
        private Guid comparisonSnapshotSkinId;
        private bool noteSnapshotInitialized;
        private Guid noteSnapshotSkinId;
        private bool configPreviewRefreshBound;
        private bool previewRefreshScheduled;

        /// <summary>
        /// 框架把 <c>GetBindable</c> 返回的绑定副本只登记为 WeakReference；副本若无强引用会被 GC 回收，订阅会静默失效。
        /// </summary>
        private readonly List<IBindable> retainedBindingCopies = new List<IBindable>();

        private ISkinEditorVirtualProvider? provider;
        private EzSkinEditorSceneContext? sceneContext;

        private Bindable<EzBeatmapPreviewMode>? sceneBarPreviewModeBindable;
        private bool playbackBarActive;
        private OsuScreen? hudTargetScreen;
        private EzLayoutLayer? layoutLayer;

        public Bindable<EzSkinEditorSceneType> CurrentScene => sceneBar.CurrentScene;

        internal void ApplyCoordinatedScene(EzSkinEditorSceneType scene) => sceneBar.CurrentScene.Value = scene;

        internal Bindable<EzSkinEditorPreviewSource> PreviewSource => PreviewState.Source;

        /// <summary>
        /// Per-skin skin.ini session. Skin-layer only.
        /// </summary>
        public EzSkinIniSession? SkinIniSession { get; private set; }

        /// <summary>
        /// Per-skin EzSkin.json session. Created only via config menu.
        /// </summary>
        public EzSkinJsonSession? SkinJsonSession { get; private set; }

        internal EzSkinEditorPreviewState PreviewState { get; } = new EzSkinEditorPreviewState();

        internal Bindable<EzSkinEditorNoteCompareKind> NoteCompareKind { get; } = new Bindable<EzSkinEditorNoteCompareKind>();

        public EzSkinEditorScreen()
        {
            RelativeSizeAxes = Axes.Both;
        }

        public void Activate()
        {
            Schedule(refreshScene);
            this.FadeInFromZero(200, Easing.OutQuint);
        }

        public void Deactivate()
        {
            persistEditorConfig();
            hudLayer.DetachLiveTargets();
        }

        public void RequestCloseFromUser() => tryExit();

        public void SetSceneHost(Drawable? host)
        {
            if (activeSceneHost == host)
                return;

            if (activeSceneHost?.Parent == gameplayHole)
                gameplayHole.Remove(activeSceneHost, false);

            activeSceneHost = host;

            if (activeSceneHost != null && activeSceneHost.Parent != gameplayHole)
                gameplayHole.Add(activeSceneHost);
        }

        public void BindHudToTarget(OsuScreen? screen, EzLayoutLayer? ezLayout)
        {
            hudTargetScreen = screen;
            layoutLayer = ezLayout;
            hudLayer.Apply(sceneBar.CurrentScene.Value, screen, ezLayout);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            // 先构造并缓存，供侧栏 HUD 栏解析选中项。
            hudLayer = new EzSkinEditorHudLayer();
            dependencies.CacheAs(hudLayer);

            InternalChildren = new Drawable[]
            {
                new OsuContextMenuContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = new PopoverContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Child = new GridContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            RowDimensions = new[]
                            {
                                new Dimension(GridSizeMode.AutoSize),
                                new Dimension(GridSizeMode.AutoSize),
                                new Dimension(),
                            },
                            Content = new[]
                            {
                                new Drawable[]
                                {
                                    new Container
                                    {
                                        Name = @"Menu container",
                                        RelativeSizeAxes = Axes.X,
                                        Depth = float.MinValue,
                                        Height = osu.Game.Overlays.SkinEditor.SkinEditor.MENU_HEIGHT,
                                        Children = new Drawable[]
                                        {
                                            menuBar = new EzSkinEditorMenuBar
                                            {
                                                ApplyAction = applySettings,
                                                ExitAction = tryExit,
                                                CreateEzSkinJsonAction = createEzSkinJson,
                                                UpdateEzSkinJsonSnapshotAction = updateEzSkinJsonSnapshot,
                                                RemoveEzSkinJsonAction = removeEzSkinJson,
                                                ImportJsonAction = importJson,
                                                ImportFromSkinJsonAction = importFromSkinJson,
                                                ExportJsonAction = exportJson,
                                                WriteColoursToSkinIniAction = writeColoursToSkinIni,
                                                WriteSizesToSkinIniAction = writeSizesToSkinIni,
                                                ExportOskAction = exportOsk,
                                                CreateConfigSnapshotAction = createConfigSnapshot,
                                                RestoreConfigSnapshotAction = restoreConfigSnapshot,
                                                ExportPreviewImageAction = exportPreviewImage,
                                                CanCreateEzSkinJson = () => SkinJsonSession is { IsSupported: true, HasFile: false },
                                                CanExportPreviewImage = () => RuntimeInfo.IsDesktop,
                                                CanUpdateEzSkinJsonSnapshot = () => SkinJsonSession is { IsSupported: true, HasFile: true }
                                                                                    && SkinJsonSession.IsDirty(ezSkinConfig),
                                                CanRemoveEzSkinJson = () => SkinJsonSession is { IsSupported: true, HasFile: true },
                                                CanImportFromSkinJson = () => SkinJsonSession is { IsSupported: true, HasFile: true },
                                                CanWriteColoursToSkinIni = () => SkinIniSession is { IsSupported: true } && getCurrentKeyMode() > 0,
                                                CanWriteSizesToSkinIni = () => SkinIniSession is { IsSupported: true } && getCurrentKeyMode() > 0,
                                                CanExportOsk = canExportOsk,
                                                CanUseConfigSnapshot = () => sceneBar.CurrentScene.Value != EzSkinEditorSceneType.Note,
                                            },
                                            // Ez-only (official menu has skin dropdown alone). Right→left: skin, mode, progress+play.
                                            new FillFlowContainer
                                            {
                                                Name = @"Header controls",
                                                Anchor = Anchor.CentreRight,
                                                Origin = Anchor.CentreRight,
                                                AutoSizeAxes = Axes.X,
                                                RelativeSizeAxes = Axes.Y,
                                                Direction = FillDirection.Horizontal,
                                                Spacing = new Vector2(8),
                                                Padding = new MarginPadding
                                                {
                                                    Horizontal = 8,
                                                    Vertical = EzUIStyleV1.SCENE_BAR_GAP,
                                                },
                                                Children = new Drawable[]
                                                {
                                                    playbackBar = new EzPlaybackBar(playback),
                                                    new EzSkinEditorTopToolbar
                                                    {
                                                        PreviewSource = PreviewState.Source,
                                                        PreviewMode = PreviewState.Mode,
                                                        LoopBeatmap = PreviewState.LoopBeatmap,
                                                        Ruleset = PreviewState.Ruleset,
                                                        BeatmapPreviewRequested = selectBeatmapPreview,
                                                    },
                                                    new EzSkinEditorSkinDropdown
                                                    {
                                                        Anchor = Anchor.CentreLeft,
                                                        Origin = Anchor.CentreLeft,
                                                    },
                                                },
                                            },
                                        },
                                    },
                                },
                                new Drawable[]
                                {
                                    sceneBar = new EzSkinEditorSceneBar
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        SceneRequested = scene => sceneBar.CurrentScene.Value = scene,
                                        OpenOfficialHudEditorRequested = () => OpenOfficialHudEditor?.Invoke(),
                                    },
                                },
                                new Drawable[]
                                {
                                    new GridContainer
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        ColumnDimensions = new[]
                                        {
                                            new Dimension(),
                                            new Dimension(GridSizeMode.AutoSize),
                                        },
                                        Content = new[]
                                        {
                                            new Drawable[]
                                            {
                                                // 游玩洞：下层 ScreenContainer 经 SetCustomRect 缩进此处；本层只叠 HUD 蓝图。
                                                gameplayHole = new Container
                                                {
                                                    RelativeSizeAxes = Axes.Both,
                                                    Children = new Drawable[]
                                                    {
                                                        hudLayer,
                                                    },
                                                },
                                                sidebar = new EzSkinEditorSidebar(),
                                            },
                                        },
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
            SkinEditorProviderResolver.EnsureDefaultProviderRegistered();
            beatmapPicker = new EzSkinEditorBeatmapPicker(realm, beatmapManager);
            sceneBar.CurrentScene.BindValueChanged(onSceneChanged, true);
            skinManager.CurrentSkinInfo.BindValueChanged(onCurrentSkinInfoChanged);
            PreviewState.LoopBeatmap.BindValueChanged(_ => refreshScene());
            PreviewState.Ruleset.BindValueChanged(r =>
            {
                hudLayer.SetRuleset(r.NewValue);
                hudLayer.Apply(sceneBar.CurrentScene.Value, hudTargetScreen, layoutLayer);
            }, true);
            sceneBar.HudLayerVisible.BindValueChanged(v =>
            {
                hudLayer.SetVisible(v.NewValue);
                updateHudInteraction();
            }, true);
            bindConfigPreviewRefresh();
            bindSceneBarPlayback();
        }

        /// <summary>
        /// 场景支持 HUD 蓝图且 HUD layer 开启时才允许蓝图拖选；否则只读预览。
        /// </summary>
        private void updateHudInteraction()
        {
            hudLayer.InteractionEnabled.Value = EzSkinEditorSceneRegistry.SupportsHudLayer(sceneBar.CurrentScene.Value) && sceneBar.HudLayerVisible.Value;
        }

        protected override void Update()
        {
            base.Update();

            bool active = isGameplayPlaybackActive();
            playbackBar.Alpha = active ? 1 : 0;

            if (active != playbackBarActive)
            {
                playbackBarActive = active;

                if (active)
                    applyPreviewAudioPolicy();
            }
        }

        private void bindSceneBarPlayback()
        {
            sceneBarPreviewModeBindable = PreviewState.Mode.GetBoundCopy();
            sceneBarPreviewModeBindable.BindValueChanged(mode =>
            {
                // Mode 是预览的 desired state：只有仍在游戏内预览时才把播放键同步过去。
                if (!isGameplayPlaybackActive())
                    return;

                playback.SetPlaying(mode.NewValue == EzBeatmapPreviewMode.Dynamic);
            }, true);

            playback.IsPlaying.BindValueChanged(p =>
            {
                if (!isGameplayPlaybackActive())
                    return;

                if (p.NewValue)
                    PreviewState.ResumeBeatmapPlayback();
                else
                    PreviewState.PauseBeatmapPlayback();
            });
        }

        /// <summary>
        /// 只有插拔进来的真实游玩屏在当前时，播放条才有意义；选歌/主菜单场景隐藏。
        /// </summary>
        private bool isGameplayPlaybackActive() => playback.ActiveTarget.Value == EzPlaybackTarget.Gameplay;

        private void onSceneChanged(ValueChangedEvent<EzSkinEditorSceneType> change)
        {
            // CurrentScene is the single request source; a scene the editor cannot present reverts it
            // so the bar and coordinator never diverge.
            if (CanPresentScene?.Invoke(change.NewValue) == false)
            {
                sceneBar.CurrentScene.Value = change.OldValue;
                return;
            }

            if (change.OldValue != change.NewValue)
                persistEditorConfig();

            Schedule(() =>
            {
                applyCurrentScene();
                SceneApplied?.Invoke(sceneBar.CurrentScene.Value);
            });
        }

        public void PopulateSettings() => refreshScene();

        /// <summary>
        /// Re-presents the current scene after the preview beatmap/ruleset changed, without rebuilding the
        /// sidebar or the HUD layer. Only the plugged gameplay screen depends on the preview selection;
        /// sidebar sections bind to the shared <see cref="PreviewState"/> and follow it live, and the
        /// playback bar renders straight from <see cref="EzPlaybackController"/>.
        /// </summary>
        internal void RefreshPreviewScene()
        {
            SceneApplied?.Invoke(sceneBar.CurrentScene.Value);
        }

        public void ApplySettings() => applySettings();

        internal void CreateEzSkinJsonForTesting() => createEzSkinJson();

        internal void WriteSizesToSkinIniForTesting() => writeSizesToSkinIni();

        internal void CreateConfigSnapshotForTesting() => createConfigSnapshot();

        internal void RestoreConfigSnapshotForTesting() => restoreConfigSnapshot();

        internal EzSkinEditorComparisonSnapshot ComparisonSnapshotForTesting { get; } = new EzSkinEditorComparisonSnapshot();

        internal EzSkinEditorNoteEditSession NoteSessionForTesting { get; } = new EzSkinEditorNoteEditSession();

        internal EzSkinEditorNoteEditSnapshot NoteSnapshotForTesting { get; } = new EzSkinEditorNoteEditSnapshot();

        internal void CreateNoteSnapshotForTesting() => createNoteSnapshot();

        internal void RestoreNoteSnapshotForTesting() => restoreNoteSnapshot();

        private void refreshScene()
        {
            ensureGlobalBeatmapPreview();

            ensureSkinIniSession();
            ensureSkinSpecificSceneVisibility();
            ensureSkinJsonSession();
            ensureComparisonSnapshotForCurrentSkin();
            ensureNoteSnapshotForCurrentSkin();

            sceneContext = buildSceneContext();
            applyCurrentScene();
            SceneApplied?.Invoke(sceneBar.CurrentScene.Value);

            menuBar.RefreshMenuState();
        }

        private void ensureGlobalBeatmapPreview()
        {
            if (attemptedGlobalBeatmapPreview || PreviewState.HasBeatmapLoaded)
                return;

            attemptedGlobalBeatmapPreview = true;

            if (!EzSkinEditorBeatmapPicker.CanUseAsPreview(gameBeatmap.Value))
                return;

            if (gameBeatmap.Value.BeatmapInfo.Ruleset is not RulesetInfo ruleset)
                return;

            PreviewState.SetBeatmap(gameBeatmap.Value, ruleset, EzBeatmapPreviewModes.GetAppearanceLoadMode(ruleset));
        }

        private void selectBeatmapPreview(RulesetInfo ruleset)
        {
            if (beatmapPicker == null)
                return;

            if (!beatmapPicker.TryPickRandom(ruleset, out var workingBeatmap) || workingBeatmap is not WorkingBeatmap picked)
            {
                notifications?.Post(new SimpleNotification
                {
                    Text = EzEditorStrings.NOTIFY_BEATMAP_NOT_FOUND,
                });
                return;
            }

            // The visible preview is the real gameplay screen, which renders the global beatmap; updating
            // this workspace's PreviewState alone changes nothing on screen. The overlay owns the switch.
            PreviewState.SetBeatmap(picked, ruleset, EzBeatmapPreviewModes.GetAppearanceLoadMode(ruleset));
            BeatmapPreviewRequested?.Invoke(ruleset, picked);
        }

        private EzSkinEditorSceneContext buildSceneContext()
        {
            var currentScene = sceneBar.CurrentScene.Value;

            bool useLoopBeatmap = PreviewState.LoopBeatmap.Value;
            // Size/Colour 已并入 Appearance 五栏；本步关闭虚拟对比预览。
            const bool use_virtual_comparison_preview = false;
            bool useNoteComparisonOnly = currentScene == EzSkinEditorSceneType.Note;
            bool supportsPlayfieldPreview = currentScene is EzSkinEditorSceneType.Appearance
                                                            or EzSkinEditorSceneType.SkinIni
                                                            or EzSkinEditorSceneType.SkinSpecific;

            // PreviewHost 场景在未开循环谱时用选中谱面预览。
            bool allowBeatmapPreview = supportsPlayfieldPreview
                                       && currentScene != EzSkinEditorSceneType.Appearance
                                       && !useLoopBeatmap
                                       && PreviewState.HasBeatmapLoaded;

            SkinEditorProviderResolver.EnsureDefaultProviderRegistered();

            ISkinEditorVirtualProvider? loopProvider = null;
            int virtualPreviewKeyCount = 0;

            if (useLoopBeatmap && supportsPlayfieldPreview)
            {
                loopProvider = SkinEditorProviderResolver.ResolveLoopProvider(PreviewState.Ruleset.Value?.OnlineID);
                virtualPreviewKeyCount = loopProvider == null ? 0 : getLoopPreviewKeyCount(currentScene);
            }

            provider = loopProvider;

            bool usesEzNoteVariants = EzSkinEditorNoteVariantResolver.UsesEzVariants(skinManager.CurrentSkin.Value);

            return new EzSkinEditorSceneContext
            {
                Provider = provider,
                EditorSkin = getEditorSkin(),
                ActualSkin = skinManager.CurrentSkin.Value,
                UsesEzNoteVariants = usesEzNoteVariants,
                NoteComparisonSource = buildNoteComparisonSource(useNoteComparisonOnly, use_virtual_comparison_preview, usesEzNoteVariants),
                NoteCompareKind = NoteCompareKind,
                SkinIniSession = SkinIniSession,
                SkinJsonSession = SkinJsonSession,
                PreviewState = PreviewState,
                RequestSceneRefresh = refreshScene,
                RequestPreviewRefresh = requestPreviewRefresh,
                CommitSkinIni = commitSkinIni,
                AllowBeatmapPreview = allowBeatmapPreview,
                UseLoopBeatmap = useLoopBeatmap,
                UseVirtualComparisonPreview = use_virtual_comparison_preview,
                VirtualPreviewKeyCount = virtualPreviewKeyCount,
                UseNoteComparisonOnly = useNoteComparisonOnly,
                PreviewSource = PreviewState.Source.Value,
                PreviewBeatmap = PreviewState.PreviewBeatmap,
                PreviewRuleset = PreviewState.Ruleset.Value,
                PreviewMode = PreviewState.Mode.Value,
                ComparisonSnapshot = useNoteComparisonOnly ? null : ComparisonSnapshotForTesting,
                NoteSession = NoteSessionForTesting,
                NoteSnapshot = NoteSnapshotForTesting,
                CreateNoteSnapshot = createNoteSnapshot,
                RestoreNoteSnapshot = restoreNoteSnapshot,
                ExportNotePreview = exportNotePreview,
                GetEmbeddedPlayer = () => null,
            };
        }

        private int getLoopPreviewKeyCount(EzSkinEditorSceneType scene) => scene switch
        {
            EzSkinEditorSceneType.SkinSpecific => 4,
            _ => Math.Max(4, getCurrentKeyMode()),
        };

        private void applyPreviewAudioPolicy()
        {
            // 游玩音频由插拔进来的 Player 持有；编辑器打开时停掉选歌全局轨，避免双音。
            if (hudTargetScreen is Player)
                musicController.Stop();
        }

        private IEzSkinEditorNoteComparisonSource? buildNoteComparisonSource(bool useNoteComparisonOnly, bool useVirtualComparisonPreview, bool usesEzNoteVariants)
        {
            if (!useNoteComparisonOnly && !useVirtualComparisonPreview)
                return null;

            if (useNoteComparisonOnly)
                return new NoteEditComparisonSource(NoteSessionForTesting, NoteSnapshotForTesting, usesEzNoteVariants);

            var profile = EzSkinEditorNoteRulesetProfileRegistry.All.FirstOrDefault();

            if (profile == null)
                return null;

            string variantId = profile.GetDefaultVariantId(usesEzNoteVariants, EzManiaNotePart.Note);

            return new ConfigSnapshotComparisonSource(
                ezSkinConfig,
                ComparisonSnapshotForTesting,
                usesEzNoteVariants,
                variantId,
                profile.RulesetInfo);
        }

        private void applyCurrentScene()
        {
            sceneContext = buildSceneContext();

            // 游玩洞常驻插拔屏；场景只换侧栏，不重建中间前景。
            var strategy = EzSkinEditorSceneRegistry.Get(sceneBar.CurrentScene.Value);
            sidebar.ApplyStrategy(strategy, sceneContext);
            updateHudInteraction();
            hudLayer.Apply(sceneBar.CurrentScene.Value, hudTargetScreen, layoutLayer);
        }

        private void applySettings()
        {
            ezSkinConfig.Save();
            hudLayer.PersistLayoutToCurrentSkin();

            if (SkinIniSession is { IsSupported: true, IsDirty: true })
                SkinIniSession.Commit();

            invalidateResourceCaches();
            skinManager.CurrentSkinInfo.TriggerChange();
            refreshScene();
        }

        private void commitSkinIni()
        {
            if (SkinIniSession is not { IsSupported: true, IsDirty: true })
                return;

            SkinIniSession.Commit();
            invalidateResourceCaches();
            refreshScene();
        }

        // 用户可能在我们开着编辑器时往 EzResources 里丢/换图。保存与应用皮肤是用户主动的冷入口，
        // 在这里丢弃目录索引、帧解析缓存与动画/大图两页，新文件才会立刻出现在预览里（与资源选择器同一套做法）。
        private void invalidateResourceCaches() => resource.InvalidateResourceCaches();

        private bool isSkinIniSupported() => EzSkinIniSupport.IsSupported(skinManager.CurrentSkinInfo.Value);

        private void ensureSkinSpecificSceneVisibility()
        {
            // 顶栏 SkinSpecific 为占位壳，按钮已隐藏；专用设置在 Appearance 侧栏「皮肤专用」栏。
            sceneBar.SetSceneVisible(EzSkinEditorSceneType.SkinSpecific, false);
        }

        private void ensureSkinIniSession()
        {
            var currentSkin = skinManager.CurrentSkinInfo.Value;

            if (!isSkinIniSupported())
            {
                if (SkinIniSession?.IsDirty == true)
                    SkinIniSession.Discard();

                SkinIniSession = null;
                sceneBar.SetSceneVisible(EzSkinEditorSceneType.SkinIni, false);

                if (sceneBar.CurrentScene.Value == EzSkinEditorSceneType.SkinIni)
                    sceneBar.CurrentScene.Value = EzSkinEditorSceneType.Appearance;

                return;
            }

            sceneBar.SetSceneVisible(EzSkinEditorSceneType.SkinIni, true);

            Guid currentSkinId = currentSkin.ID;
            SkinIniSession ??= new EzSkinIniSession(skinManager);

            if (SkinIniSession.SkinId == currentSkinId)
                return;

            if (SkinIniSession.IsDirty)
                SkinIniSession.Discard();

            SkinIniSession.LoadFromSkin(currentSkin);
        }

        private void ensureSkinJsonSession()
        {
            var currentSkin = skinManager.CurrentSkinInfo.Value;

            if (!EzSkinJsonSupport.IsSupported(currentSkin))
            {
                SkinJsonSession = null;
                return;
            }

            Guid currentSkinId = currentSkin.ID;
            SkinJsonSession ??= new EzSkinJsonSession(skinManager);

            if (SkinJsonSession.SkinId == currentSkinId)
                return;

            SkinJsonSession.LoadFromSkin(currentSkin);
        }

        private void createEzSkinJson()
        {
            ensureSkinJsonSession();

            if (SkinJsonSession == null || !SkinJsonSession.CreateInitial(ezSkinConfig))
            {
                postNotification(EzEditorStrings.NOTIFY_CANNOT_CREATE_EZSKIN_JSON);
                return;
            }

            postNotification(EzEditorStrings.NOTIFY_CREATED_EZSKIN_JSON);
            refreshScene();
        }

        private void updateEzSkinJsonSnapshot()
        {
            ensureSkinJsonSession();

            if (SkinJsonSession == null || !SkinJsonSession.Commit(ezSkinConfig))
            {
                postNotification(EzEditorStrings.NOTIFY_CANNOT_UPDATE_EZSKIN_JSON_SNAPSHOT);
                return;
            }

            postNotification(EzEditorStrings.NOTIFY_UPDATED_EZSKIN_JSON_SNAPSHOT);
            refreshScene();
        }

        private void removeEzSkinJson()
        {
            ensureSkinJsonSession();

            if (SkinJsonSession == null || !SkinJsonSession.Remove())
            {
                postNotification(EzEditorStrings.NOTIFY_CANNOT_REMOVE_EZSKIN_JSON);
                return;
            }

            postNotification(EzEditorStrings.NOTIFY_REMOVED_EZSKIN_JSON);
            refreshScene();
        }

        private void importFromSkinJson()
        {
            ensureSkinJsonSession();

            if (SkinJsonSession is not { HasFile: true })
            {
                postNotification(EzEditorStrings.NOTIFY_NO_EZSKIN_JSON_ON_SKIN);
                return;
            }

            string? json = skinManager.CurrentSkinInfo.Value.PerformRead(skin => EzSkinJsonStorage.TryReadJson(skinManager, skin));

            if (json == null)
            {
                postNotification(EzEditorStrings.NOTIFY_CANNOT_READ_EZSKIN_JSON);
                return;
            }

            EzSkinJsonBridge.Apply(EzSkinJsonDocument.Parse(json), ezSkinConfig);
            postNotification(EzEditorStrings.NOTIFY_IMPORTED_FROM_SKIN_TO_MEMORY);
            refreshScene();
        }

        private void importJson()
        {
            var selector = host.CreateSystemFileSelector(new[] { ".json" });

            if (selector == null)
            {
                postNotification(EzEditorStrings.NOTIFY_FILE_SELECTOR_NOT_SUPPORTED);
                return;
            }

            selector.Selected += file =>
            {
                try
                {
                    string text = File.ReadAllText(file.FullName);
                    var document = EzSkinJsonDocument.Parse(text);

                    if (SkinJsonSession is { IsSupported: true })
                        SkinJsonSession.ApplyImportedDocument(document, ezSkinConfig);
                    else
                        EzSkinJsonBridge.Apply(document, ezSkinConfig);

                    postNotification(EzEditorStrings.NOTIFY_IMPORTED_JSON);
                    refreshScene();
                }
                catch (Exception e)
                {
                    postNotification(LocalisableString.Format(EzEditorStrings.NOTIFY_IMPORT_FAILED, e.Message));
                }
                finally
                {
                    selector.Dispose();
                }
            };

            selector.Present();
        }

        private void exportJson()
        {
            try
            {
                var exportStorage = (storage as OsuStorage)?.GetExportStorage() ?? storage.GetStorageForDirectory(@"exports");
                string skinName = skinManager.CurrentSkinInfo.Value.PerformRead(s => s.Name);
                string path = EzSkinJsonStorage.ExportToStorage(exportStorage, ezSkinConfig, skinName);
                postNotification(LocalisableString.Format(EzEditorStrings.NOTIFY_EXPORTED_TO, path));
            }
            catch (Exception e)
            {
                postNotification(LocalisableString.Format(EzEditorStrings.NOTIFY_EXPORT_FAILED, e.Message));
            }
        }

        private void writeColoursToSkinIni()
        {
            int keyMode = getCurrentKeyMode();

            if (SkinIniSession == null || !EzSkinColourSkinIniWriter.TryWriteManiaColumnColours(keyMode, ezSkinConfig, SkinIniSession))
            {
                postNotification(EzEditorStrings.NOTIFY_CANNOT_WRITE_SKIN_INI_COLOURS);
                return;
            }

            postNotification(LocalisableString.Format(EzEditorStrings.NOTIFY_COLOURS_WRITTEN_TO_DRAFT, keyMode));
            refreshScene();
        }

        private void writeSizesToSkinIni()
        {
            int keyMode = getCurrentKeyMode();

            if (SkinIniSession == null || !EzSkinSizeSkinIniWriter.TryWriteManiaSizeSettings(keyMode, ezSkinConfig, SkinIniSession))
            {
                postNotification(EzEditorStrings.NOTIFY_CANNOT_WRITE_SKIN_INI_SIZES);
                return;
            }

            postNotification(LocalisableString.Format(EzEditorStrings.NOTIFY_SIZES_WRITTEN_TO_DRAFT, keyMode));
            refreshScene();
        }

        private void exportOsk()
        {
            if (!canExportOsk())
            {
                postNotification(RuntimeInfo.IsDesktop ? EzEditorStrings.NOTIFY_CANNOT_EXPORT_SKIN : EzEditorStrings.NOTIFY_EXPORT_NOT_SUPPORTED_PLATFORM);
                return;
            }

            try
            {
                bool committedSkinIni = false;

                if (SkinIniSession is { IsDirty: true })
                {
                    SkinIniSession.Commit();
                    invalidateResourceCaches();
                    committedSkinIni = true;
                }

                skinManager.ExportCurrentSkin();
                postNotification(committedSkinIni ? EzEditorStrings.NOTIFY_SAVED_SKIN_INI_AND_EXPORTING_OSK : EzEditorStrings.NOTIFY_EXPORTING_OSK);
                refreshScene();
            }
            catch (Exception e)
            {
                postNotification(LocalisableString.Format(EzEditorStrings.NOTIFY_EXPORT_FAILED, e.Message));
            }
        }

        private int getCurrentKeyMode()
        {
            if (PreviewState.SuggestedKeyMode.Value is int suggested and > 0)
                return suggested;

            return ezSkinConfig.Get<int>(Ez2Setting.ColumnTypeListSelect);
        }

        private bool canExportOsk() => RuntimeInfo.IsDesktop
                                       && !skinManager.CurrentSkin.Disabled
                                       && skinManager.CurrentSkinInfo.Value.PerformRead(s => !s.Protected);

        private void postNotification(LocalisableString text) => notifications?.Post(new SimpleNotification { Text = text });

        private void onCurrentSkinInfoChanged(ValueChangedEvent<Live<SkinInfo>> skin)
        {
            if (skin.OldValue.ID != skin.NewValue.ID)
                persistEditorConfig();

            Schedule(refreshScene);
        }

        private void ensureComparisonSnapshotForCurrentSkin()
        {
            var currentSkinId = skinManager.CurrentSkinInfo.Value.ID;

            if (comparisonSnapshotInitialized && comparisonSnapshotSkinId == currentSkinId)
                return;

            recaptureComparisonSnapshot();
            comparisonSnapshotInitialized = true;
            comparisonSnapshotSkinId = currentSkinId;
        }

        private void recaptureComparisonSnapshot()
        {
            ComparisonSnapshotForTesting.CaptureFrom(ezSkinConfig, SkinIniSession);
            ComparisonSnapshotForTesting.SyncBindableDefaults(ezSkinConfig);
        }

        private void createConfigSnapshot()
        {
            recaptureComparisonSnapshot();
            postNotification(EzEditorStrings.NOTIFY_CREATED_CONFIG_SNAPSHOT);
            refreshScene();
        }

        private void restoreConfigSnapshot()
        {
            ComparisonSnapshotForTesting.ApplyTo(ezSkinConfig, SkinIniSession);
            postNotification(EzEditorStrings.NOTIFY_RESTORED_CONFIG_SNAPSHOT);
            refreshScene();
        }

        private void ensureNoteSnapshotForCurrentSkin()
        {
            var currentSkinId = skinManager.CurrentSkinInfo.Value.ID;

            if (noteSnapshotInitialized && noteSnapshotSkinId == currentSkinId)
                return;

            initializeNoteSessionDefaults();
            recaptureNoteSnapshot();
            noteSnapshotInitialized = true;
            noteSnapshotSkinId = currentSkinId;
        }

        private void initializeNoteSessionDefaults()
        {
            if (NoteSessionForTesting.Ruleset.Value is not null)
                return;

            var profile = EzSkinEditorNoteRulesetProfileRegistry.All.FirstOrDefault();

            if (profile == null)
                return;

            NoteSessionForTesting.Ruleset.Value = profile.RulesetInfo;
            NoteSessionForTesting.VariantId.Value = profile.GetDefaultVariantId(
                EzSkinEditorNoteVariantResolver.UsesEzVariants(skinManager.CurrentSkin.Value),
                NoteSessionForTesting.Part.Value);
        }

        private void recaptureNoteSnapshot()
        {
            NoteSnapshotForTesting.CaptureFrom(NoteSessionForTesting);
        }

        private void createNoteSnapshot()
        {
            recaptureNoteSnapshot();
            postNotification(EzEditorStrings.NOTIFY_CREATED_NOTE_SNAPSHOT);
            refreshNoteComparisonSnapshotPane();
        }

        private void restoreNoteSnapshot()
        {
            NoteSnapshotForTesting.ApplyTo(NoteSessionForTesting);
            postNotification(EzEditorStrings.NOTIFY_RESTORED_NOTE_SNAPSHOT);
            refreshNoteComparisonLivePane();
        }

        private void refreshNoteComparisonSnapshotPane() => Schedule(refreshPreviewContent);

        private void refreshNoteComparisonLivePane() => Schedule(refreshPreviewContent);

        private void exportNotePreview()
        {
            if (!RuntimeInfo.IsDesktop)
                return;

            var skinInfo = skinManager.CurrentSkinInfo.Value;

            if (skinInfo.PerformRead(s => s.Protected))
            {
                postNotification(EzEditorStrings.NOTIFY_CANNOT_EXPORT_NOTE_PREVIEW);
                return;
            }

            var profile = EzSkinEditorNoteRulesetProfileRegistry.Get(NoteSessionForTesting.Ruleset.Value);

            if (profile == null)
            {
                postNotification(EzEditorStrings.TOOLBAR_NOT_SUPPORTED);
                return;
            }

            string exportName = string.IsNullOrWhiteSpace(NoteSessionForTesting.ExportName.Value)
                ? "note-preview"
                : NoteSessionForTesting.ExportName.Value.Trim();

            foreach (char invalid in Path.GetInvalidFileNameChars())
                exportName = exportName.Replace(invalid, '_');

            bool usesEzVariants = EzSkinEditorNoteVariantResolver.UsesEzVariants(skinManager.CurrentSkin.Value);
            var request = NoteSessionForTesting.ToPreviewRequest(usesEzVariants, NoteCompareKind.Value);

            Task.Run(async () =>
            {
                try
                {
                    string? directory = ScriptedSkinSupport.IsScriptedSkin(skinInfo)
                        ? skinManager.GetScriptDirectory(skinInfo.Value)
                        : null;

                    ExternalEditOperation<SkinInfo>? edit = null;

                    if (directory == null)
                    {
                        var detached = skinInfo.PerformRead(s => s.Detach());
                        edit = await skinManager.BeginExternalEditing(detached).ConfigureAwait(false);
                        directory = edit.MountedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    }

                    using var image = EzSkinEditorNoteImageExporter.Export(profile, request, directory);

                    if (image == null)
                    {
                        if (edit != null)
                            await edit.Finish().ConfigureAwait(false);

                        Schedule(() => postNotification(EzEditorStrings.NOTIFY_CANNOT_EXPORT_NOTE_PREVIEW));
                        return;
                    }

                    string filePath = Path.Combine(directory, $"{exportName}.png");
                    await image.SaveAsPngAsync(filePath).ConfigureAwait(false);

                    if (edit != null)
                        await edit.Finish().ConfigureAwait(false);

                    Schedule(() =>
                    {
                        host.OpenFileExternally(directory + Path.DirectorySeparatorChar);
                        postNotification(EzEditorStrings.NOTIFY_NOTE_EXPORTED);
                    });
                }
                catch (Exception e)
                {
                    Schedule(() => postNotification(LocalisableString.Format(EzEditorStrings.NOTIFY_EXPORT_FAILED, e.Message)));
                }
            });
        }

        private void persistEditorConfig()
        {
            ezSkinConfig.Save();
            skinManager.CurrentSkinInfo.TriggerChange();
        }

        private void bindConfigPreviewRefresh()
        {
            if (configPreviewRefreshBound)
                return;

            configPreviewRefreshBound = true;

            // 绑定副本必须由本屏幕持有：框架只登记 WeakReference，副本被 GC 回收后订阅会静默失效。
            foreach (var setting in EzSkinJsonSettingCatalog.All)
            {
                if (EzSkinJsonBridge.BindSettingValueChanged(ezSkinConfig, setting, requestPreviewRefresh) is IBindable bindable)
                    retainedBindingCopies.Add(bindable);
            }
        }

        /// <summary>
        /// Coalesces repeated preview refresh requests within a frame. Dragging a slider produces one change per tick;
        /// rebuilding the scene tree for each of them is both wasteful and unbounded under snapshot comparison.
        /// </summary>
        private void requestPreviewRefresh()
        {
            if (previewRefreshScheduled)
                return;

            previewRefreshScheduled = true;

            Schedule(() =>
            {
                previewRefreshScheduled = false;
                refreshPreviewContent();
            });
        }

        private void refreshPreviewContent()
        {
            if (!IsLoaded)
                return;

            sceneContext = buildSceneContext();
            sidebar.ApplyStrategy(EzSkinEditorSceneRegistry.Get(sceneBar.CurrentScene.Value), sceneContext);
        }

        private void exportPreviewImage()
        {
            if (!RuntimeInfo.IsDesktop)
                return;

            host.TakeScreenshotAsync().ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    Schedule(() => postNotification(LocalisableString.Format(EzEditorStrings.NOTIFY_EXPORT_FAILED, task.Exception?.GetBaseException().Message ?? "unknown error")));
                    return;
                }

                Schedule(() =>
                {
                    try
                    {
                        using Image<Rgba32> image = task.GetResultSafely();
                        string filename = $"ez-skin-editor-preview-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.png";
                        var exportStorage = (storage as OsuStorage)?.GetExportStorage() ?? storage.GetStorageForDirectory(@"exports");

                        using (var stream = exportStorage.CreateFileSafely(filename))
                            image.SaveAsPng(stream);

                        postNotification(LocalisableString.Format(EzEditorStrings.NOTIFY_EXPORTED_TO, filename));
                    }
                    catch (Exception e)
                    {
                        postNotification(LocalisableString.Format(EzEditorStrings.NOTIFY_EXPORT_FAILED, e.Message));
                    }
                });
            }, TaskScheduler.Default);
        }

        private ISkin getEditorSkin()
        {
            var currentSkin = skinManager.CurrentSkin.Value;

            return currentSkin is EzStyleProSkin or Ez2Skin or SbISkin or ScriptedSkinWrapper
                ? currentSkin
                : new EzStyleProSkin(skinManager);
        }

        public void ShowExitDialog() => tryExit();

        private void tryExit()
        {
            if (dialogOverlay == null || SkinIniSession?.IsDirty != true)
            {
                RequestClose?.Invoke();
                return;
            }

            dialogOverlay.Push(new ConfirmDialog(EzEditorStrings.EXIT_CONFIRM_APPLY_TO_SKIN, () =>
            {
                applySettings();
                RequestClose?.Invoke();
            }, () =>
            {
                SkinIniSession?.Discard();
                RequestClose?.Invoke();
            }));
        }

        private static ISkinComponentLookup? tryCreateManiaSkinComponentLookupOrNull(string componentName)
        {
            const string lookup_type_name = "osu.Game.Rulesets.Mania.ManiaSkinComponentLookup, osu.Game.Rulesets.Mania";
            const string enum_type_name = "osu.Game.Rulesets.Mania.ManiaSkinComponents, osu.Game.Rulesets.Mania";

            try
            {
                var lookupType = Type.GetType(lookup_type_name, throwOnError: false);
                var enumType = Type.GetType(enum_type_name, throwOnError: false);

                if (lookupType == null || enumType == null)
                    return null;

                object componentValue = Enum.Parse(enumType, componentName, ignoreCase: false);
                return Activator.CreateInstance(lookupType, componentValue) as ISkinComponentLookup;
            }
            catch
            {
                return null;
            }
        }
    }
}

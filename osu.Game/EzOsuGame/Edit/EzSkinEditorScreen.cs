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
using osu.Framework.Input;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
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
using osu.Game.Input.Bindings;
using osu.Game.Localisation;
using osu.Game.IO;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;
using osu.Game.Overlays.SkinEditor;
using OfficialSkinEditor = osu.Game.Overlays.SkinEditor.SkinEditor;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets;
using osu.Game.Screens;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Play;
using osu.Game.Skinning;
using osuTK;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.EzOsuGame.Edit
{
    /// <summary>
    /// Ez 皮肤编辑器工作区（菜单栏、场景栏、游玩洞、侧栏）。
    /// 由 <see cref="EzSkinEditorOverlay"/> 托管；当前游玩/选歌屏经缩放插拔，不进本树。
    /// </summary>
    public partial class EzSkinEditorScreen : CompositeDrawable, IKeyBindingHandler<PlatformAction>, IKeyBindingHandler<GlobalAction>
    {
        public Action? RequestClose { get; set; }

        public Action? OpenOfficialHudEditor { get; set; }

        public Action<EzEditorSceneType>? SceneApplied { get; set; }

        public Func<EzEditorSceneType, bool>? CanPresentScene { get; set; }

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
        private Bindable<RulesetInfo> gameRuleset { get; set; } = null!;

        [Resolved]
        private IRulesetStore rulesets { get; set; } = null!;

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

        [Resolved(canBeNull: true)]
        private OnScreenDisplay? onScreenDisplay { get; set; }

        [Resolved(canBeNull: true)]
        private SkinEditorOverlay? skinEditorOverlay { get; set; }

        [Resolved]
        private MusicController musicController { get; set; } = null!;

        [Resolved]
        private EzResourceStore resource { get; set; } = null!;

        [Resolved]
        private EzPlaybackController playback { get; set; } = null!;

        [Resolved]
        private EzLayoutThemeHost layoutThemeHost { get; set; } = null!;

        private DependencyContainer dependencies = null!;
        private Container gameplayHole = null!;
        private Drawable? activeSceneHost;
        private EzSkinEditorHudLayer hudLayer = null!;
        private EzSkinEditorMenuBar menuBar = null!;
        private EzSkinEditorSceneBar sceneBar = null!;
        private EzSkinEditorSceneRail sceneRail = null!;
        private EzSkinEditorSidebar sidebar = null!;
        private EzPlaybackBar playbackBar = null!;
        private EzSkinEditorComponentPickerWindow componentPicker = null!;

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
            => dependencies = new DependencyContainer(base.CreateChildDependencies(parent));

        private EzSkinEditorBeatmapPicker? beatmapPicker;

        private bool comparisonSnapshotInitialized;
        private Guid comparisonSnapshotSkinId;

        // 菜单「配置快照」与左右对比：用户手动创建/恢复，不参与退出放弃。
        private bool appliedConfigBaselineInitialized;
        private Guid appliedConfigBaselineSkinId;
        private bool noteSnapshotInitialized;
        private Guid noteSnapshotSkinId;

        private Bindable<bool>? previewIsPlayingBindable;
        private bool playbackBarActive;
        private OsuScreen? hudTargetScreen;
        private EzLayoutLayer? layoutLayer;

        public Bindable<EzEditorSceneType> CurrentScene => sceneBar.CurrentScene;

        internal void ApplyCoordinatedScene(EzEditorSceneType scene) => sceneBar.CurrentScene.Value = scene;

        /// <summary>
        /// Top-level group switch: re-selects the scene last used in that group, falling back to its
        /// first available scene. The scene bar only picks groups; the rail lists the scenes inside.
        /// </summary>
        private void requestSceneGroup(EzEditorSceneGroup group)
        {
            if (rememberedScene.TryGetValue(group, out var remembered) && canPresent(remembered))
            {
                sceneBar.CurrentScene.Value = remembered;
                return;
            }

            foreach (var strategy in EzSkinEditorSceneRegistry.ForGroup(group))
            {
                if (!canPresent(strategy.SceneType))
                    continue;

                sceneBar.CurrentScene.Value = strategy.SceneType;
                return;
            }
        }

        private bool canPresent(EzEditorSceneType scene) => CanPresentScene?.Invoke(scene) != false;

        private readonly Dictionary<EzEditorSceneGroup, EzEditorSceneType> rememberedScene = new Dictionary<EzEditorSceneGroup, EzEditorSceneType>();

        internal Bindable<IWorkingBeatmap?> PreviewBeatmap => PreviewState.PreviewBeatmap;

        /// <summary>
        /// Space the sidebar currently reserves in the workspace layout. Zero while the sidebar
        /// overlays (unpinned); panel + rail width while pinned-expanded. Drives the plugged scene rect.
        /// </summary>
        internal Bindable<float> SidebarReservedWidth => sidebar.ReservedWidth;

        /// <summary>
        /// Space the left secondary-scene rail always reserves. Drives the plugged scene rect the same
        /// way the sidebar's reserved width does, but is fixed since the rail never collapses.
        /// </summary>
        internal float SceneRailReservedWidth => EzVerticalRail.WIDTH;

        /// <summary>
        /// Per-skin skin.ini session. Skin-layer only.
        /// </summary>
        public EzSkinIniSession SkinIniSession { get; private set; } = null!;

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
            hudLayer.PersistAllLayouts();
            hudLayer.DetachLiveTargets();
        }

        public void RequestCloseFromUser() => RequestEndSession(() => RequestClose?.Invoke());

        public void RequestEndSession(Action onContinued)
        {
            if (!hasUnsavedWorkspaceChanges())
            {
                flushWorkspaceLayouts();
                persistEditorConfig();
                onContinued();
                return;
            }

            if (dialogOverlay == null)
            {
                applySettings();
                onContinued();
                return;
            }

            dialogOverlay.Push(new ConfirmDialog(EzEditorStrings.EXIT_CONFIRM_APPLY_TO_SKIN, () =>
            {
                applySettings();
                onContinued();
            }, () =>
            {
                discardWorkspaceChanges();
                onContinued();
            }));
        }

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
            syncHudRuleset();
            hudLayer.Apply(sceneBar.CurrentScene.Value, screen, ezLayout, sceneBar.ActiveLayer.Value);
        }

        /// <summary>
        /// Resolves the ruleset the HUD layer edits from the same source the gameplay containers are
        /// built from (the live screen), falling back to the editor's preview ruleset. Without this the
        /// ruleset-scoped layers resolve to no target, and the global layer only sees an empty global
        /// container whenever the layout keeps its components under a ruleset section.
        /// </summary>
        private void syncHudRuleset()
        {
            RulesetInfo? resolved = (hudTargetScreen as Player)?.Ruleset.Value ?? PreviewState.Ruleset.Value;

            if (sceneBar.Ruleset.Value?.Equals(resolved) == true)
                return;

            sceneBar.Ruleset.Value = resolved;
            hudLayer.SetRuleset(resolved);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            // 先构造并缓存，供侧栏 HUD 栏解析选中项；同时作为 IEditorChangeHandler 供选择处理器做撤销/重做。
            hudLayer = new EzSkinEditorHudLayer();
            dependencies.CacheAs(hudLayer);
            dependencies.CacheAs<IEditorChangeHandler>(hudLayer);
            dependencies.CacheAs(PreviewState);
            dependencies.CacheAs(PreviewState.PreviewGridBeatDivisor);
            dependencies.CacheAs(NoteSessionForTesting);
            dependencies.CacheAs(ComparisonSnapshotForTesting);
            SkinIniSession = new EzSkinIniSession(skinManager);
            dependencies.CacheAs(SkinIniSession);
            dependencies.CacheAs(new EzSkinEditorSidebarActions
            {
                CommitSkinIni = commitSkinIni,
                CreateNoteSnapshot = createNoteSnapshot,
                RestoreNoteSnapshot = restoreNoteSnapshot,
                ExportNotePreview = exportNotePreview,
            });

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
                                        // Smaller depth draws in front. The menu and its open dropdown must stay above the workspace.
                                        Depth = float.MinValue,
                                        Height = osu.Game.Overlays.SkinEditor.SkinEditor.MENU_HEIGHT,
                                        Children = new Drawable[]
                                        {
                                            menuBar = new EzSkinEditorMenuBar
                                            {
                                                ApplyAction = applySettings,
                                                ExitAction = RequestCloseFromUser,
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
                                                CanUseConfigSnapshot = () => sceneBar.CurrentScene.Value != EzEditorSceneType.Note,
                                                UndoAction = undo,
                                                RedoAction = redo,
                                                CutAction = cut,
                                                CopyAction = copy,
                                                PasteAction = paste,
                                                CloneAction = clone,
                                                CanUndo = () => hudLayer.ChangeHandler?.CanUndo.Value ?? false,
                                                CanRedo = () => hudLayer.ChangeHandler?.CanRedo.Value ?? false,
                                                CanCut = () => hudLayer.SelectedComponents.Count > 0,
                                                CanCopy = () => hudLayer.SelectedComponents.Count > 0,
                                                CanPaste = () => hudLayer.CanPaste,
                                                ExportLayoutPackAction = exportLayoutPack,
                                                ImportLayoutPackAction = importLayoutPack,
                                                ExportCurrentLayoutLayerAction = exportCurrentLayoutLayer,
                                                ImportCurrentLayoutLayerAction = importCurrentLayoutLayer,
                                                CanImportExportCurrentLayoutLayer = canImportExportCurrentLayoutLayer,
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
                                                        PreviewBeatmap = PreviewState.PreviewBeatmap,
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
                                        // In front of the workspace row so a scrolling sidebar cannot paint over these controls.
                                        Depth = -1,
                                        GroupRequested = requestSceneGroup,
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
                                            new Dimension(GridSizeMode.AutoSize),
                                            new Dimension(),
                                            new Dimension(GridSizeMode.AutoSize),
                                        },
                                        Content = new[]
                                        {
                                            new Drawable[]
                                            {
                                                sceneRail = new EzSkinEditorSceneRail(),
                                                // 游玩洞：下层 ScreenContainer 经 SetCustomRect 缩进此处；本层只叠 HUD 蓝图。
                                                gameplayHole = new Container
                                                {
                                                    RelativeSizeAxes = Axes.Both,
                                                    Children = new Drawable[]
                                                    {
                                                        hudLayer,
                                                        componentPicker = new EzSkinEditorComponentPickerWindow(ezSkinConfig.GetBindable<float>(Ez2Setting.SkinEditorComponentPickerX)),
                                                    },
                                                },
                                                sidebar = new EzSkinEditorSidebar
                                                {
                                                    Anchor = Anchor.TopRight,
                                                    Origin = Anchor.TopRight,
                                                    Width = 0,
                                                },
                                            },
                                        },
                                    },
                                },
                            },
                        },
                    },
                },
            };

            dependencies.CacheAs(sceneBar);
            cacheSkinEditorForToolboxPreviews();
        }

        private void cacheSkinEditorForToolboxPreviews()
        {
            if (skinEditorOverlay?.ToolboxSkinEditor is global::osu.Game.Overlays.SkinEditor.SkinEditor official)
                dependencies.CacheAs(official);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            hudLayer.LayoutWritten += onLayoutWritten;
            SkinEditorProviderResolver.EnsureDefaultProviderRegistered();
            beatmapPicker = new EzSkinEditorBeatmapPicker(realm, beatmapManager);
            sceneRail.CurrentScene.BindTo(sceneBar.CurrentScene);
            sceneBar.CurrentScene.BindValueChanged(onSceneChanged, true);
            skinManager.CurrentSkin.BindValueChanged(onCurrentSkinChanged);
            PreviewState.LoopBeatmap.BindValueChanged(_ => refreshScene());
            PreviewState.Ruleset.BindValueChanged(_ =>
            {
                syncHudRuleset();
                hudLayer.Apply(sceneBar.CurrentScene.Value, hudTargetScreen, layoutLayer, sceneBar.ActiveLayer.Value);
                updateHudInteraction();
            }, true);
            sceneBar.ActiveLayer.BindValueChanged(v =>
            {
                hudLayer.SetVisible(v.NewValue != EzHUDLayer.HudOff);
                hudLayer.Apply(sceneBar.CurrentScene.Value, hudTargetScreen, layoutLayer, v.NewValue);
                updateHudInteraction();
            }, true);
            hudLayer.OnStateChange += () => menuBar.RefreshMenuState();
            // 侧栏设置面板叠在游玩区右缘上；选择器右界跟随面板当前可见宽度，面板收起时贴到轨道左缘。
            componentPicker.RightReserve = () => sidebar.VisiblePanelReserve;
            sceneBar.VisualPickerOpen.BindTo(componentPicker.Open);
            bindSceneBarPlayback();
        }

        /// <summary>
        /// 场景支持 HUD 蓝图、且有可编辑层（非「隐藏 HUD」）时才允许蓝图拖选；否则只读预览。
        /// </summary>
        private void updateHudInteraction()
        {
            hudLayer.InteractionEnabled.Value = EzSkinEditorLayerOptions.HasEditingTarget(
                sceneBar.CurrentScene.Value, sceneBar.ActiveLayer.Value, sceneBar.Ruleset.Value);
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
            previewIsPlayingBindable = PreviewState.IsPlaying.GetBoundCopy();
            previewIsPlayingBindable.BindValueChanged(isPlaying =>
            {
                if (!isGameplayPlaybackActive())
                    return;

                playback.SetPlaying(isPlaying.NewValue);
            }, true);

            playback.IsPlaying.BindValueChanged(p =>
            {
                if (!isGameplayPlaybackActive())
                    return;

                PreviewState.IsPlaying.Value = p.NewValue;
            });
        }

        /// <summary>
        /// 只有插拔进来的真实游玩屏在当前时，播放条才有意义；选歌/主菜单场景隐藏。
        /// </summary>
        private bool isGameplayPlaybackActive() =>
            playback.ActiveTarget.Value is EzPlaybackTarget.Gameplay or EzPlaybackTarget.Editor;

        private void onSceneChanged(ValueChangedEvent<EzEditorSceneType> change)
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

            rememberedScene[EzSkinEditorSceneRegistry.GroupOf(change.NewValue)] = change.NewValue;

            if (change.NewValue == EzEditorSceneType.BeatmapPreview
                || change.NewValue == EzEditorSceneType.EditorColouring)
                syncPreviewStateFromGlobalBeatmap();

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
            syncPreviewStateFromGlobalBeatmap();
            SceneApplied?.Invoke(sceneBar.CurrentScene.Value);
        }

        /// <summary>
        /// 用当前全局谱面刷新本地预览状态（不随机切谱）。进入全谱预览或全局谱面变化时调用。
        /// </summary>
        internal void SyncPreviewStateFromGlobalBeatmap() => syncPreviewStateFromGlobalBeatmap();

        public void ApplySettings() => applySettings();

        internal void CreateEzSkinJsonForTesting() => createEzSkinJson();

        internal void WriteSizesToSkinIniForTesting() => writeSizesToSkinIni();

        internal void CreateConfigSnapshotForTesting() => createConfigSnapshot();

        internal void RestoreConfigSnapshotForTesting() => restoreConfigSnapshot();

        internal EzSkinEditorComparisonSnapshot ComparisonSnapshotForTesting { get; } = new EzSkinEditorComparisonSnapshot();

        /// <summary>
        /// 本会话「已落地」基线：进皮肤 capture，每次菜单 Apply 后刷新。退出脏判与放弃只对照此锚点（还原内存与 skin.ini 草稿，不写盘）。
        /// 本会话从未 Apply 时等价于进编辑器时磁盘状态。
        /// </summary>
        private readonly EzSkinEditorComparisonSnapshot appliedConfigBaseline = new EzSkinEditorComparisonSnapshot();

        internal EzSkinEditorNoteEditSession NoteSessionForTesting { get; } = new EzSkinEditorNoteEditSession();

        internal EzSkinEditorNoteEditSnapshot NoteSnapshotForTesting { get; } = new EzSkinEditorNoteEditSnapshot();

        internal void CreateNoteSnapshotForTesting() => createNoteSnapshot();

        internal void RestoreNoteSnapshotForTesting() => restoreNoteSnapshot();

        private void refreshScene()
        {
            syncPreviewStateFromGlobalBeatmap();

            ensureSkinIniSession();
            ensureSkinSpecificSceneVisibility();
            ensureSkinJsonSession();
            ensureComparisonSnapshotForCurrentSkin();
            ensureAppliedConfigBaselineForCurrentSkin();
            ensureNoteSnapshotForCurrentSkin();

            applyCurrentScene();
            SceneApplied?.Invoke(sceneBar.CurrentScene.Value);

            menuBar.RefreshMenuState();
        }

        private void syncPreviewStateFromGlobalBeatmap()
        {
            if (!EzSkinEditorBeatmapPicker.CanUseAsPreview(gameBeatmap.Value))
                return;

            if (gameBeatmap.Value.BeatmapInfo.Ruleset is not RulesetInfo beatmapRuleset)
                return;

            RulesetInfo? ruleset = EzSkinEditorBeatmapPicker.ResolveRulesetForPreview(rulesets, beatmapRuleset, gameRuleset.Value);

            if (ruleset == null)
                return;

            var working = gameBeatmap.Value;

            if (PreviewState.HasBeatmapLoaded
                && PreviewState.PreviewBeatmap.Value!.BeatmapInfo.Hash == working.BeatmapInfo.Hash
                && PreviewState.Ruleset.Value?.ShortName == ruleset.ShortName
                && EzBeatmapPreviewModes.IsManiaRuleset(PreviewState.Ruleset.Value))
                return;

            PreviewState.SetBeatmap(working, ruleset);
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
            PreviewState.SetBeatmap(picked, ruleset);
            BeatmapPreviewRequested?.Invoke(ruleset, picked);
        }

        private void applyPreviewAudioPolicy()
        {
            // 游玩音频由插拔进来的 Player 持有；编辑器打开时停掉选歌全局轨，避免双音。
            if (hudTargetScreen is Player)
                musicController.Stop();
        }

        private void applyCurrentScene()
        {
            // 游玩洞常驻插拔屏；场景只换侧栏栏目，不重建中间前景。
            sidebar.ShowScene(EzSkinEditorSceneRegistry.Get(sceneBar.CurrentScene.Value));
            updateHudInteraction();
            hudLayer.Apply(sceneBar.CurrentScene.Value, hudTargetScreen, layoutLayer, sceneBar.ActiveLayer.Value);
        }

        private void applySettings()
        {
            if (!EzSkinEditorHostScope.CanWriteSkinLayout)
                return;

            ensureSkinIniSession();
            ensureSkinJsonSession();

            bool configDirty = hasUnsavedConfigChanges();
            bool iniDirty = SkinIniSession.IsDirty;
            bool jsonDirty = SkinJsonSession is { IsSupported: true, HasFile: true } && SkinJsonSession.IsDirty(ezSkinConfig);
            bool layoutDirty = hudLayer.HasPendingSkinLayoutSave;

            if ((iniDirty || jsonDirty || layoutDirty)
                && needsMutableSkinBeforeWrite(iniDirty, jsonDirty, layoutDirty)
                && !ensureMutableSkinPreservingWorkspaceDrafts(iniDirty, jsonDirty))
            {
                postNotification(EzEditorStrings.NOTIFY_SKIN_LAYOUT_EXIT_SAVE_FAILED);
                return;
            }

            ezSkinConfig.Save();
            hudLayer.PersistAllLayouts();

            if (jsonDirty)
                SkinJsonSession!.Commit(ezSkinConfig);

            if (iniDirty)
                SkinIniSession.Commit();

            recaptureAppliedConfigBaseline();

            if (!configDirty && !iniDirty && !jsonDirty)
                return;

            refreshScene();
            invalidateResourceCaches();

            if (iniDirty)
                skinManager.CurrentSkinInfo.TriggerChange();

            refreshScene();
        }

        private void undo()
        {
            hudLayer.RestoreState(-1);
            menuBar.RefreshMenuState();
        }

        private void redo()
        {
            hudLayer.RestoreState(1);
            menuBar.RefreshMenuState();
        }

        private void cut()
        {
            hudLayer.CutSelection();
            menuBar.RefreshMenuState();
        }

        private void copy()
        {
            hudLayer.CopySelection();
            menuBar.RefreshMenuState();
        }

        private void paste()
        {
            hudLayer.PasteClipboard();
            menuBar.RefreshMenuState();
        }

        private void clone()
        {
            hudLayer.CloneSelection();
            menuBar.RefreshMenuState();
        }

        public bool OnPressed(KeyBindingPressEvent<PlatformAction> e)
        {
            if (e.Repeat)
                return false;

            if (e.Action == PlatformAction.Save)
            {
                applySettings();
                return true;
            }

            if (!hudLayer.InteractionEnabled.Value)
                return false;

            switch (e.Action)
            {
                case PlatformAction.Cut:
                    cut();
                    return true;

                case PlatformAction.Copy:
                    copy();
                    return true;

                case PlatformAction.Paste:
                    paste();
                    return true;

                case PlatformAction.Undo:
                    undo();
                    return true;

                case PlatformAction.Redo:
                    redo();
                    return true;
            }

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<PlatformAction> e)
        {
        }

        public bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (e.Repeat || !hudLayer.InteractionEnabled.Value)
                return false;

            if (e.Action == GlobalAction.EditorCloneSelection)
            {
                clone();
                return true;
            }

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e)
        {
        }

        private void commitSkinIni()
        {
            if (SkinIniSession is not { IsSupported: true, IsDirty: true })
                return;

            if (needsMutableSkinBeforeWrite(iniDirty: true, jsonDirty: false, layoutDirty: false)
                && !ensureMutableSkinPreservingWorkspaceDrafts(iniDirty: true, jsonDirty: false))
                return;

            SkinIniSession.Commit();
            refreshScene();
            invalidateResourceCaches();
            refreshScene();
        }

        // 用户可能在我们开着编辑器时往 EzResources 里丢/换图。须先 refresh 让旧预览 Drawable 下树，再 ClearCache 动画池，最后再 refresh 取新图。
        private void invalidateResourceCaches() => resource.InvalidateResourceCaches();

        private bool isSkinIniSupported() => EzSkinIniSupport.IsSupported(skinManager.CurrentSkinInfo.Value);

        private void ensureSkinSpecificSceneVisibility()
        {
            // 左栏 SkinSpecific 为占位壳，按钮已隐藏；专用设置在 Appearance 侧栏「皮肤专用」栏。
            sceneRail.SetSceneVisible(EzEditorSceneType.SkinSpecific, false);
        }

        private static bool usesLegacySkinFolder(Live<SkinInfo> live) =>
            live.PerformRead(s => EzLegacySkinFolders.TryGetDirectory(s, out _));

        private bool needsMutableSkinBeforeWrite(bool iniDirty, bool jsonDirty, bool layoutDirty)
        {
            if (usesLegacySkinFolder(skinManager.CurrentSkinInfo.Value))
                return false;

            if (layoutDirty)
                return true;

            if (iniDirty && SkinIniSession.IsSupported)
                return true;

            if (jsonDirty)
                return true;

            if (SkinJsonSession is { HasFile: true })
                return true;

            return skinManager.CurrentSkinInfo.Value.PerformRead(s => s.Protected);
        }

        /// <summary>
        /// Mirrors official skin editor: built-in protected skins are copied before the first write.
        /// Preserves in-memory workspace drafts when <see cref="SkinManager.EnsureMutableSkin"/> switches the active skin.
        /// </summary>
        private bool ensureMutableSkinPreservingWorkspaceDrafts(bool iniDirty, bool jsonDirty)
        {
            var live = skinManager.CurrentSkinInfo.Value;

            if (usesLegacySkinFolder(live))
                return true;

            if (!live.PerformRead(s => s.Protected))
                return live.IsManaged;

            string? iniDraft = iniDirty ? SkinIniSession.DraftText : null;

            if (!skinManager.EnsureMutableSkin())
                return false;

            if (iniDraft != null)
            {
                SkinIniSession.LoadFromSkin(skinManager.CurrentSkinInfo.Value);
                SkinIniSession.SetDraftText(iniDraft);
            }
            else
                ensureSkinIniSession();

            ensureSkinJsonSession();
            return true;
        }

        private void ensureSkinIniSession()
        {
            var currentSkin = skinManager.CurrentSkinInfo.Value;

            if (!isSkinIniSupported())
            {
                if (SkinIniSession.IsDirty)
                    SkinIniSession.Discard();

                SkinIniSession.LoadFromSkin(currentSkin);
                sceneRail.SetSceneVisible(EzEditorSceneType.SkinIni, false);

                if (sceneBar.CurrentScene.Value == EzEditorSceneType.SkinIni)
                    sceneBar.CurrentScene.Value = EzEditorSceneType.Appearance;

                return;
            }

            sceneRail.SetSceneVisible(EzEditorSceneType.SkinIni, true);

            Guid currentSkinId = currentSkin.ID;

            if (SkinIniSession.SkinId == currentSkinId && SkinIniSession.IsSupported)
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

            if (needsMutableSkinBeforeWrite(iniDirty: false, jsonDirty: false, layoutDirty: false)
                && !ensureMutableSkinPreservingWorkspaceDrafts(iniDirty: false, jsonDirty: false))
            {
                postNotification(EzEditorStrings.NOTIFY_CANNOT_CREATE_EZSKIN_JSON);
                return;
            }

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

            bool jsonDirty = SkinJsonSession is { HasFile: true } && SkinJsonSession.IsDirty(ezSkinConfig);

            if (jsonDirty
                && needsMutableSkinBeforeWrite(iniDirty: false, jsonDirty: true, layoutDirty: false)
                && !ensureMutableSkinPreservingWorkspaceDrafts(iniDirty: false, jsonDirty: true))
            {
                postNotification(EzEditorStrings.NOTIFY_CANNOT_UPDATE_EZSKIN_JSON_SNAPSHOT);
                return;
            }

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

            if (SkinJsonSession is { HasFile: true }
                && needsMutableSkinBeforeWrite(iniDirty: false, jsonDirty: false, layoutDirty: false)
                && !ensureMutableSkinPreservingWorkspaceDrafts(iniDirty: false, jsonDirty: false))
            {
                postNotification(EzEditorStrings.NOTIFY_CANNOT_REMOVE_EZSKIN_JSON);
                return;
            }

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

        private bool canImportExportCurrentLayoutLayer() =>
            EzSkinEditorSceneRegistry.SupportsHudLayer(sceneBar.CurrentScene.Value)
            && hudLayer.ActiveTarget != null;

        private void exportLayoutPack()
        {
            try
            {
                var exportStorage = (storage as OsuStorage)?.GetExportStorage() ?? storage.GetStorageForDirectory(@"exports");
                string path = EzLayoutPackIO.ExportPack(storage, layoutThemeHost.CurrentSetName, exportStorage);
                postNotification(LocalisableString.Format(EzEditorStrings.NOTIFY_EXPORTED_TO, path));

                if (RuntimeInfo.IsDesktop)
                    host.OpenFileExternally(path + Path.DirectorySeparatorChar);
            }
            catch (Exception e)
            {
                postNotification(LocalisableString.Format(EzEditorStrings.NOTIFY_EXPORT_FAILED, e.Message));
            }
        }

        private void importLayoutPack()
        {
            if (dialogOverlay == null)
            {
                postNotification(EzEditorStrings.NOTIFY_FILE_SELECTOR_NOT_SUPPORTED);
                return;
            }

            dialogOverlay.Push(new EzLayoutThemeImportDialog(beginLayoutPackImport));
        }

        private void beginLayoutPackImport(string themeSuffix)
        {
            if (string.IsNullOrWhiteSpace(themeSuffix))
            {
                postNotification(EzEditorStrings.NOTIFY_LAYOUT_THEME_NAME_REQUIRED);
                return;
            }

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
                    string? directory = Path.GetDirectoryName(file.FullName);

                    if (string.IsNullOrEmpty(directory))
                        throw new InvalidOperationException("Missing directory");

                    string setName = EzLayoutPackIO.ImportPackFromDirectory(storage, directory, themeSuffix);
                    EzLayoutSetCandidates.Refresh(storage);
                    layoutThemeHost.SelectSet(setName);
                    postNotification(EzEditorStrings.NOTIFY_LAYOUT_IMPORTED_PACK);
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

        private void exportCurrentLayoutLayer()
        {
            var target = hudLayer.ActiveTarget;

            if (target == null)
            {
                postNotification(EzEditorStrings.NOTIFY_LAYOUT_NO_CURRENT_LAYER);
                return;
            }

            try
            {
                hudLayer.PersistLayoutToCurrentSkin();
                var exportStorage = (storage as OsuStorage)?.GetExportStorage() ?? storage.GetStorageForDirectory(@"exports");
                string path = target is EzLayoutContainer ezLayout
                    ? EzLayoutPackIO.ExportLayerJson(storage, layoutThemeHost.Store, ezLayout.LayoutFile, exportStorage)
                    : EzSkinLayoutIO.ExportLayerJson(target, skinManager.CurrentSkin.Value, exportStorage);
                postNotification(LocalisableString.Format(EzEditorStrings.NOTIFY_EXPORTED_TO, path));
            }
            catch (Exception e)
            {
                postNotification(LocalisableString.Format(EzEditorStrings.NOTIFY_EXPORT_FAILED, e.Message));
            }
        }

        private void importCurrentLayoutLayer()
        {
            var target = hudLayer.ActiveTarget;

            if (target == null)
            {
                postNotification(EzEditorStrings.NOTIFY_LAYOUT_NO_CURRENT_LAYER);
                return;
            }

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
                    if (target is EzLayoutContainer ezLayout)
                    {
                        EzLayoutPackIO.ImportLayerJson(storage, layoutThemeHost.Store, ezLayout.LayoutFile, file.FullName);
                        ezLayout.LoadFromStore();
                    }
                    else
                        EzSkinLayoutIO.ImportLayerJson(target, skinManager, file.FullName);

                    hudLayer.ChangeHandler?.SaveState();
                    postNotification(EzEditorStrings.NOTIFY_LAYOUT_IMPORTED_LAYER);
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
                    refreshScene();
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

        private void onLayoutWritten()
        {
            onScreenDisplay?.Display(new OfficialSkinEditor.SkinEditorToast(
                ToastStrings.SkinSaved,
                skinManager.CurrentSkin.Value.SkinInfo.ToString() ?? "Unknown"));
        }

        private void onCurrentSkinChanged(ValueChangedEvent<Skin> skin)
        {
            if (skin.OldValue != null && skin.NewValue.SkinInfo.Value.ID != skin.OldValue.SkinInfo.Value.ID)
                hudLayer.FlushPendingForSkin(skin.OldValue);

            if (skin.OldValue != null && skin.NewValue.SkinInfo.Value.ID != skin.OldValue.SkinInfo.Value.ID)
                persistEditorConfig();

            Schedule(refreshScene);
        }

        private bool flushWorkspaceLayouts() => hudLayer.PersistAllLayouts();

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

        private void ensureAppliedConfigBaselineForCurrentSkin()
        {
            var currentSkinId = skinManager.CurrentSkinInfo.Value.ID;

            if (appliedConfigBaselineInitialized && appliedConfigBaselineSkinId == currentSkinId)
                return;

            recaptureAppliedConfigBaseline();
            appliedConfigBaselineInitialized = true;
            appliedConfigBaselineSkinId = currentSkinId;
        }

        private void recaptureAppliedConfigBaseline()
        {
            appliedConfigBaseline.CaptureFrom(ezSkinConfig, SkinIniSession);
            appliedConfigBaseline.SyncBindableDefaults(ezSkinConfig);
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
        }

        private void restoreNoteSnapshot()
        {
            NoteSnapshotForTesting.ApplyTo(NoteSessionForTesting);
            postNotification(EzEditorStrings.NOTIFY_RESTORED_NOTE_SNAPSHOT);
        }

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
                    string? directory = null;

                    skinInfo.PerformRead(s =>
                    {
                        if (EzLegacySkinFolders.TryGetDirectory(s, out string folder))
                            directory = folder;
                    });

                    if (directory == null && ScriptedSkinSupport.IsScriptedSkin(skinInfo))
                        directory = skinManager.GetScriptDirectory(skinInfo.Value);

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

        public void ShowExitDialog() => RequestCloseFromUser();

        private bool hasUnsavedWorkspaceChanges() =>
            SkinIniSession.IsDirty
            || hasUnsavedConfigChanges()
            || (hudLayer.ChangeHandler?.CanUndo.Value ?? false)
            || hudLayer.HasPendingSkinLayoutSave
            || (SkinJsonSession?.IsDirty(ezSkinConfig) ?? false);

        private bool hasUnsavedConfigChanges()
        {
            if (!appliedConfigBaselineInitialized)
                return false;

            string current = EzSkinJsonBridge.CreateNormalizedSnapshot(ezSkinConfig);
            string baseline = EzSkinJsonDocument.SerializeNormalized(appliedConfigBaseline.Document);
            return current != baseline;
        }

        private void discardWorkspaceChanges()
        {
            if (SkinIniSession.IsDirty)
                SkinIniSession.Discard();

            if (hasUnsavedConfigChanges())
            {
                appliedConfigBaseline.ApplyConfigTo(ezSkinConfig);
                appliedConfigBaseline.ApplyTo(ezSkinConfig, SkinIniSession);
            }

            hudLayer.DiscardPendingSkinLayoutSave();
            skinManager.CurrentSkinInfo.TriggerChange();
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

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                foreach (var strategy in EzSkinEditorSceneRegistry.All)
                    strategy.ReleaseCachedDrawables();
            }

            base.Dispose(isDisposing);
        }
    }
}

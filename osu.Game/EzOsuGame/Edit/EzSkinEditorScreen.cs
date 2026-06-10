// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Extensions;
using osu.Framework.Testing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Edit.Components;
using osu.Game.EzOsuGame.Edit.Note;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics.Cursor;
using osu.Game.IO;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;
using osu.Game.Overlays.Notifications;
using osu.Game.Overlays.SkinEditor;
using osu.Game.Rulesets;
using osu.Game.Screens;
using osu.Game.Skinning;

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
    /// Ez skin editor screen with menu bar, scene bar, scene content and toolbox-style settings sidebar.
    /// Scene switching is driven by <see cref="IEzSkinEditorSceneStrategy"/> — not by sidebar groups.
    /// </summary>
    public partial class EzSkinEditorScreen : OsuScreen
    {
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

        private Container? backgroundContainer;
        private Container sceneContentHost = null!;
        private EzSkinEditorMenuBar menuBar = null!;
        private EzSkinEditorSceneBar sceneBar = null!;
        private EzSkinEditorSidebar sidebar = null!;
        private EzSkinEditorTopToolbar topToolbar = null!;

        private EzSkinEditorBeatmapPicker? beatmapPicker;

        private bool attemptedGlobalBeatmapPreview;
        private bool comparisonSnapshotInitialized;
        private Guid comparisonSnapshotSkinId;
        private bool noteSnapshotInitialized;
        private Guid noteSnapshotSkinId;
        private bool configPreviewRefreshBound;

        private ISkinEditorVirtualProvider? provider;
        private EzSkinEditorSceneContext? sceneContext;

        private EzSkinEditorEmbeddedPlayer? embeddedPlayer;
        private string embeddedPlayerBeatmapHash = string.Empty;
        private int embeddedPlayerRulesetId;
        private Guid embeddedPlayerSkinId;
        private double lastSceneBarProgressDisplayTime = double.MinValue;
        private Bindable<EzBeatmapPreviewMode>? sceneBarPreviewModeBindable;

        public Bindable<EzSkinEditorSceneType> CurrentScene => sceneBar.CurrentScene;

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
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
        }

        public override void OnEntering(ScreenTransitionEvent e)
        {
            base.OnEntering(e);
            Schedule(refreshScene);
            this.FadeInFromZero(200, Easing.OutQuint);
        }

        public override void OnResuming(ScreenTransitionEvent e)
        {
            base.OnResuming(e);
            Schedule(refreshScene);
        }

        public override bool OnExiting(ScreenExitEvent e)
        {
            persistEditorConfig();
            this.FadeOut(200, Easing.OutQuint);
            return base.OnExiting(e);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
                backgroundContainer = new Container { RelativeSizeAxes = Axes.Both },
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
                                        Height = SkinEditor.MENU_HEIGHT,
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
                                            topToolbar = new EzSkinEditorTopToolbar
                                            {
                                                PreviewSource = PreviewState.Source,
                                                PreviewMode = PreviewState.Mode,
                                                ToggleBeatmapPlaybackRequested = toggleBeatmapPlayback,
                                                BeatmapPreviewRequested = selectBeatmapPreview,
                                            },
                                        },
                                    },
                                },
                                new Drawable[]
                                {
                                    sceneBar = new EzSkinEditorSceneBar
                                    {
                                        RelativeSizeAxes = Axes.X,
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
                                                sceneContentHost = new Container { RelativeSizeAxes = Axes.Both, Masking = true },
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
            bindConfigPreviewRefresh();
            bindSceneBarPlayback();
        }

        protected override void Update()
        {
            base.Update();

            var source = getActiveScenePlaybackSource();

            if (source == null || !source.IsPlaying)
                return;

            if (source.CurrentTime - lastSceneBarProgressDisplayTime < 16)
                return;

            sceneBar.PlaybackControls.SetCurrentTime(source.CurrentTime);
            lastSceneBarProgressDisplayTime = source.CurrentTime;
        }

        private void bindSceneBarPlayback()
        {
            var controls = sceneBar.PlaybackControls;
            controls.OnSeek = seekSceneBarPlayback;
            controls.OnPlayStateChanged = setSceneBarPlaybackPlaying;

            sceneBarPreviewModeBindable = PreviewState.Mode.GetBoundCopy();
            sceneBarPreviewModeBindable.BindValueChanged(mode =>
            {
                bool playing = mode.NewValue == EzBeatmapPreviewMode.Dynamic;
                getActiveScenePlaybackSource()?.SetPlaying(playing);
                sceneBar.PlaybackControls.SetPlaying(playing);
            }, true);
        }

        private void seekSceneBarPlayback(double time) => getActiveScenePlaybackSource()?.Seek(time);

        private void setSceneBarPlaybackPlaying(bool playing)
        {
            if (playing)
                PreviewState.ResumeBeatmapPlayback();
            else
                PreviewState.PauseBeatmapPlayback();
        }

        private void refreshSceneBarPlayback()
        {
            var controls = sceneBar.PlaybackControls;
            var source = getActiveScenePlaybackSource();

            if (source == null)
            {
                controls.Alpha = 0;
                return;
            }

            controls.Alpha = 1;
            controls.SetRange(source.BeatmapMinTime, source.BeatmapMaxTime);
            controls.SetCurrentTime(source.CurrentTime);
            controls.SetPlaying(source.IsPlaying);
        }

        private IEzSkinEditorScenePlaybackSource? getActiveScenePlaybackSource()
        {
            if (sceneBar.CurrentScene.Value == EzSkinEditorSceneType.Appearance)
                return embeddedPlayer is { CanBeMounted: true } player ? player : null;

            if (sceneBar.CurrentScene.Value is EzSkinEditorSceneType.Size or EzSkinEditorSceneType.Colour)
                return sceneContentHost.ChildrenOfType<IEzSkinEditorScenePlaybackSource>().FirstOrDefault(s => s.IsActive);

            return null;
        }

        private void onSceneChanged(ValueChangedEvent<EzSkinEditorSceneType> change)
        {
            if (change.NewValue == EzSkinEditorSceneType.SkinIni && !isSkinIniSupported())
            {
                sceneBar.CurrentScene.Value = EzSkinEditorSceneType.Appearance;
                return;
            }

            if (change.OldValue != change.NewValue)
                persistEditorConfig();

            Schedule(applyCurrentScene);
        }

        public void PopulateSettings() => refreshScene();

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

            if (sceneBar.CurrentScene.Value == EzSkinEditorSceneType.Appearance)
            {
                backgroundContainer!.Child = createManiaStageBackgroundOrNull() ?? new Container { RelativeSizeAxes = Axes.Both };
                backgroundContainer.Child.RelativeSizeAxes = Axes.Both;
            }
            else
            {
                disposeEmbeddedPlayer();
                backgroundContainer!.Child = createManiaStageBackgroundOrNull() ?? new Container { RelativeSizeAxes = Axes.Both };
                backgroundContainer.Child.RelativeSizeAxes = Axes.Both;
            }

            ensureSkinIniSession();
            ensureSkinJsonSession();
            ensureComparisonSnapshotForCurrentSkin();
            ensureNoteSnapshotForCurrentSkin();

            sceneContext = buildSceneContext();
            applyCurrentScene();

            if (sceneBar.CurrentScene.Value == EzSkinEditorSceneType.Appearance)
                mountAppearanceEmbeddedPlayer();

            Schedule(refreshSceneBarPlayback);
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

            PreviewState.SetBeatmap(gameBeatmap.Value, ruleset, EzSkinEditorPreviewModes.GetAppearanceLoadMode(ruleset));
        }

        private void toggleBeatmapPlayback()
        {
            if (!PreviewState.HasBeatmapLoaded)
                return;

            if (PreviewState.Mode.Value == EzBeatmapPreviewMode.Dynamic)
                PreviewState.PauseBeatmapPlayback();
            else
                PreviewState.ResumeBeatmapPlayback();
        }

        private void selectBeatmapPreview(RulesetInfo ruleset)
        {
            if (beatmapPicker == null)
                return;

            if (!beatmapPicker.TryPickRandom(ruleset, out var workingBeatmap))
            {
                notifications?.Post(new SimpleNotification
                {
                    Text = EzEditorStrings.NOTIFY_BEATMAP_NOT_FOUND,
                });
                return;
            }

            PreviewState.SetBeatmap(workingBeatmap!, ruleset, EzSkinEditorPreviewModes.GetAppearanceLoadMode(ruleset));
            refreshScene();
        }

        private EzSkinEditorSceneContext buildSceneContext()
        {
            var currentScene = sceneBar.CurrentScene.Value;

            bool allowBeatmapPreview = currentScene == EzSkinEditorSceneType.Appearance
                                       && PreviewState.Source.Value == EzSkinEditorPreviewSource.Beatmap;

            bool useVirtualComparisonPreview = currentScene is EzSkinEditorSceneType.Size or EzSkinEditorSceneType.Colour;
            bool useNoteComparisonOnly = currentScene == EzSkinEditorSceneType.Note;

            // Size/colour scenes always use the mania LN virtual provider, not the loaded beatmap ruleset.
            var previewBeatmap = allowBeatmapPreview
                ? PreviewState.PreviewBeatmap?.Beatmap
                : null;

            SkinEditorProviderResolver.EnsureDefaultProviderRegistered();
            provider = SkinEditorProviderResolver.Resolve(previewBeatmap);

            bool usesEzNoteVariants = EzSkinEditorNoteVariantResolver.UsesEzVariants(skinManager.CurrentSkin.Value);

            return new EzSkinEditorSceneContext
            {
                Provider = provider,
                EditorSkin = getEditorSkin(),
                ActualSkin = skinManager.CurrentSkin.Value,
                UsesEzNoteVariants = usesEzNoteVariants,
                NoteComparisonSource = buildNoteComparisonSource(useNoteComparisonOnly, useVirtualComparisonPreview, usesEzNoteVariants),
                NoteCompareKind = NoteCompareKind,
                SkinIniSession = SkinIniSession,
                SkinJsonSession = SkinJsonSession,
                PreviewState = PreviewState,
                RequestSceneRefresh = refreshScene,
                RequestPreviewRefresh = () => Schedule(refreshPreviewContent),
                CommitSkinIni = commitSkinIni,
                AllowBeatmapPreview = allowBeatmapPreview,
                UseVirtualComparisonPreview = useVirtualComparisonPreview,
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
                GetEmbeddedPlayer = () => embeddedPlayer,
            };
        }

        private void mountAppearanceEmbeddedPlayer()
        {
            var workingBeatmap = resolveAppearanceWorkingBeatmap();
            var ruleset = resolveAppearanceRuleset(workingBeatmap);

            if (embeddedPlayer != null && !embeddedPlayer.CanBeMounted)
            {
                embeddedPlayer = null;
                embeddedPlayerBeatmapHash = string.Empty;
                embeddedPlayerRulesetId = -1;
                embeddedPlayerSkinId = Guid.Empty;
            }

            if (workingBeatmap == null || ruleset == null)
            {
                disposeEmbeddedPlayer();
                return;
            }

            string beatmapHash = workingBeatmap.BeatmapInfo.Hash;
            int rulesetId = ruleset.OnlineID;
            var skinId = skinManager.CurrentSkinInfo.Value.ID;

            if (embeddedPlayer != null
                && embeddedPlayerBeatmapHash == beatmapHash
                && embeddedPlayerRulesetId == rulesetId
                && embeddedPlayerSkinId == skinId)
            {
                applyEmbeddedPlayerToAppearanceContent();
                return;
            }

            disposeEmbeddedPlayer();

            ensureAppearanceBeatmapTrackLoaded(workingBeatmap);

            embeddedPlayer = new EzSkinEditorEmbeddedPlayer(workingBeatmap, ruleset, getEditorSkin());
            embeddedPlayerBeatmapHash = beatmapHash;
            embeddedPlayerRulesetId = rulesetId;
            embeddedPlayerSkinId = skinId;

            applyEmbeddedPlayerToAppearanceContent();

            bool shouldPlay = PreviewState.Mode.Value == EzBeatmapPreviewMode.Dynamic;
            Schedule(() =>
            {
                embeddedPlayer?.SetPlaying(shouldPlay);
                refreshSceneBarPlayback();
            });
        }

        private T? getSceneContent<T>() where T : Drawable
            => sceneContentHost.Children.Count == 1 ? sceneContentHost.Child as T : null;

        private void applyEmbeddedPlayerToAppearanceContent()
        {
            if (getSceneContent<EzSkinEditorAppearanceSceneContent>() is EzSkinEditorAppearanceSceneContent appearance)
                appearance.SetEmbeddedPlayer(embeddedPlayer);
        }

        private void ensureAppearanceBeatmapTrackLoaded(WorkingBeatmap workingBeatmap)
        {
            if (workingBeatmap.TrackLoaded)
                return;

            if (gameBeatmap.Value is WorkingBeatmap current && current.TryTransferTrack(workingBeatmap))
                return;

            workingBeatmap.LoadTrack();
        }

        private WorkingBeatmap? resolveAppearanceWorkingBeatmap()
        {
            if (PreviewState.PreviewBeatmap is WorkingBeatmap preview)
                return preview;

            if (EzSkinEditorBeatmapPicker.CanUseAsPreview(gameBeatmap.Value))
                return gameBeatmap.Value;

            return null;
        }

        private RulesetInfo? resolveAppearanceRuleset(WorkingBeatmap? workingBeatmap) =>
            PreviewState.Ruleset.Value ?? workingBeatmap?.BeatmapInfo.Ruleset;

        private void disposeEmbeddedPlayer()
        {
            embeddedPlayer?.Expire();
            embeddedPlayer = null;
            embeddedPlayerBeatmapHash = string.Empty;
            embeddedPlayerRulesetId = -1;
            embeddedPlayerSkinId = Guid.Empty;

            applyEmbeddedPlayerToAppearanceContent();
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

            string variantId = profile.GetDefaultVariantId(usesEzNoteVariants, EzSkinEditorNotePart.Note);

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

            var strategy = EzSkinEditorSceneRegistry.Get(sceneBar.CurrentScene.Value);

            if (sceneBar.CurrentScene.Value == EzSkinEditorSceneType.Appearance
                && getSceneContent<EzSkinEditorAppearanceSceneContent>() is EzSkinEditorAppearanceSceneContent appearance)
            {
                appearance.RefreshFromContext(sceneContext);
            }
            else
            {
                detachEmbeddedPlayerFromHierarchy();
                sceneContentHost.Child = strategy.CreateSceneContent(sceneContext);
            }

            sidebar.ApplyStrategy(strategy, sceneContext);
        }

        private void detachEmbeddedPlayerFromHierarchy()
            => embeddedPlayer?.DetachForRemount();

        private void applySettings()
        {
            ezSkinConfig.Save();

            if (SkinIniSession is { IsSupported: true, IsDirty: true })
                SkinIniSession.Commit();

            skinManager.CurrentSkinInfo.TriggerChange();
            refreshScene();
        }

        private void commitSkinIni()
        {
            if (SkinIniSession is not { IsSupported: true, IsDirty: true })
                return;

            SkinIniSession.Commit();
            refreshScene();
        }

        private bool isSkinIniSupported() => EzSkinIniSupport.IsSupported(skinManager.CurrentSkinInfo.Value);

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

        private void refreshNoteComparisonSnapshotPane()
        {
            if (tryGetNoteComparisonHost(out var noteHost))
                noteHost.RefreshSnapshotPane();
            else
                Schedule(refreshPreviewContent);
        }

        private void refreshNoteComparisonLivePane()
        {
            if (tryGetNoteComparisonHost(out var noteHost))
                noteHost.UpdateContext(buildSceneContext());
            else
                Schedule(refreshPreviewContent);
        }

        private bool tryGetNoteComparisonHost(out EzSkinEditorNoteComparisonHost noteHost)
        {
            if (getSceneContent<EzSkinEditorNoteComparisonHost>() is EzSkinEditorNoteComparisonHost directHost)
            {
                noteHost = directHost;
                return true;
            }

            if (getSceneContent<EzSkinEditorPreviewHost>() is EzSkinEditorPreviewHost previewHost
                && previewHost.ChildrenOfType<EzSkinEditorNoteComparisonHost>().FirstOrDefault() is EzSkinEditorNoteComparisonHost nestedHost)
            {
                noteHost = nestedHost;
                return true;
            }

            noteHost = null!;
            return false;
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
                postNotification(EzEditorStrings.PLACEHOLDER_NOTE_RULESET_NOT_SUPPORTED);
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
                    var detached = skinInfo.PerformRead(s => s.Detach());
                    var edit = await skinManager.BeginExternalEditing(detached).ConfigureAwait(false);
                    string directory = edit.MountedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                    using var image = EzSkinEditorNoteImageExporter.Export(profile, request, directory);

                    if (image == null)
                    {
                        await edit.Finish().ConfigureAwait(false);
                        Schedule(() => postNotification(EzEditorStrings.NOTIFY_CANNOT_EXPORT_NOTE_PREVIEW));
                        return;
                    }

                    string filePath = Path.Combine(directory, $"{exportName}.png");
                    await image.SaveAsPngAsync(filePath).ConfigureAwait(false);

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

            foreach (var setting in EzSkinJsonSettingCatalog.All)
                EzSkinJsonBridge.BindSettingValueChanged(ezSkinConfig, setting, () => Schedule(refreshPreviewContent));
        }

        private void refreshPreviewContent()
        {
            if (!IsLoaded)
                return;

            sceneContext = buildSceneContext();

            if (tryGetNoteComparisonHost(out var noteHost))
            {
                noteHost.UpdateContext(sceneContext);
                return;
            }

            if (getSceneContent<EzSkinEditorAppearanceSceneContent>() is EzSkinEditorAppearanceSceneContent appearanceContent)
            {
                appearanceContent.RefreshFromContext(sceneContext);
                applyEmbeddedPlayerToAppearanceContent();
                refreshSceneBarPlayback();
                return;
            }

            var strategy = EzSkinEditorSceneRegistry.Get(sceneBar.CurrentScene.Value);
            sceneContentHost.Child = strategy.CreateSceneContent(sceneContext);
            Schedule(refreshSceneBarPlayback);
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

        private void tryExit()
        {
            if (this.IsCurrentScreen())
                ShowExitDialog();
        }

        public void ShowExitDialog()
        {
            if (!this.IsCurrentScreen())
                return;

            if (dialogOverlay == null)
            {
                this.Exit();
                return;
            }

            if (SkinIniSession?.IsDirty != true)
            {
                this.Exit();
                return;
            }

            dialogOverlay.Push(new ConfirmDialog(EzEditorStrings.EXIT_CONFIRM_APPLY_TO_SKIN, () =>
            {
                applySettings();

                if (this.IsCurrentScreen())
                    this.Exit();
            }, () =>
            {
                SkinIniSession?.Discard();

                if (this.IsCurrentScreen())
                    this.Exit();
            }));
        }

        public void PresentGameplay()
        {
            if (sceneBar.CurrentScene.Value != EzSkinEditorSceneType.Appearance)
                return;

            mountAppearanceEmbeddedPlayer();
            embeddedPlayer?.Restart();
            refreshPreviewContent();
        }

        private static Drawable? createManiaStageBackgroundOrNull()
        {
            var lookup = tryCreateManiaSkinComponentLookupOrNull("StageBackground");
            if (lookup == null)
                return null;

            return new SkinnableDrawable(lookup) { RelativeSizeAxes = Axes.Both };
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

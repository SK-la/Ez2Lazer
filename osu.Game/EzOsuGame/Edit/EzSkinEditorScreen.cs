// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Edit.Components;
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
    // M2: Saved/Draft session + Note/LN comparison preview
    // M3: Skin.ini editor + backup + save footer
    // M4: top-bar preview controls + static/beatmap scene backends
    // M5: EzSkin.json + advanced colouring + config menu (json/colour→ini)
    // M6: size→ini, .osk export

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

        private ISkinEditorVirtualProvider? provider;
        private EzSkinEditorSceneContext? sceneContext;

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
                                            CanCreateEzSkinJson = () => SkinJsonSession is { IsSupported: true, HasFile: false },
                                            CanUpdateEzSkinJsonSnapshot = () => SkinJsonSession is { IsSupported: true, HasFile: true }
                                                                                && SkinJsonSession.IsDirty(ezSkinConfig),
                                            CanRemoveEzSkinJson = () => SkinJsonSession is { IsSupported: true, HasFile: true },
                                            CanImportFromSkinJson = () => SkinJsonSession is { IsSupported: true, HasFile: true },
                                            CanWriteColoursToSkinIni = () => SkinIniSession is { IsSupported: true } && getCurrentKeyMode() > 0,
                                            CanWriteSizesToSkinIni = () => SkinIniSession is { IsSupported: true } && getCurrentKeyMode() > 0,
                                            CanExportOsk = canExportOsk,
                                        },
                                        topToolbar = new EzSkinEditorTopToolbar
                                        {
                                            PreviewSource = PreviewState.Source,
                                            StaticPreviewRequested = selectStaticPreview,
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
                                            sceneContentHost = new Container { RelativeSizeAxes = Axes.Both },
                                            sidebar = new EzSkinEditorSidebar(),
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
            beatmapPicker = new EzSkinEditorBeatmapPicker(realm, beatmapManager);
            sceneBar.CurrentScene.BindValueChanged(onSceneChanged, true);
            skinManager.CurrentSkinInfo.BindValueChanged(onCurrentSkinInfoChanged);
        }

        private void onSceneChanged(ValueChangedEvent<EzSkinEditorSceneType> change)
        {
            if (change.NewValue == EzSkinEditorSceneType.SkinIni && !isSkinIniSupported())
            {
                sceneBar.CurrentScene.Value = EzSkinEditorSceneType.Appearance;
                return;
            }

            Schedule(applyCurrentScene);
        }

        public void PopulateSettings() => refreshScene();

        public void ApplySettings() => applySettings();

        internal void CreateEzSkinJsonForTesting() => createEzSkinJson();

        internal void WriteSizesToSkinIniForTesting() => writeSizesToSkinIni();

        private void refreshScene()
        {
            backgroundContainer!.Child = createManiaStageBackgroundOrNull() ?? new Container { RelativeSizeAxes = Axes.Both };
            backgroundContainer.Child.RelativeSizeAxes = Axes.Both;

            ensureSkinIniSession();
            ensureSkinJsonSession();

            sceneContext = buildSceneContext();
            applyCurrentScene();
            menuBar.RefreshMenuState();
        }

        private void selectStaticPreview()
        {
            PreviewState.SetStatic();
            refreshScene();
        }

        private void selectBeatmapPreview(RulesetInfo ruleset, EzBeatmapPreviewMode mode)
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

            PreviewState.SetBeatmap(workingBeatmap!, ruleset, EzSkinEditorPreviewModes.ValidateMode(mode, ruleset));
            refreshScene();
        }

        private EzSkinEditorSceneContext buildSceneContext()
        {
            var previewBeatmap = PreviewState.Source.Value == EzSkinEditorPreviewSource.Beatmap
                ? PreviewState.PreviewBeatmap?.Beatmap
                : null;

            provider = SkinEditorProviderResolver.Resolve(previewBeatmap);

            return new EzSkinEditorSceneContext
            {
                Provider = provider,
                EditorSkin = getEditorSkin(),
                SkinIniSession = SkinIniSession,
                SkinJsonSession = SkinJsonSession,
                PreviewState = PreviewState,
                RequestSceneRefresh = refreshScene,
                CommitSkinIni = commitSkinIni,
                PreviewSource = PreviewState.Source.Value,
                PreviewBeatmap = PreviewState.PreviewBeatmap,
                PreviewRuleset = PreviewState.Ruleset.Value,
                PreviewMode = PreviewState.Mode.Value,
            };
        }

        private void applyCurrentScene()
        {
            sceneContext = buildSceneContext();

            var strategy = EzSkinEditorSceneRegistry.Get(sceneBar.CurrentScene.Value);
            sceneContentHost.Child = strategy.CreateSceneContent(sceneContext);
            sidebar.ApplyStrategy(strategy, sceneContext);
        }

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
            Schedule(refreshScene);
        }

        private ISkin getEditorSkin()
        {
            var currentSkin = skinManager.CurrentSkin.Value;

            return currentSkin is EzStyleProSkin or Ez2Skin or SbISkin
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

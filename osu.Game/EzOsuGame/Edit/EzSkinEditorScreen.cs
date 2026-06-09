// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Screens;
using osu.Game.Database;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Edit.Components;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Cursor;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;
using osu.Game.Overlays.SkinEditor;
using osu.Game.Screens;
using osu.Game.Skinning;

namespace osu.Game.EzOsuGame.Edit
{
    // Milestone index — see EzSkinEditor refactor plan:
    // M1: layout shell + scene strategies + sidebar groups
    // M2: Saved/Draft session + Note/LN comparison preview
    // M3: Skin.ini editor + backup + save footer
    // M4: export, EzSkin.json, advanced colouring

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

        [Resolved(canBeNull: true)]
        private IDialogOverlay? dialogOverlay { get; set; }

        private Container? backgroundContainer;
        private Container sceneContentHost = null!;
        private EzSkinEditorMenuBar menuBar = null!;
        private EzSkinEditorSceneBar sceneBar = null!;
        private EzSkinEditorSidebar sidebar = null!;
        private OsuTextFlowContainer headerText = null!;

        private ISkinEditorVirtualProvider? provider;
        private EzSkinEditorSceneContext? sceneContext;

        public Bindable<EzSkinEditorSceneType> CurrentScene => sceneBar.CurrentScene;

        /// <summary>
        /// Per-skin skin.ini session. Skin-layer only.
        /// </summary>
        public EzSkinIniSession? SkinIniSession { get; private set; }

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
                                        },
                                        headerText = new OsuTextFlowContainer
                                        {
                                            TextAnchor = Anchor.TopRight,
                                            Padding = new MarginPadding(5),
                                            Anchor = Anchor.TopRight,
                                            Origin = Anchor.TopRight,
                                            AutoSizeAxes = Axes.X,
                                            RelativeSizeAxes = Axes.Y,
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
            sceneBar.CurrentScene.BindValueChanged(onSceneChanged, true);
            skinManager.CurrentSkinInfo.BindValueChanged(onCurrentSkinInfoChanged);
            updateHeaderText();
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

        private void refreshScene()
        {
            backgroundContainer!.Child = createManiaStageBackgroundOrNull() ?? new Container { RelativeSizeAxes = Axes.Both };
            backgroundContainer.Child.RelativeSizeAxes = Axes.Both;

            updateHeaderText();
            ensureSkinIniSession();

            sceneContext = buildSceneContext();
            applyCurrentScene();
        }

        private EzSkinEditorSceneContext buildSceneContext()
        {
            provider = SkinEditorProviderResolver.Resolve(Beatmap.Value?.Beatmap);

            return new EzSkinEditorSceneContext
            {
                Provider = provider,
                EditorSkin = getEditorSkin(),
                SkinIniSession = SkinIniSession,
                RequestSceneRefresh = refreshScene,
                CommitSkinIni = commitSkinIni,
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

        private void updateHeaderText()
        {
            headerText.Clear();
            headerText.AddText(@"Ez ", t => t.Font = OsuFont.TorusAlternate);
            headerText.AddText(@"Skin Editor", t =>
            {
                t.Font = OsuFont.TorusAlternate;
                t.Colour = colourProvider.Highlight1;
            });
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

            dialogOverlay.Push(new ConfirmDialog("应用更改到皮肤？", () =>
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

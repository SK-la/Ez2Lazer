// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Screens;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Edit.Components;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;
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
    /// Ez skin editor screen with menu bar, scene bar, scene content and collapsible settings sidebar.
    /// Scene switching is driven by <see cref="IEzSkinEditorSceneStrategy"/> — not by sidebar groups.
    /// </summary>
    public partial class EzSkinEditorScreen : OsuScreen
    {
        [Resolved]
        private SkinManager skinManager { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private IDialogOverlay? dialogOverlay { get; set; }

        private Container? backgroundContainer;
        private Container sceneContentHost = null!;
        private EzSkinEditorMenuBar menuBar = null!;
        private EzSkinEditorSceneBar sceneBar = null!;
        private EzSkinEditorSidebar sidebar = null!;

        private ISkinEditorVirtualProvider? provider;
        private EzSkinEditorSceneContext? sceneContext;

        public Bindable<EzSkinEditorSceneType> CurrentScene => sceneBar.CurrentScene;

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
                new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    RowDimensions = new[]
                    {
                        new Dimension(GridSizeMode.Absolute, EzSkinEditorMenuBar.HEIGHT),
                        new Dimension(GridSizeMode.Absolute, EzSkinEditorSceneBar.HEIGHT),
                        new Dimension(),
                    },
                    Content = new[]
                    {
                        new Drawable[] { menuBar = new EzSkinEditorMenuBar { ApplyAction = applySettings } },
                        new Drawable[] { sceneBar = new EzSkinEditorSceneBar() },
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
                                    new Drawable[] { sceneContentHost = new Container { RelativeSizeAxes = Axes.Both } },
                                    new Drawable[] { sidebar = new EzSkinEditorSidebar() },
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
            sceneBar.CurrentScene.BindValueChanged(_ => Schedule(applyCurrentScene), true);
        }

        public void PopulateSettings() => refreshScene();

        private void refreshScene()
        {
            backgroundContainer!.Child = createManiaStageBackgroundOrNull() ?? new Container { RelativeSizeAxes = Axes.Both };
            backgroundContainer.Child.RelativeSizeAxes = Axes.Both;

            provider = SkinEditorProviderResolver.Resolve(Beatmap.Value?.Beatmap);

            sceneContext = new EzSkinEditorSceneContext
            {
                Provider = provider,
                EditorSkin = getEditorSkin(),
                RequestSceneRefresh = refreshScene,
            };

            applyCurrentScene();
        }

        private void applyCurrentScene()
        {
            if (sceneContext == null)
                return;

            var strategy = EzSkinEditorSceneRegistry.Get(sceneBar.CurrentScene.Value);
            sceneContentHost.Child = strategy.CreateSceneContent(sceneContext);
            sidebar.ApplyStrategy(strategy, sceneContext);
        }

        private void applySettings()
        {
            // Milestone 2: commit draft to config and refresh preview.
            applyCurrentScene();
        }

        private ISkin getEditorSkin()
        {
            var currentSkin = skinManager.CurrentSkin.Value;

            return currentSkin is EzStyleProSkin or Ez2Skin or SbISkin
                ? currentSkin
                : new EzStyleProSkin(skinManager);
        }

        public void ShowExitDialog()
        {
            if (dialogOverlay == null)
            {
                this.Exit();
                return;
            }

            dialogOverlay.Push(new ConfirmDialog("应用更改到皮肤？", () =>
            {
                applySettings();
                this.Exit();
            }, () => this.Exit()));
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

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.UI;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osu.Game.Overlays.SkinEditor;
using osuTK;

namespace osu.Game.EzOsuGame.Edit.Components
{
    public partial class EzSkinEditorSceneBar : CompositeDrawable
    {
        public const float HEIGHT = SkinEditorSceneLibrary.HEIGHT;

        private const float padding = 10;

        public Bindable<EzSkinEditorSceneType> CurrentScene { get; } = new Bindable<EzSkinEditorSceneType>(EzSkinEditorSceneType.Appearance);

        public Action? OpenEzHudEditorRequested { get; set; }

        public BindableBool HudLayerVisible => hudLayerToggle.Active;

        private EzEditorHudLayerToggle hudLayerToggle = null!;

        private readonly Dictionary<EzSkinEditorSceneType, SceneTabButton> tabButtons = new Dictionary<EzSkinEditorSceneType, SceneTabButton>();

        public EzSkinEditorSceneBar()
        {
            RelativeSizeAxes = Axes.X;
            Height = HEIGHT;
            Masking = true;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider overlayColourProvider)
        {
            float gap = EzUIStyleV1.SCENE_BAR_GAP;

            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = overlayColourProvider.Background6,
                },
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
                            new FillFlowContainer
                            {
                                Name = @"Ez Scene library",
                                AutoSizeAxes = Axes.X,
                                RelativeSizeAxes = Axes.Y,
                                Spacing = new Vector2(EzUIStyleV1.SCENE_BAR_BUTTON_SPACING, 0),
                                Padding = new MarginPadding { Horizontal = padding, Vertical = gap },
                                Direction = FillDirection.Horizontal,
                                Children = buildSceneButtons()
                                           .Prepend(new OsuSpriteText
                                           {
                                               Text = EzEditorStrings.SCENE_LIBRARY_LABEL,
                                               Anchor = Anchor.CentreLeft,
                                               Origin = Anchor.CentreLeft,
                                               Margin = new MarginPadding { Horizontal = 10 },
                                           })
                                           .Append(new SkinEditorSceneLibrary.SceneButton
                                           {
                                               Text = EzEditorStrings.SCENE_EZ_HUD,
                                               Width = 110,
                                               Action = () => OpenEzHudEditorRequested?.Invoke(),
                                           })
                                           .ToArray(),
                            },
                            hudLayerToggle = new EzEditorHudLayerToggle
                            {
                                Margin = new MarginPadding { Left = padding, Top = gap, Right = padding },
                            },
                        },
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            CurrentScene.BindValueChanged(e => updateActiveTab(e.NewValue), true);
        }

        private IEnumerable<Drawable> buildSceneButtons() => EzSkinEditorSceneRegistry.All.Select(strategy =>
        {
            var button = new SceneTabButton(strategy.SceneType, strategy.TabTitle.ToString(), () => CurrentScene.Value = strategy.SceneType);
            tabButtons[strategy.SceneType] = button;
            return button;
        });

        public void SetSceneVisible(EzSkinEditorSceneType scene, bool visible)
        {
            if (!tabButtons.TryGetValue(scene, out var button))
                return;

            if (visible)
                button.Show();
            else
                button.Hide();
        }

        private void updateActiveTab(EzSkinEditorSceneType scene)
        {
            foreach (var (sceneType, button) in tabButtons)
                button.Active = sceneType == scene;
        }

        private partial class SceneTabButton : SkinEditorSceneLibrary.SceneButton
        {
            public EzSkinEditorSceneType SceneType { get; }

            public SceneTabButton(EzSkinEditorSceneType sceneType, string text, Action action)
            {
                SceneType = sceneType;
                Text = text;
                Action = action;
            }
        }
    }
}

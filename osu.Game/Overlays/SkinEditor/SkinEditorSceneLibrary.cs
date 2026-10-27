// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Screens;
using osu.Game.EzOsuGame.Edit.Components;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.UI;
using osu.Game.EzOsuGame.UserInterface;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation;
using osu.Game.Screens;
using osu.Game.Screens.Select;
using osuTK;

namespace osu.Game.Overlays.SkinEditor
{
    public partial class SkinEditorSceneLibrary : CompositeDrawable
    {
        public const float HEIGHT = EzUIStyleV1.SCENE_BAR_HEIGHT;

        private const float gap = EzUIStyleV1.SCENE_BAR_GAP;
        private const float padding = 10;

        public BindableBool HudLayerVisible => hudLayerToggle.Active;

        private EzEditorHudLayerToggle hudLayerToggle = null!;

        [Resolved]
        private IPerformFromScreenRunner? performer { get; set; }

        [Resolved]
        private SkinEditorOverlay? skinEditorOverlay { get; set; }

        public SkinEditorSceneLibrary()
        {
            Height = HEIGHT;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider overlayColourProvider)
        {
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
                            new OsuScrollContainer(Direction.Horizontal)
                            {
                                RelativeSizeAxes = Axes.Both,
                                Children = new Drawable[]
                                {
                                    new FillFlowContainer
                                    {
                                        Name = @"Scene library",
                                        AutoSizeAxes = Axes.X,
                                        RelativeSizeAxes = Axes.Y,
                                        Spacing = new Vector2(EzUIStyleV1.SCENE_BAR_BUTTON_SPACING, 0),
                                        Padding = new MarginPadding { Horizontal = padding, Vertical = gap },
                                        Direction = FillDirection.Horizontal,
                                        Children = new Drawable[]
                                        {
                                            new OsuSpriteText
                                            {
                                                Text = SkinEditorStrings.SceneLibrary,
                                                Anchor = Anchor.CentreLeft,
                                                Origin = Anchor.CentreLeft,
                                                Margin = new MarginPadding { Horizontal = 10 },
                                            },
                                            new SceneButton
                                            {
                                                Text = SkinEditorStrings.SongSelect,
                                                Action = () => performer?.PerformFromScreen(screen =>
                                                {
                                                    if (screen is SongSelect)
                                                        return;

                                                    screen.Push(new SoloSongSelect());
                                                }, new[] { typeof(SongSelect) })
                                            },
                                            new SceneButton
                                            {
                                                Text = SkinEditorStrings.Gameplay,
                                                Action = () => skinEditorOverlay?.PresentGameplay(),
                                            },
                                            new SceneButton
                                            {
                                                Text = EzEditorStrings.SCENE_EZ_SKIN,
                                                Width = 110,
                                                Action = () => skinEditorOverlay?.ToggleEzSkinEditor(),
                                            },
                                        }
                                    },
                                }
                            },
                            hudLayerToggle = new EzEditorHudLayerToggle
                            {
                                Margin = new MarginPadding { Left = padding, Top = gap },
                            },
                        },
                    },
                },
            };
        }

        /// <summary>Scene-bar action button; footer chrome via <see cref="EzSceneBarButton"/>.</summary>
        public partial class SceneButton : EzSceneBarButton
        {
        }
    }
}

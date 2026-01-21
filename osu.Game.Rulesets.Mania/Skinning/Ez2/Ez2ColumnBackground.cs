// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Game.LAsEzExtensions.Configuration;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.LAsEZMania;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Screens;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning.Ez2
{
    public partial class Ez2ColumnBackground : CompositeDrawable, IKeyBindingHandler<ManiaAction>
    {
        private readonly Bindable<float> overlayHeight = new Bindable<float>();
        private Bindable<double> hitPosition = new Bindable<double>();
        private Color4 brightColour;
        private Color4 dimColour;

        private Box background = null!;
        private Box backgroundOverlay = null!;
        private Box separator = new Box();
        private Bindable<Color4> accentColour = null!;

        [Resolved]
        private Column column { get; set; } = null!;

        [Resolved]
        private StageDefinition stageDefinition { get; set; } = null!;

        [Resolved]
        private Ez2ConfigManager ezSkinConfig { get; set; } = null!;

        public Ez2ColumnBackground()
        {
            Anchor = Anchor.BottomLeft;
            Origin = Anchor.BottomLeft;
            RelativeSizeAxes = Axes.Both;
            // Masking = true;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    background = new Box
                    {
                        Name = "Background",
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.Black.Opacity(0.8f),
                    },
                    backgroundOverlay = new Box
                    {
                        Name = "Background Gradient Overlay",
                        RelativeSizeAxes = Axes.Both,
                        Height = 0.5f,
                        Blending = BlendingParameters.Additive,
                        Alpha = 0
                    },
                }
            };

            separator = new Box
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopCentre,
                Width = 2,
                Colour = Color4.White.Opacity(0.5f),
                Alpha = 0,
            };
            column.TopLevelContainer.Add(separator);

            overlayHeight.BindValueChanged(height => backgroundOverlay.Height = height.NewValue, true);

            accentColour = new Bindable<Color4>(DrawColoursForColumns(column.Index, stageDefinition));
            accentColour.BindValueChanged(colour =>
            {
                var newColour = colour.NewValue.Darken(3);

                if (newColour.A != 0)
                {
                    newColour = newColour.Opacity(0.8f);
                }

                backgroundOverlay.Colour = newColour;
                background.Colour = colour.NewValue.Opacity(0.8f).Darken(3);
                brightColour = colour.NewValue.Opacity(0.6f);
                dimColour = colour.NewValue.Opacity(0);
            }, true);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            hitPosition = ezSkinConfig.GetBindable<double>(Ez2Setting.HitPosition);
            hitPosition.BindValueChanged(_ => OnConfigChanged(), true);
        }

        private void OnConfigChanged()
        {
            separator.Height = DrawHeight - (float)hitPosition.Value;

            if (drawSeparator(column.Index, stageDefinition))
            {
                separator.Alpha = 0.2f;
            }
            else
            {
                separator.Alpha = 0;
            }
        }

        public bool OnPressed(KeyBindingPressEvent<ManiaAction> e)
        {
            if (e.Action == column.Action.Value)
            {
                var noteColour = column.AccentColour.Value;
                brightColour = noteColour.Opacity(0.9f);
                dimColour = noteColour.Opacity(0);

                backgroundOverlay.Colour = ColourInfo.GradientVertical(dimColour, brightColour);

                overlayHeight.Value = 0.5f;

                backgroundOverlay.FadeTo(1, 50, Easing.OutQuint).Then().FadeTo(0.5f, 250, Easing.OutQuint);
            }

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<ManiaAction> e)
        {
            if (e.Action == column.Action.Value)
                backgroundOverlay.FadeTo(0, 250, Easing.OutQuint);
        }

        public static Color4 DrawColoursForColumns(int columnIndex, StageDefinition stage)
        {
            return stage.EzGetColumnColor(columnIndex);
        }

        //TODO: 这里的逻辑可以优化，避免重复计算
        private bool drawSeparator(int columnIndex, StageDefinition stage)
        {
            return stage.Columns switch
            {
                12 => columnIndex is 0 or 10,
                14 => columnIndex is 0 or 5 or 6 or 11,
                16 => columnIndex is 0 or 5 or 9 or 14,
                _ => false
            };
        }
    }
}

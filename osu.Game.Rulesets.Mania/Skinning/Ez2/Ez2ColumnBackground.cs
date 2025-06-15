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
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.LAsEZMania;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Screens;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning.Ez2
{
    public partial class Ez2ColumnBackground : CompositeDrawable, IKeyBindingHandler<ManiaAction>
    {
        private readonly Bindable<float> overlayHeight = new Bindable<float>();
        private readonly Bindable<float> hitPosition = new Bindable<float>();
        private readonly IBindable<ScrollingDirection> direction = new Bindable<ScrollingDirection>();
        private Color4 brightColour;
        private Color4 dimColour;

        private Box background = null!;
        private Box backgroundOverlay = null!;
        private Box? separator;
        private Bindable<Color4> accentColour = null!;

        [Resolved]
        private Column column { get; set; } = null!;

        [Resolved]
        private StageDefinition stageDefinition { get; set; } = null!;

        [Resolved]
        private EzSkinSettingsManager ezSkinConfig { get; set; } = null!;

        public Ez2ColumnBackground()
        {
            RelativeSizeAxes = Axes.Both;
            Masking = true;
        }

        [BackgroundDependencyLoader]
        private void load(IScrollingInfo scrollingInfo)
        {
            if (stageDefinition.Columns == 14 && column.Index == 13)
                return;

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
                    separator = new Box
                    {
                        Name = "Separator",
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopRight,
                        Width = 2,
                        Colour = Color4.White,
                        Alpha = 0,
                    }
                }
            };

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

            direction.BindTo(scrollingInfo.Direction);
            direction.BindValueChanged(onDirectionChanged, true);

            if (drawSeparator(column.Index, stageDefinition, true))
            {
                separator.Alpha = 0.2f;
            }

            if (drawSeparator(column.Index, stageDefinition, false))
            {
                separator.Alpha = 0;
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            hitPosition.Value = (float)ezSkinConfig.GetBindable<double>(EzSkinSetting.HitPosition).Value;
            ezSkinConfig.OnSettingsChanged += OnConfigChanged;
            OnConfigChanged();
        }

        private void OnConfigChanged()
        {
            if (separator != null)
                separator.Height = DrawHeight - hitPosition.Value;
        }

        private void onDirectionChanged(ValueChangedEvent<ScrollingDirection> direction)
        {
            if (direction.NewValue == ScrollingDirection.Up)
            {
                backgroundOverlay.Anchor = backgroundOverlay.Origin = Anchor.TopLeft;
            }
            else
            {
                backgroundOverlay.Anchor = backgroundOverlay.Origin = Anchor.BottomLeft;
            }
        }

        public bool OnPressed(KeyBindingPressEvent<ManiaAction> e)
        {
            if (e.Action == column.Action.Value)
            {
                var noteColour = column.AccentColour.Value;
                brightColour = noteColour.Opacity(0.9f);
                dimColour = noteColour.Opacity(0);

                backgroundOverlay.Colour = direction.Value == ScrollingDirection.Up
                    ? ColourInfo.GradientVertical(brightColour, dimColour)
                    : ColourInfo.GradientVertical(dimColour, brightColour);

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
        private bool drawSeparator(int columnIndex, StageDefinition stage, bool isSeparator)
        {
            if (!isSeparator)
                return false;

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

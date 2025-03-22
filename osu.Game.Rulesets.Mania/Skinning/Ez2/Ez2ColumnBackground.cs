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
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Skinning;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning.Ez2
{
    public partial class Ez2ColumnBackground : CompositeDrawable, IKeyBindingHandler<ManiaAction>
    {
        private readonly IBindable<ScrollingDirection> direction = new Bindable<ScrollingDirection>();

        private Color4 brightColour;
        private Color4 dimColour;

        private Box background = null!;
        private Box backgroundOverlay = null!;
        private Box separator = null!;

        [Resolved]
        private Column column { get; set; } = null!;

        private Bindable<Color4> accentColour = null!;
        private readonly Bindable<float> overlayHeight = new Bindable<float>(0f);

        public Ez2ColumnBackground()
        {
            RelativeSizeAxes = Axes.Both;

            Masking = true;
            // CornerRadius = 6; //设置圆角, 轨道间会出现缝隙
        }

        [BackgroundDependencyLoader]
        private void load(IScrollingInfo scrollingInfo, ISkinSource skin, StageDefinition stageDefinition)
        {
            if (stageDefinition.Columns == 14 && column.Index == 13)
            {
                return;
            }

            InternalChildren = new Drawable[]
            {
                background = new Box
                {
                    Name = "Background",
                    RelativeSizeAxes = Axes.Both,
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
                    RelativeSizeAxes = Axes.None,
                    Width = 2,
                    // Height = DrawHeight - Stage.HIT_TARGET_POSITION,
                    Colour = Color4.White,
                    Alpha = 0,
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

                background.Colour = newColour;
                // background.Colour = colour.NewValue.Darken(3);
                // brightColour = colour.NewValue.Opacity(0.6f);
                // dimColour = colour.NewValue.Opacity(0);
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

            separator.Height = DrawHeight - Stage.HIT_TARGET_POSITION;
        }

        // public void UpdateBackgroundColour(List<int> processedTracks)
        // {
        //     if (processedTracks.Contains(column.Index))
        //     {
        //         background.Colour = new Color4(0, 0, 0, 0);
        //     }
        // }

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

                overlayHeight.Value = 0.75f;

                backgroundOverlay.FadeTo(1, 50, Easing.OutQuint).Then().FadeTo(0.5f, 250, Easing.OutQuint);
            }

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<ManiaAction> e)
        {
            if (e.Action == column.Action.Value)
                backgroundOverlay.FadeTo(0, 250, Easing.OutQuint);
        }

        private static readonly Color4 colour_column = new Color4(4, 4, 4, 255);
        private static readonly Color4 colour_scratch = new Color4(20, 0, 0, 255);
        private static readonly Color4 colour_panel = new Color4(0, 20, 0, 255);
        private static readonly Color4 colour_alpha = new Color4(0, 0, 0, 0);

        public static Color4 DrawColoursForColumns(int columnIndex, StageDefinition stage)
        {
            columnIndex %= stage.Columns;

            // bool noScratch = NoScratch.Value;
            // bool noPanel = NoPanel.Value;
            switch (stage.Columns)
            {
                case 12:
                    switch (columnIndex)
                    {
                        case 0:
                        case 11:
                            return colour_scratch;

                        default:
                            return colour_column;
                    }

                case 14:
                    switch (columnIndex)
                    {
                        case 0:
                        case 12:
                            return colour_scratch;

                        case 13:
                            return colour_alpha;

                        case 6:
                            return colour_panel;

                        default:
                            return colour_column;
                    }

                case 16:
                    switch (columnIndex)
                    {
                        case 0:
                        case 15:
                            return colour_scratch;

                        case 6:
                        case 7:
                        case 8:
                        case 9:
                            return colour_panel;

                        default:
                            return colour_column;
                    }

                default:
                    return colour_column;
            }
        }

        private bool drawSeparator(int columnIndex, StageDefinition stage, bool isSeparator)
        {
            columnIndex %= stage.Columns;

            switch (stage.Columns)
            {
                case 12:
                    switch (columnIndex)
                    {
                        case 0:
                        case 10:

                            return isSeparator;

                        default: return false;
                    }

                case 14:
                    switch (columnIndex)
                    {
                        case 0:
                        case 5:
                        case 6:
                        case 11:

                            return isSeparator;

                        default: return false;
                    }

                case 16:
                    switch (columnIndex)
                    {
                        case 0:
                        case 5:
                        case 9:
                        case 14:

                            return isSeparator;

                        default: return false;
                    }

                default: return false;
            }
        }
    }
}

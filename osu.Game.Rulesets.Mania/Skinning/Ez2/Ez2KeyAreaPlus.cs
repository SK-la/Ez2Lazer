// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Utils;
using osu.Game.EzOsuGame.Mods;
using osu.Game.Graphics;
using osu.Game.Rulesets.Mania.EzMania.HUD;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Screens.Play;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning.Ez2
{
    // 警告：此文件没写完，调查创建可能导致程序崩溃。
    public partial class Ez2KeyAreaPlus : CompositeDrawable, IKeyBindingHandler<ManiaAction>
    {
        private readonly IBindable<ScrollingDirection> directionLocal = new Bindable<ScrollingDirection>();

        private Container directionContainer = null!;
        private Drawable background = null!;

        private Circle hitTargetLine = null!;

        private Container<Circle> bottomIcon = null!;
        private CircularContainer topIcon = null!;
        private Box? topIconBox;
        private readonly IBindable<Color4> accentColourLocal = new Bindable<Color4>();

        [Resolved]
        private Column column { get; set; } = null!;

        [Resolved]
        private IGameplayClock gameplayClock { get; set; } = null!;

        /// <summary>
        /// Drives the blink on the icon. Taken from the shared tracker, so the blink follows every timing section and
        /// any live rate change instead of a repeating schedule set up once at load.
        /// </summary>
        private EzBeatmapSpeedTracker speedTracker = null!;

        private double beatPhase;

        public Ez2KeyAreaPlus()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(IScrollingInfo scrollingInfo)
        {
            const float icon_circle_size = 8;
            const float icon_spacing = 7;
            const float icon_vertical_offset = -30;

            InternalChild = directionContainer = new Container
            {
                RelativeSizeAxes = Axes.X,
                Height = Stage.HIT_TARGET_POSITION + Ez2NotePiece.CORNER_RADIUS * 2,
                Children = new Drawable[]
                {
                    new Container
                    {
                        Masking = true,
                        RelativeSizeAxes = Axes.Both,
                        CornerRadius = Ez2NotePiece.CORNER_RADIUS,
                        Child = background = new Box
                        {
                            Name = "Key gradient",
                            Alpha = 0,
                            RelativeSizeAxes = Axes.Both,
                        },
                    },
                    new EzKeyCounterPro(column.Action.Value)
                    {
                        Anchor = Anchor.BottomCentre,
                        Origin = Anchor.TopCentre,
                        Y = 10, // 调整计数器位置
                    },
                    hitTargetLine = new Circle
                    {
                        RelativeSizeAxes = Axes.X,
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Colour = OsuColour.Gray(196 / 255f),
                        Height = Ez2NotePiece.CORNER_RADIUS * 2,
                        Masking = true,
                        EdgeEffect = new EdgeEffectParameters { Type = EdgeEffectType.Glow },
                    },
                    new Container
                    {
                        Name = "Icons",
                        RelativeSizeAxes = Axes.Both,
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Children = new Drawable[]
                        {
                            bottomIcon = new Container<Circle>
                            {
                                AutoSizeAxes = Axes.Both,
                                Anchor = Anchor.BottomCentre,
                                Origin = Anchor.Centre,
                                Blending = BlendingParameters.Additive,
                                Y = icon_vertical_offset + 5,
                                Children = new[]
                                {
                                    new Circle
                                    {
                                        Size = new Vector2(icon_circle_size),
                                        Anchor = Anchor.BottomCentre,
                                        Origin = Anchor.Centre,
                                        EdgeEffect = new EdgeEffectParameters { Type = EdgeEffectType.Glow },
                                    },
                                    new Circle
                                    {
                                        X = -icon_spacing,
                                        Y = icon_spacing * 1.2f,
                                        Size = new Vector2(icon_circle_size),
                                        Anchor = Anchor.BottomCentre,
                                        Origin = Anchor.Centre,
                                        EdgeEffect = new EdgeEffectParameters { Type = EdgeEffectType.Glow },
                                    },
                                    new Circle
                                    {
                                        X = icon_spacing,
                                        Y = icon_spacing * 1.2f,
                                        Size = new Vector2(icon_circle_size),
                                        Anchor = Anchor.BottomCentre,
                                        Origin = Anchor.Centre,
                                        EdgeEffect = new EdgeEffectParameters { Type = EdgeEffectType.Glow },
                                    },
                                }
                            },
                            topIcon = new CircularContainer
                            {
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.Centre,
                                Y = -icon_vertical_offset + 35,
                                Size = new Vector2(22, 14),
                                Masking = true,
                                BorderThickness = 4,
                                BorderColour = Color4.White,
                                EdgeEffect = new EdgeEffectParameters { Type = EdgeEffectType.Glow },
                                Children = new Drawable[]
                                {
                                    new Box
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Alpha = 0,
                                        AlwaysPresent = true,
                                    },
                                },
                            },
                        },
                    },
                }
            };

            directionLocal.BindTo(scrollingInfo.Direction);
            directionLocal.BindValueChanged(onDirectionChanged, true);

            AddInternal(speedTracker = new EzBeatmapSpeedTracker());

            // cache inner box once to avoid repeated LINQ allocation inside Update
            topIconBox = topIcon.Children.OfType<Box>().FirstOrDefault();

            // Use a local bindable bound to the column's shared bindable so we can safely unbind later.
            accentColourLocal.BindTo(column.AccentColour);

            void applyAccent(Color4 c)
            {
                background.Colour = c.Darken(0.2f);
                bottomIcon.Colour = c;
            }

            // apply current value immediately and subscribe to future changes via local bindable
            applyAccent(accentColourLocal.Value);
            accentColourLocal.BindValueChanged(e => applyAccent(e.NewValue), true);

            column.TopLevelContainer.Add(CreateProxy());
        }

        protected override void Update()
        {
            base.Update();

            if (topIconBox == null)
                return;

            double beatLength = speedTracker.BeatLength.Value;

            if (beatLength <= 0)
                return;

            double elapsed = gameplayClock.ElapsedFrameTime;
            double previousPhase = beatPhase;

            // Elapsed time on the gameplay clock is already song time (it advances at the audible rate), so the beat
            // length alone converts it into beats.
            beatPhase = EzBeatmapSpeedTracker.AdvanceBeatPhase(beatPhase, elapsed, beatLength);

            // A phase that went backwards wrapped, i.e. a beat just passed. The fade durations are song time too, which
            // is the clock the transform runs on.
            if (elapsed > 0 && beatPhase < previousPhase)
            {
                double fadeTime = Math.Max(1, beatLength / 2);
                var box = topIconBox;

                box.FadeTo(1, fadeTime).Then().FadeTo(0, fadeTime);
            }
        }

        private void onDirectionChanged(ValueChangedEvent<ScrollingDirection> direction)
        {
            switch (direction.NewValue)
            {
                case ScrollingDirection.Up:
                    directionContainer.Scale = new Vector2(1, -1);
                    directionContainer.Anchor = Anchor.TopCentre;
                    directionContainer.Origin = Anchor.BottomCentre;
                    break;

                case ScrollingDirection.Down:
                    directionContainer.Scale = new Vector2(1, 1);
                    directionContainer.Anchor = Anchor.BottomCentre;
                    directionContainer.Origin = Anchor.BottomCentre;
                    break;
            }
        }

        public bool OnPressed(KeyBindingPressEvent<ManiaAction> e)
        {
            if (e.Action != column.Action.Value) return false;

            const double lighting_fade_in_duration = 70;
            Color4 lightingColour = getLightingColour();

            background
                .FlashColour(accentColourLocal.Value.Lighten(0.8f), 200, Easing.OutQuint)
                .FadeTo(1, lighting_fade_in_duration, Easing.OutQuint)
                .Then()
                .FadeTo(0.8f, 500);

            hitTargetLine.FadeColour(Color4.White, lighting_fade_in_duration, Easing.OutQuint);
            hitTargetLine.TransformTo(nameof(EdgeEffect), new EdgeEffectParameters
            {
                Type = EdgeEffectType.Glow,
                Colour = lightingColour.Opacity(0.4f),
                Radius = 20,
            }, lighting_fade_in_duration, Easing.OutQuint);

            topIcon.ScaleTo(0.9f, lighting_fade_in_duration, Easing.OutQuint);
            topIcon.TransformTo(nameof(EdgeEffect), new EdgeEffectParameters
            {
                Type = EdgeEffectType.Glow,
                Colour = lightingColour.Opacity(0.1f),
                Radius = 20,
            }, lighting_fade_in_duration, Easing.OutQuint);

            bottomIcon.FadeColour(Color4.White, lighting_fade_in_duration, Easing.OutQuint);

            foreach (var circle in bottomIcon)
            {
                circle.TransformTo(nameof(EdgeEffect), new EdgeEffectParameters
                {
                    Type = EdgeEffectType.Glow,
                    Colour = lightingColour.Opacity(0.2f),
                    Radius = 60,
                }, lighting_fade_in_duration, Easing.OutQuint);
            }

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<ManiaAction> e)
        {
            if (e.Action != column.Action.Value) return;

            const double lighting_fade_out_duration = 800;

            Color4 lightingColour = getLightingColour().Opacity(0);

            // background fades out faster than lighting elements to give better definition to the player.
            background.FadeTo(0.3f, 50, Easing.OutQuint)
                      .Then()
                      .FadeOut(lighting_fade_out_duration, Easing.OutQuint);

            topIcon.ScaleTo(1f, 200, Easing.OutQuint);
            topIcon.TransformTo(nameof(EdgeEffect), new EdgeEffectParameters
            {
                Type = EdgeEffectType.Glow,
                Colour = lightingColour,
                Radius = 20,
            }, lighting_fade_out_duration, Easing.OutQuint);

            hitTargetLine.FadeColour(OsuColour.Gray(196 / 255f), lighting_fade_out_duration, Easing.OutQuint);
            hitTargetLine.TransformTo(nameof(EdgeEffect), new EdgeEffectParameters
            {
                Type = EdgeEffectType.Glow,
                Colour = lightingColour,
                Radius = 25,
            }, lighting_fade_out_duration, Easing.OutQuint);

            bottomIcon.FadeColour(accentColourLocal.Value, lighting_fade_out_duration, Easing.OutQuint);

            foreach (var circle in bottomIcon)
            {
                circle.TransformTo(nameof(EdgeEffect), new EdgeEffectParameters
                {
                    Type = EdgeEffectType.Glow,
                    Colour = lightingColour,
                    Radius = 30,
                }, lighting_fade_out_duration, Easing.OutQuint);
            }
        }

        private Color4 getLightingColour() => Interpolation.ValueAt(0.2f, accentColourLocal.Value, Color4.White, 0, 1);
    }
}

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
using osu.Game.Beatmaps;
using osu.Game.LAsEzExtensions.Configuration;
using osu.Game.Graphics;
using osu.Game.Overlays.SkinEditor;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Screens;
using osu.Game.Screens.Play;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning.Ez2
{
    public partial class Ez2KeyArea : CompositeDrawable, IKeyBindingHandler<ManiaAction>
    {
        private readonly IBindable<ScrollingDirection> directionLocal = new Bindable<ScrollingDirection>();
        private readonly IBindable<double> hitPositionLocal = new BindableDouble();
        private Container directionContainer = null!;
        private Drawable background = null!;

        private Circle hitTargetLine = null!;

        private CircularContainer? topIcon;
        private Box? topIconBox;
        private readonly IBindable<Color4> accentColourLocal = new Bindable<Color4>();

        [Resolved]
        private Column column { get; set; } = null!;

        [Resolved]
        private IBeatmap beatmap { get; set; } = null!;

        [Resolved]
        private IGameplayClock gameplayClock { get; set; } = null!;

        public Ez2KeyArea()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(IEzSkinInfo ezSkinInfo)
        {
            InternalChild = directionContainer = new Container
            {
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.BottomCentre,
                RelativeSizeAxes = Axes.X,
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
                            topIcon = new CircularContainer
                            {
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.Centre,
                                Y = 60,
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

            hitPositionLocal.BindTo(ezSkinInfo.HitPosition);
            hitPositionLocal.BindValueChanged(_ => updateHitPosition(), true);

            accentColourLocal.BindTo(column.AccentColour);

            void applyAccent(Color4 c)
            {
                background.Colour = c.Darken(0.2f);
                topIcon.Colour = c;
            }

            applyAccent(accentColourLocal.Value);
            accentColourLocal.BindValueChanged(e => applyAccent(e.NewValue), true);

            column.TopLevelContainer.Add(CreateProxy());
        }

        private void updateHitPosition()
        {
            directionContainer.Height = (float)hitPositionLocal.Value;
        }

        private double beatInterval;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            double bpm = beatmap.BeatmapInfo.BPM * gameplayClock.GetTrueGameplayRate();
            beatInterval = 60000 / bpm;
            // cache reference to inner box to avoid LINQ allocations during Update
            topIconBox = topIcon?.Children.OfType<Box>().FirstOrDefault();
        }

        protected override void Update()
        {
            base.Update();

            if (topIconBox == null)
                return;

            double progress = (gameplayClock.CurrentTime % beatInterval) / beatInterval;

            if (progress < gameplayClock.ElapsedFrameTime / beatInterval)
            {
                double fadeTime = Math.Max(1, beatInterval / 2);
                var box = topIconBox;

                box?.FadeTo(1, fadeTime)
                   .Then()
                   .FadeTo(0, fadeTime);
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

            topIcon.FadeColour(Color4.White, lighting_fade_in_duration, Easing.OutQuint);
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

            topIcon.FadeColour(accentColourLocal.Value, lighting_fade_out_duration, Easing.OutQuint);
        }

        private Color4 getLightingColour() => Interpolation.ValueAt(0.2f, accentColourLocal.Value, Color4.White, 0, 1);

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                // Unbind local bindables to avoid leaking subscriptions and avoid touching shared bindables.
                accentColourLocal.UnbindBindings();
                hitPositionLocal.UnbindBindings();
                directionLocal.UnbindBindings();
            }

            base.Dispose(isDisposing);
        }
    }
}

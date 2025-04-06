// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics.Containers;
using osu.Game.Skinning;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Effects;
using osu.Framework.Input.Events;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Screens.Play;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning.Ez2.Ez2HUD
{
    public partial class EzComJudgementArea : CompositeDrawable, ISerialisableDrawable, IHitExplosion
    {
        public bool UsesFixedAnchor { get; set; }

        public override bool RemoveWhenNotAlive => true;

        private Container largeFaint = null!;
        private Container background = null!;
        private double bpm;
        private readonly IBindable<ScrollingDirection> direction = new Bindable<ScrollingDirection>();

        private Bindable<Color4> accentColour = null!;

        [Resolved]
        private Column column { get; set; } = null!;

        [Resolved]
        private IBeatmap beatmap { get; set; } = null!;

        [Resolved]
        private IGameplayClock gameplayClock { get; set; } = null!;

        [Resolved]
        private StageDefinition stageDefinition { get; set; } = null!;

        public EzComJudgementArea()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(IScrollingInfo scrollingInfo)
        {
            if (stageDefinition.Columns == 14 && column.Index == 13)
                Alpha = 0;

            Size = new Vector2(2);
            Alpha = 1;

            InternalChildren = new Drawable[]
            {
                background = new Container
                {
                    Height = NOTE_HEIGHT,
                    Blending = BlendingParameters.Mixture,
                    Colour = Color4.Gray,
                    Alpha = 0.3f,
                },
                largeFaint = new Container
                {
                    Height = NOTE_HEIGHT,
                    Blending = BlendingParameters.Additive,
                    Alpha = 0.8f,
                    RelativeSizeAxes = Axes.Both,
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    Masking = true,
                    Scale = new Vector2(0.5f),

                    EdgeEffect = new EdgeEffectParameters
                    {
                        Type = EdgeEffectType.Glow,
                        Colour = new Color4(1f, 1f, 1f, 0.5f),
                        Radius = 2f, // 调整光晕半径
                        Roundness = 0f,
                    },
                },
            };
            direction.BindTo(scrollingInfo.Direction);
            direction.BindValueChanged(onDirectionChanged, true);

            accentColour = column.AccentColour.GetBoundCopy();
            accentColour.BindValueChanged(colour =>
            {
                largeFaint.Colour = Interpolation.ValueAt(0.8f, colour.NewValue, Color4.White, 0, 1);

                largeFaint.EdgeEffect = new EdgeEffectParameters
                {
                    Type = EdgeEffectType.Glow,
                    Colour = colour.NewValue,
                    Roundness = Ez2NotePiece.NOTE_HEIGHT,
                    Radius = 50,
                };
            }, true);
            bpm = beatmap.ControlPointInfo.TimingPointAt(gameplayClock.CurrentTime).BPM * gameplayClock.GetTrueGameplayRate();
        }

        protected override void Update()
        {
            base.Update();
            Height = DrawWidth;

            if (stageDefinition.Columns == 14 && column.Index == 13)
                Alpha = 0;

            double interval = 60000 / bpm;
            const double amplitude = 6.0;
            double progress = (gameplayClock.CurrentTime % interval) / interval;

            double smoothValue = smoothSineWave(progress);
            Y = (float)(smoothValue * amplitude);
        }

        private double smoothSineWave(double t)
        {
            const double frequency = 1;
            const double amplitude = 0.3;
            return amplitude * Math.Sin(frequency * t * 2 * Math.PI);
        }

        private const float NOTE_HEIGHT = 40;

        public void Animate(JudgementResult result)
        {
            this.FadeOutFromOne(PoolableHitExplosion.DURATION, Easing.Out);
        }

        private void onDirectionChanged(ValueChangedEvent<ScrollingDirection> direction)
        {
            Anchor = Origin = direction.NewValue == ScrollingDirection.Up ? Anchor.TopCentre : Anchor.BottomCentre;
        }

        public bool OnPressed(KeyBindingPressEvent<ManiaAction> e)
        {
            if (e.Action != column.Action.Value) return false;

            const double lighting_fade_in_duration = 70;
            Color4 lightingColour = getLightingColour();

            background.ScaleTo(0.9f, lighting_fade_in_duration, Easing.OutQuint);
            background.TransformTo(nameof(EdgeEffect), new EdgeEffectParameters
            {
                Type = EdgeEffectType.Glow,
                Colour = lightingColour.Opacity(0.1f),
                Radius = 20,
            }, lighting_fade_in_duration, Easing.OutQuint);

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<ManiaAction> e)
        {
            if (e.Action != column.Action.Value) return;

            const double lighting_fade_out_duration = 800;

            Color4 lightingColour = getLightingColour().Opacity(0);

            background.ScaleTo(1f, 200, Easing.OutQuint);
            background.TransformTo(nameof(EdgeEffect), new EdgeEffectParameters
            {
                Type = EdgeEffectType.Glow,
                Colour = lightingColour,
                Radius = 20,
            }, lighting_fade_out_duration, Easing.OutQuint);
        }

        private Color4 getLightingColour() => Interpolation.ValueAt(0.2f, accentColour.Value, Color4.White, 0, 1);
    }
}

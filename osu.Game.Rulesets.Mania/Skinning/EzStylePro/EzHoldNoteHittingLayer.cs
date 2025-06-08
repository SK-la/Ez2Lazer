// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Beatmaps;
using osu.Game.Screens;
using osu.Game.Screens.Play;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    public partial class EzHoldNoteHittingLayer : CompositeDrawable
    {
        // public readonly Bindable<Color4> AccentColour = new Bindable<Color4>();
        public readonly Bindable<bool> IsHolding = new Bindable<bool>();

        // private readonly Container hitEffectDrawable = null!;

        [Resolved]
        private IBeatmap beatmap { get; set; } = null!;

        [Resolved]
        private IGameplayClock gameplayClock { get; set; } = null!;

        [Resolved]
        private EzLocalTextureFactory factory { get; set; } = null!;

        private double bpm;

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.Both;
            Blending = new BlendingParameters
            {
                Source = BlendingType.SrcAlpha,
                Destination = BlendingType.One,
            };
            // Alpha = 0;
            bpm = beatmap.ControlPointInfo.TimingPointAt(gameplayClock.CurrentTime).BPM * gameplayClock.GetTrueGameplayRate();
        }

        protected override void Update()
        {
            base.Update();

            double interval = 60000 / bpm;
            const double amplitude = 6.0;
            double progress = (gameplayClock.CurrentTime % interval) / interval;

            double smoothValue = smoothSineWave(progress);
            Y =  (float)(smoothValue * amplitude);
        }

        private double smoothSineWave(double t)
        {
            const double frequency = 1;
            const double amplitude = 0.3;
            return amplitude * Math.Sin(frequency * t * 2 * Math.PI);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            loadAnimation();
            factory.OnTextureNameChanged += onSkinChanged;
            // AccentColour.BindValueChanged(colour =>
            // {
            //     hitEffectDrawable.Colour = colour.NewValue.Lighten(0.2f);
            // }, true);

            IsHolding.BindValueChanged(hitting =>
            {
                const float animation_length = 80;
                // ClearTransforms();

                if (hitting.NewValue)
                {
                    this.FadeTo(1, animation_length / 2)
                        .Loop();
                }
                else
                {
                    this.FadeOut(animation_length);
                }
            }, true);
        }

        private void onSkinChanged()
        {
            Schedule(() =>
            {
                ClearInternal();
                loadAnimation();
                IsHolding.TriggerChange();
            });
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            factory.OnTextureNameChanged -= onSkinChanged;
        }

        protected virtual string ComponentName => "noteflaregood";

        private void loadAnimation()
        {
            var animationContainer = factory.CreateAnimation(ComponentName);
            AddInternal(animationContainer);
        }

        public void Recycle()
        {
            ClearTransforms();
            Alpha = 1;

            // hitEffectDrawable.FinishTransforms();
            // hitEffectDrawable.Scale = Vector2.One;
        }
    }
}

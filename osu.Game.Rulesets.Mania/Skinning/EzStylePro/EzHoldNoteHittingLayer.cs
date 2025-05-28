// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Textures;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Screens;
using osu.Game.Screens.Play;
using osuTK;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    public partial class EzHoldNoteHittingLayer : CompositeDrawable
    {
        // public readonly Bindable<Color4> AccentColour = new Bindable<Color4>();
        public readonly Bindable<bool> IsHitting = new Bindable<bool>();

        private readonly Container hitEffectDrawable = null!;

        [Resolved]
        private IBeatmap beatmap { get; set; } = null!;

        [Resolved]
        private IGameplayClock gameplayClock { get; set; } = null!;

        [Resolved]
        private EzLocalTextureFactory factory { get; set; } = null!;

        protected virtual string ComponentName => "noteflaregood";

        public EzHoldNoteHittingLayer()
        {
            RelativeSizeAxes = Axes.Both;

            Blending = new BlendingParameters
            {
                Source = BlendingType.SrcAlpha,
                Destination = BlendingType.One,
            };
            Alpha = 0;
        }

        private double bpm;

        [BackgroundDependencyLoader]
        private void load(EzSkinSettingsManager ezSkinConfig, TextureStore textureStore, IScrollingInfo scrollingInfo)
        {
            bpm = beatmap.ControlPointInfo.TimingPointAt(gameplayClock.CurrentTime).BPM * gameplayClock.GetTrueGameplayRate();
        }

        protected override void Update()
        {
            base.Update();
            // Height = DrawWidth;

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
            ClearInternal();
            loadAnimation();
            factory.OnTextureNameChanged += onSkinChanged;
            // AccentColour.BindValueChanged(colour =>
            // {
            //     hitEffectDrawable.Colour = colour.NewValue.Lighten(0.2f);
            // }, true);

            IsHitting.BindValueChanged(hitting =>
            {
                const float animation_length = 80;

                hitEffectDrawable.FinishTransforms();
                ClearTransforms();

                if (hitting.NewValue)
                {
                    double synchronisedOffset = animation_length * 2 - Time.Current % (animation_length * 2);

                    using (BeginDelayedSequence(synchronisedOffset))
                    {
                        this.FadeTo(1f, animation_length, Easing.OutSine).Then()
                            .FadeTo(0.6f, animation_length, Easing.InSine)
                            .Loop();
                    }

                    this.FadeIn(animation_length);

                    hitEffectDrawable.ScaleTo(new Vector2(1.1f), animation_length)
                                     .Then()
                                     .ScaleTo(new Vector2(0.9f), animation_length)
                                     .Loop();
                }
                else
                {
                    this.FadeOut(animation_length);
                    hitEffectDrawable.ScaleTo(new Vector2(1.0f), animation_length);
                }
            }, true);
        }

        private void onSkinChanged()
        {
            Schedule(() =>
            {
                ClearInternal();
                loadAnimation();
            });
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            // 取消订阅，防止内存泄漏
            factory.OnTextureNameChanged -= onSkinChanged;
        }

        private void loadAnimation()
        {
            var animationContainer = factory.CreateAnimation(ComponentName);

            AddInternal(animationContainer);
        }

        public void Recycle()
        {
            ClearTransforms();
            hitEffectDrawable.FinishTransforms();
            Alpha = 0;
            hitEffectDrawable.Scale = Vector2.One;
        }
    }
}

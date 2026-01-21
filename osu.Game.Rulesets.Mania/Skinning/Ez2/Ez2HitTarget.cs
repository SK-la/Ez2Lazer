// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Screens.Play;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning.Ez2
{
    internal partial class Ez2HitTarget : Ez2NotePiece
    {
        private readonly IBindable<ScrollingDirection> direction = new Bindable<ScrollingDirection>();

        private double bpm;

        [Resolved]
        private IBeatmap beatmap { get; set; } = null!;

        [Resolved]
        private IGameplayClock gameplayClock { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load(IScrollingInfo scrollingInfo)
        {
            RelativeSizeAxes = Axes.X;
            // Masking = true;
            Height = NoteHeight * NOTE_ACCENT_RATIO;
            CornerRadius = NoteHeight;
            Alpha = 0.3f;
            Blending = BlendingParameters.Mixture;
            Colour = Color4.Gray;

            direction.BindTo(scrollingInfo.Direction);
            direction.BindValueChanged(onDirectionChanged, true);

            bpm = beatmap.ControlPointInfo.TimingPointAt(gameplayClock.CurrentTime).BPM * gameplayClock.GetTrueGameplayRate();
        }

        protected override void Update()
        {
            base.Update();
            Height = DrawWidth;

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
        //     double elasticValue = elasticEaseOut(progress);
        //     Y = (float)(elasticValue * amplitude);
        // }
        //
        // private double elasticEaseOut(double t) //弹性缓动函数
        // {
        //     double p = 0.3;
        //     return Math.Pow(2, -10 * t) * Math.Sin((t - p / 4) * (2 * Math.PI) / p) + 1;
        // }

        private void onDirectionChanged(ValueChangedEvent<ScrollingDirection> direction)
        {
            Anchor = Origin = direction.NewValue == ScrollingDirection.Up ? Anchor.TopCentre : Anchor.BottomCentre;
        }
    }
}

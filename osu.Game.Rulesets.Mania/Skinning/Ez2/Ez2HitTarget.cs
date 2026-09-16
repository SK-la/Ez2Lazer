// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.EzOsuGame.Mods;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Screens.Play;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning.Ez2
{
    public partial class Ez2HitTarget : Ez2NotePiece
    {
        private IBindable<ScrollingDirection> direction = null!;

        private double beatPhase;

        [Resolved]
        private IGameplayClock gameplayClock { get; set; } = null!;

        /// <summary>
        /// The beat this bobs to. Taking it from the shared tracker (rather than a BPM captured once at load) is what
        /// makes it follow every timing section and any live rate change.
        /// </summary>
        private EzBeatmapSpeedTracker speedTracker = null!;

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

            direction = scrollingInfo.Direction;
            direction.BindValueChanged(onDirectionChanged, true);

            AddInternal(speedTracker = new EzBeatmapSpeedTracker());
        }

        protected override void Update()
        {
            base.Update();
            Height = DrawWidth;

            double beatLength = speedTracker.BeatLength.Value;

            if (beatLength <= 0)
                return;

            // Elapsed time on the gameplay clock is already song time (it advances at the audible rate), so the beat
            // length alone converts it into beats.
            beatPhase = EzBeatmapSpeedTracker.AdvanceBeatPhase(beatPhase, gameplayClock.ElapsedFrameTime, beatLength);

            const double amplitude = 6.0;
            Y = (float)(smoothSineWave(beatPhase) * amplitude);
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

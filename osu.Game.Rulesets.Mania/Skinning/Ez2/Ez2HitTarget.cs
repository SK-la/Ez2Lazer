// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Platform;
using osu.Framework.Timing;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Mods;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Screens.Play;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning.Ez2
{
    public partial class Ez2HitTarget : Ez2NotePiece
    {
        private readonly IBindable<double> hitTargetFloatFixed = new Bindable<double>();
        private readonly IBindable<double> hitTargetAlpha = new Bindable<double>();

        private IBindable<ScrollingDirection> direction = null!;

        private double beatPhase;

        [Resolved]
        private IGameplayClock gameplayClock { get; set; } = null!;

        /// <summary>
        /// The beat this bobs to. Taking it from the shared tracker (rather than a BPM captured once at load) is what
        /// makes it follow every timing section and any live rate change.
        /// </summary>
        private EzBeatmapSpeedTracker speedTracker = null!;

        private IFrameBasedClock? hostClock;

        [BackgroundDependencyLoader]
        private void load(IScrollingInfo scrollingInfo, IEzSkinInfo ezSkinInfo, GameHost host)
        {
            RelativeSizeAxes = Axes.X;
            Height = NoteHeight * NOTE_ACCENT_RATIO;
            CornerRadius = NoteHeight;
            Blending = BlendingParameters.Mixture;
            Colour = Color4.Gray;

            // Host update clock keeps bobbing while gameplay is paused (same as EzStylePro).
            hostClock = host.UpdateThread.Clock;

            direction = scrollingInfo.Direction;
            direction.BindValueChanged(onDirectionChanged, true);

            AddInternal(speedTracker = new EzBeatmapSpeedTracker());

            hitTargetFloatFixed.BindTo(ezSkinInfo.HitTargetFloatFixed);
            hitTargetAlpha.BindTo(ezSkinInfo.HitTargetAlpha);
            hitTargetAlpha.BindValueChanged(v => Alpha = (float)v.NewValue, true);
        }

        protected override void Update()
        {
            base.Update();
            Height = DrawWidth;

            double beatLength = speedTracker.BeatLength.Value;

            if (beatLength <= 0)
                return;

            beatPhase = hostClock != null
                ? EzBeatmapSpeedTracker.AdvanceBeatPhase(beatPhase, hostClock.ElapsedFrameTime, beatLength, speedTracker.Rate.Value)
                : EzBeatmapSpeedTracker.AdvanceBeatPhase(beatPhase, gameplayClock.ElapsedFrameTime, beatLength);

            double smoothValue = 0.3 * Math.Sin(beatPhase * 2 * Math.PI);
            Y = (float)(smoothValue * hitTargetFloatFixed.Value);
        }

        private void onDirectionChanged(ValueChangedEvent<ScrollingDirection> direction)
        {
            Anchor = Origin = direction.NewValue == ScrollingDirection.Up ? Anchor.TopCentre : Anchor.BottomCentre;
        }
    }
}

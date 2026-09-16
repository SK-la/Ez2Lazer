// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Mods;
using osu.Game.Screens.Play;
using osu.Framework.Platform;
using osu.Framework.Timing;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    public partial class EzHitTarget : EzNote
    {
        protected override bool UseColorization => false;
        protected override bool ShowSeparators => false;

        private readonly IBindable<double> hitTargetFloatFixed = new Bindable<double>();
        private readonly IBindable<double> hitTargetAlpha = new Bindable<double>();

        [Resolved]
        private IGameplayClock gameplayClock { get; set; } = null!;

        /// <summary>
        /// The beat this bobs to. Taking it from the shared tracker (rather than the metadata BPM captured at load)
        /// is what makes it follow every timing section and any live rate change.
        /// </summary>
        private EzBeatmapSpeedTracker speedTracker = null!;

        private double beatPhase;

        private IFrameBasedClock? hostClock;

        public EzHitTarget()
        {
            RelativeSizeAxes = Axes.X;
            FillMode = FillMode.Fill;
            Depth = 1;
        }

        [BackgroundDependencyLoader]
        private void load(IEzSkinInfo ezSkinInfo, GameHost host)
        {
            Anchor = Anchor.BottomCentre;
            Origin = Anchor.BottomCentre;

            // 使用 host 的 update 线程时钟作为独立时间源，从而在暂停时仍能继续动画。
            hostClock = host.UpdateThread.Clock;

            AddInternal(speedTracker = new EzBeatmapSpeedTracker());

            hitTargetFloatFixed.BindTo(ezSkinInfo.HitTargetFloatFixed);
            hitTargetAlpha.BindTo(ezSkinInfo.HitTargetAlpha);

            hitTargetAlpha.BindValueChanged(v => Alpha = (float)v.NewValue, true);
        }

        protected override void Update()
        {
            base.Update();

            updatePosition();
        }

        private void updatePosition()
        {
            double beatLength = speedTracker.BeatLength.Value;

            if (beatLength <= 0)
                return;

            // 平滑正弦波效果
            // The host clock is real time (it deliberately keeps running while gameplay is paused), so the audible
            // rate has to convert it into song time; the gameplay clock's elapsed time would already carry it.
            beatPhase = hostClock != null
                ? EzBeatmapSpeedTracker.AdvanceBeatPhase(beatPhase, hostClock.ElapsedFrameTime, beatLength, speedTracker.Rate.Value)
                : EzBeatmapSpeedTracker.AdvanceBeatPhase(beatPhase, gameplayClock.ElapsedFrameTime, beatLength);

            double smoothValue = 0.3 * Math.Sin(beatPhase * 2 * Math.PI);
            Y = (float)(smoothValue * hitTargetFloatFixed.Value);
        }
    }
}

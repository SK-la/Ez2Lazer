// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Screens.Play;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    internal partial class EzHitTarget : EzNote
    {
        protected override bool UseColorization => false;
        protected override bool ShowSeparators => false;

        private readonly IBindable<double> hitTargetFloatFixed = new Bindable<double>();
        private readonly IBindable<double> hitTargetAlpha = new Bindable<double>();

        [Resolved]
        private IBeatmap beatmap { get; set; } = null!;

        [Resolved]
        private IGameplayClock gameplayClock { get; set; } = null!;

        private double beatInterval;
        private bool requiresUpdate;

        public EzHitTarget()
        {
            RelativeSizeAxes = Axes.X;
            FillMode = FillMode.Fill;
            Depth = 1;
        }

        [BackgroundDependencyLoader]
        private void load(IEzSkinInfo ezSkinInfo)
        {
            Anchor = Anchor.BottomCentre;
            Origin = Anchor.BottomCentre;

            calculateBeatInterval();

            hitTargetFloatFixed.BindTo(ezSkinInfo.HitTargetFloatFixed);
            hitTargetFloatFixed.BindValueChanged(_ =>
            {
                updatePosition();
            }, true);

            hitTargetAlpha.BindTo(ezSkinInfo.HitTargetAlpha);
            hitTargetAlpha.BindValueChanged(_ => Alpha = (float)hitTargetAlpha.Value, true);

            requiresUpdate = true;
        }

        protected override void Update()
        {
            base.Update();

            if (requiresUpdate)
            {
                updatePosition();
            }
        }

        private void calculateBeatInterval()
        {
            double bpm = beatmap.BeatmapInfo.BPM * gameplayClock.GetTrueGameplayRate();
            beatInterval = 60000 / bpm;
        }

        private void updatePosition()
        {
            // 平滑正弦波效果
            if (beatInterval > 0)
            {
                double progress = (gameplayClock.CurrentTime % beatInterval) / beatInterval;
                double smoothValue = 0.3 * Math.Sin(progress * 2 * Math.PI);
                Y = (float)(smoothValue * hitTargetFloatFixed.Value);
            }
        }
    }
}

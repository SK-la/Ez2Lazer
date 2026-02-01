// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Beatmaps;
using osu.Game.LAsEzExtensions.Configuration;
using osu.Game.Screens;
using osu.Game.Screens.Play;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    internal partial class EzHitTarget : EzNote
    {
        protected override bool BoolUpdateColor => false;
        protected override bool UseColorization => false;
        protected override bool ShowSeparators => false;

        protected override string ColorPrefix => "white";

        private IBindable<double> hitTargetFloatFixed = null!;
        private IBindable<double> hitTargetAlpha = null!;

        [Resolved]
        private IBeatmap beatmap { get; set; } = null!;

        [Resolved]
        private IGameplayClock gameplayClock { get; set; } = null!;

        public EzHitTarget()
        {
            RelativeSizeAxes = Axes.X;
            FillMode = FillMode.Fill;
            Depth = 1;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Anchor = Anchor.BottomCentre;
            Origin = Anchor.BottomCentre;
        }

        private double beatInterval;
        private bool requiresUpdate = true;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            hitTargetAlpha = Column.EzSkinInfo.HitTargetAlpha;
            // use column-led notifications; set initial alpha
            Alpha = (float)hitTargetAlpha.Value;

            hitTargetFloatFixed = Column.EzSkinInfo.HitTargetFloatFixed;

            calculateBeatInterval();
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

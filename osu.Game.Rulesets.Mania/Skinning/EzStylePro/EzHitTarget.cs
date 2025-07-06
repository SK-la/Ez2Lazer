// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.Beatmaps;
using osu.Game.Screens.Play;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    internal partial class EzHitTarget : EzNote
    {
        // private IBindable<double> hitPosition = new Bindable<double>();

        protected override bool ShowSeparators => false;
        protected override bool UseColorization => false;
        protected override string ColorPrefix => "white";

        [Resolved]
        private IBeatmap beatmap { get; set; } = null!;

        [Resolved]
        private IGameplayClock gameplayClock { get; set; } = null!;

        // [Resolved]
        // private EzSkinSettingsManager ezSkinConfig { get; set; } = null!;

        public EzHitTarget()
        {
            RelativeSizeAxes = Axes.X;
            FillMode = FillMode.Fill;
            Alpha = 0.3f;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Anchor = Anchor.BottomCentre;
            Origin = Anchor.BottomCentre;
            // hitPosition = ezSkinConfig.GetBindable<double>(EzSkinSetting.HitPosition);
            // hitPosition.BindValueChanged(_ => updateY(), true);
        }

        // private float baseYPosition = 0f;
        private double beatInterval;

        protected override void LoadComplete()
        {
            base.LoadComplete();
            double bpm = beatmap.BeatmapInfo.BPM * gameplayClock.GetTrueGameplayRate();
            beatInterval = 60000 / bpm;
        }

        protected override void Update()
        {
            base.Update();

            double progress = (gameplayClock.CurrentTime % beatInterval) / beatInterval;
            // 平滑正弦波效果
            double smoothValue = 0.3 * Math.Sin(progress * 2 * Math.PI);
            Y = (float)(smoothValue * 6);
        }

        //DrawableManiaRuleset中关联设置后，此处不必设置
        // private void updateY()
        // {
        //     baseYPosition = LegacyManiaSkinConfiguration.DEFAULT_HIT_POSITION - (float)hitPosition.Value;
        //     Position = new Vector2(0, baseYPosition);
        // }
    }
}

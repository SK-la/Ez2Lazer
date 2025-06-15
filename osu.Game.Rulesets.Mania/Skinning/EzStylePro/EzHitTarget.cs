// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Screens;
using osu.Game.Screens.Play;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    internal partial class EzHitTarget : EzNote
    {
        private readonly IBindable<ScrollingDirection> direction = new Bindable<ScrollingDirection>();
        private IBindable<double> hitPosition = new Bindable<double>();

        protected override bool UseColorization => false; //不染色
        protected override string ColorPrefix => "white";
        private double bpm;

        [Resolved]
        private IBeatmap beatmap { get; set; } = null!;

        [Resolved]
        private IGameplayClock gameplayClock { get; set; } = null!;

        [Resolved]
        private EzSkinSettingsManager ezSkinConfig { get; set; } = null!;

        public EzHitTarget()
        {
            RelativeSizeAxes = Axes.None;
            Width = 1f;
            Blending = new BlendingParameters
            {
                Source = BlendingType.SrcAlpha,
                Destination = BlendingType.One,
            };
            Alpha = 0.3f;
        }

        [BackgroundDependencyLoader]
        private void load(IScrollingInfo scrollingInfo)
        {
            direction.BindTo(scrollingInfo.Direction);
            direction.BindValueChanged(onDirectionChanged, true);

            bpm = beatmap.ControlPointInfo.TimingPointAt(gameplayClock.CurrentTime).BPM * gameplayClock.GetTrueGameplayRate();
            hitPosition = ezSkinConfig.GetBindable<double>(EzSkinSetting.HitPosition);
        }

        private float baseYPosition;

        protected override void Update()
        {
            base.Update();

            double interval = 60000 / bpm;
            const double amplitude = 6.0;
            double progress = (gameplayClock.CurrentTime % interval) / interval;
            double smoothValue = smoothSineWave(progress);

            Y = baseYPosition + (float)(smoothValue * amplitude);
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
            hitPosition.BindValueChanged(_ => updateY(), true);
        }

        private void updateY()
        {
            baseYPosition = 110f - (float)hitPosition.Value;
            Position = new osuTK.Vector2(0, baseYPosition);
        }

        private void onDirectionChanged(ValueChangedEvent<ScrollingDirection> direction)
        {
            Anchor = Origin = direction.NewValue == ScrollingDirection.Up ? Anchor.TopCentre : Anchor.BottomCentre;
        }
    }
}

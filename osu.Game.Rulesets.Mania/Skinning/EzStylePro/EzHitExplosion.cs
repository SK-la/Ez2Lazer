// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osu.Framework.Graphics.Containers;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.LAsEZMania;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Screens;
using osu.Game.Screens.Play;

namespace osu.Game.Rulesets.Mania.Skinning.EzStylePro
{
    public partial class EzHitExplosion : CompositeDrawable, IHitExplosion
    {
        // public override bool RemoveWhenNotAlive => true;

        private readonly IBindable<ScrollingDirection> direction = new Bindable<ScrollingDirection>();

        private OsuConfigManager config = null!;
        private double bpm;

        private readonly Bindable<double> columnWidth = new Bindable<double>();

        private IBindable<double> columnWidthBindable = new Bindable<double>();
        private IBindable<double> specialFactorBindable = new Bindable<double>();

        [Resolved]
        private IBeatmap beatmap { get; set; } = null!;

        [Resolved]
        private IGameplayClock gameplayClock { get; set; } = null!;

        [Resolved]
        private Column column { get; set; } = null!;

        [Resolved]
        private StageDefinition stageDefinition { get; set; } = null!;

        [Resolved]
        private EzLocalTextureFactory factory { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, IScrollingInfo scrollingInfo)
        {
            this.config = config;
            RelativeSizeAxes = Axes.Both;
            Blending = new BlendingParameters
            {
                Source = BlendingType.SrcAlpha,
                Destination = BlendingType.One,
            };

            direction.BindTo(scrollingInfo.Direction);
            direction.BindValueChanged(onDirectionChanged, true);

            bpm = beatmap.ControlPointInfo.TimingPointAt(gameplayClock.CurrentTime).BPM * gameplayClock.GetTrueGameplayRate();
        }

        protected override void Update()
        {
            base.Update();

            double interval = 60000 / bpm;
            double progress = (gameplayClock.CurrentTime % interval) / interval;
            double smoothValue = smoothSineWave(progress);

            columnWidthBindable = config.GetBindable<double>(OsuSetting.ColumnWidth);
            specialFactorBindable = config.GetBindable<double>(OsuSetting.SpecialFactor);
            bool isSpecialColumn = stageDefinition.EzIsSpecialColumn(column.Index);
            columnWidth.Value = columnWidthBindable.Value * (isSpecialColumn ? specialFactorBindable.Value : 1);

            var animationContainer = factory.CreateAnimation("bluenote");
            double noteHeight = columnWidth.Value;

            if (animationContainer is Container container &&
                container.Children.FirstOrDefault() is TextureAnimation animation &&
                animation.FrameCount > 0)
            {
                var texture = animation.CurrentFrame;

                if (texture != null)
                {
                    float aspectRatio = texture.Height / (float)texture.Width;
                    noteHeight = columnWidth.Value * aspectRatio;
                }
            }

            // Framework.Logging.Logger.Log($"EzHitExplosion: noteHeight={noteHeight}");
            Y =  -(float)noteHeight / 2 + (float)(smoothValue * 6);
        }

        private double smoothSineWave(double t)
        {
            const double frequency = 1;
            const double amplitude = 0.3;
            return amplitude * Math.Sin(frequency * t * 2 * Math.PI);
        }

        protected virtual string ComponentName => "noteflare";

        private void loadAnimation()
        {
            var animationContainer = factory.CreateAnimation(ComponentName);

            AddInternal(animationContainer);
        }

        public void Animate(JudgementResult result)
        {
            ClearInternal();
            loadAnimation();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            ClearInternal();
            loadAnimation();
            factory.OnTextureNameChanged += onSkinChanged;
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
            factory.OnTextureNameChanged -= onSkinChanged;
        }

        private void onDirectionChanged(ValueChangedEvent<ScrollingDirection> direction)
        {
            Anchor = Origin = direction.NewValue == ScrollingDirection.Up ? Anchor.TopCentre : Anchor.BottomCentre;
        }
    }
}

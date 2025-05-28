// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Logging;
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

        private double bpm;
        private OsuConfigManager config = null!;
        private EzSkinSettingsManager? ezSkinConfig;
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
        private void load(OsuConfigManager config, EzSkinSettingsManager ezSkinConfig, IScrollingInfo scrollingInfo)
        {
            this.config = config;
            this.ezSkinConfig = ezSkinConfig;
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
            // Height = DrawWidth;

            double interval = 60000 / bpm;
            const double amplitude = 6.0;
            double progress = (gameplayClock.CurrentTime % interval) / interval;
            double smoothValue = smoothSineWave(progress);
            // var hit = config.GetBindable<double>(OsuSetting.VirtualHitPosition);

            columnWidthBindable = config.GetBindable<double>(OsuSetting.ColumnWidth);
            specialFactorBindable = config.GetBindable<double>(OsuSetting.SpecialFactor);
            bool isSpecialColumn = stageDefinition.EzIsSpecialColumn(column.Index);
            columnWidth.Value = columnWidthBindable.Value * (isSpecialColumn ? specialFactorBindable.Value : 1);

            double noteHeight = -(ezSkinConfig?.GetBindable<double>(EzSkinSetting.NonSquareNoteHeight).Value ?? 0);

            if (noteHeight == 0)
            {
                noteHeight = -(columnWidth.Value / 2);
            }

            Logger.Log($"EzHitExplosion: noteHeight={noteHeight}", LoggingTarget.Runtime, LogLevel.Debug);
            Y =  (float)noteHeight + (float)(smoothValue * amplitude);
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
            factory.OnTextureNameChanged -= onSkinChanged; // 取消订阅，防止内存泄漏
        }

        private void onDirectionChanged(ValueChangedEvent<ScrollingDirection> direction)
        {
            Anchor = Origin = direction.NewValue == ScrollingDirection.Up ? Anchor.TopCentre : Anchor.BottomCentre;
        }
    }
}

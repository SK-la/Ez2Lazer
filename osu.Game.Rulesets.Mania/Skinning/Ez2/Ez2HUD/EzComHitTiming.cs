using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Threading;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation.SkinComponents;
using osu.Game.Rulesets.Judgements;
using osu.Game.Screens.Play.HUD.HitErrorMeters;
using osu.Game.Skinning.Components;

namespace osu.Game.Rulesets.Mania.Skinning.Ez2.Ez2HUD
{
    [Cached]
    public partial class EzComHitTiming : HitErrorMeter
    {
        private FillFlowContainer errorContainer = null!;
        private SpriteText timingText = null!;
        private EzCounterText offsetText = null!;
        private Box backgroundBox = null!;

        [SettingSource("AloneShow", "Show only Early or: Late separately")]
        public Bindable<AloneShowMenu> AloneShow { get; } = new Bindable<AloneShowMenu>(AloneShowMenu.None);

        [SettingSource("Threshold", "Adjust the threshold for displaying values")]
        public BindableNumber<double> Threshold { get; } = new BindableNumber<double>(22)
        {
            MinValue = 0.0,
            MaxValue = 100.0,
            Precision = 1
        };

        [SettingSource("Display Duration", "Duration (in seconds) before text disappears")]
        public BindableNumber<double> DisplayDuration { get; } = new BindableNumber<double>(300)
        {
            MinValue = 10, // 最小持续时间
            MaxValue = 1000, // 最大持续时间
            Precision = 1, // 精度
        };

        [SettingSource("Font", "Font", SettingControlType = typeof(FontNameSelector))]
        public Bindable<string> FontNameDropdown { get; } = new Bindable<string>("argon");

        [SettingSource("Alpha", "The alpha value of this box")]
        public BindableNumber<float> BoxAlpha { get; } = new BindableNumber<float>(1)
        {
            MinValue = 0,
            MaxValue = 1,
            Precision = 0.01f,
        };

        [SettingSource(typeof(SkinnableComponentStrings), nameof(SkinnableComponentStrings.Colour), nameof(SkinnableComponentStrings.ColourDescription))]
        public BindableColour4 AccentColour { get; } = new BindableColour4(Colour4.White);

        public EzComHitTiming()
        {
            AutoSizeAxes = Axes.Both;
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
                backgroundBox = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Colour4.Black,
                    Alpha = 0f
                },
                errorContainer = new FillFlowContainer
                {
                    Direction = FillDirection.Vertical,
                    AutoSizeAxes = Axes.Both,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Alpha = 0,
                    Children = new Drawable[]
                    {
                        offsetText = new EzCounterText(Anchor.Centre, FontNameDropdown)
                        {
                            Anchor = Anchor.BottomCentre,
                            Origin = Anchor.BottomCentre,
                            Text = "±0.00",
                        },
                        timingText = new OsuSpriteText
                        {
                            Anchor = Anchor.BottomCentre,
                            Origin = Anchor.BottomCentre,
                            Text = "Early/Late",
                            Font = OsuFont.GetFont(size: 20),
                        },
                    }
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            BoxAlpha.BindValueChanged(alpha => errorContainer.Alpha = alpha.NewValue, true);
            AccentColour.BindValueChanged(_ => errorContainer.Colour = AccentColour.Value, true);

            FontNameDropdown.BindValueChanged(e =>
            {
                offsetText.FontName.Value = e.NewValue;
                offsetText.Invalidate();
            }, true);

            AloneShow.BindValueChanged(_ => Invalidate(), true);
            Threshold.BindValueChanged(_ => Invalidate(), true);
        }

        private ScheduledDelegate disappearTask = null!;

        protected override void OnNewJudgement(JudgementResult judgement)
        {
            if (!judgement.IsHit)
                return;

            if (!shouldDisplayJudgement(AloneShow.Value, judgement.TimeOffset))
                return;

            timingText.Text = judgement.TimeOffset < 0 ? "Early" : "Late";
            offsetText.Text = $"{judgement.TimeOffset:+0.00;-0.00}";
            backgroundBox.Colour = GetColourForHitResult(judgement.Type);

            errorContainer.FadeTo(BoxAlpha.Value, 10); // 渐现动画
            resetDisappearTask();
        }

        private void resetDisappearTask()
        {
            // 如果已有任务在运行，取消它
            disappearTask?.Cancel();

            // 启动新的任务，在持续时间后渐隐至透明度为零
            disappearTask = Scheduler.AddDelayed(() =>
            {
                errorContainer.FadeOutFromOne(300); // 渐隐动画
            }, DisplayDuration.Value); // 延时 DisplayDuration 的值（单位为毫秒）
        }

        private bool shouldDisplayJudgement(AloneShowMenu aloneShowMenu, double timeOffset)
        {
            if (Math.Abs(timeOffset) < Threshold.Value)
                return false;

            return aloneShowMenu switch
            {
                AloneShowMenu.Early => timeOffset < 0, // 仅显示负值
                AloneShowMenu.Late => timeOffset > 0, // 仅显示正值
                AloneShowMenu.None => true, // 显示全部
                _ => true
            };
        }

        public override void Clear()
        {
            timingText.Text = string.Empty;
            offsetText.Text = string.Empty;
            backgroundBox.Colour = Colour4.Black;
        }
    }

    public enum AloneShowMenu
    {
        Early,
        Late,
        None,
    }
}

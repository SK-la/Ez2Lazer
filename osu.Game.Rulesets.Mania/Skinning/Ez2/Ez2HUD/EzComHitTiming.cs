using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Threading;
using osu.Game.Configuration;
using osu.Game.Localisation.SkinComponents;
using osu.Game.Rulesets.Judgements;
using osu.Game.Screens.Play.HUD.HitErrorMeters;
using osu.Game.Skinning.Components;
using osuTK;

namespace osu.Game.Rulesets.Mania.Skinning.Ez2.Ez2HUD
{
    [Cached]
    public partial class EzComHitTiming : HitErrorMeter
    {
        private FillFlowContainer errorContainer = null!;
        private FillFlowContainer timingContainer = null!;
        private EzCounterText timingText1 = null!;
        private EzCounterText timingText = null!;
        private EzCounterText timingText3 = null!;
        private EzCounterText offsetText = null!;
        private Box backgroundBox = null!;

        [SettingSource("Offset Number Font", "Offset Number Font", SettingControlType = typeof(OffsetNumberNameSelector))]
        public Bindable<string> NumberNameDropdown { get; } = new Bindable<string>("Tomato");

        [SettingSource("Offset Text Font", "Offset Text Font", SettingControlType = typeof(OffsetTextNameSelector))]
        public Bindable<string> TextNameDropdown { get; } = new Bindable<string>("Tomato");

        [SettingSource("AloneShow", "Show only Early or: Late separately")]
        public Bindable<AloneShowMenu> AloneShow { get; } = new Bindable<AloneShowMenu>(AloneShowMenu.None);

        [SettingSource("Displaying Threshold", "(显示阈值) Displaying Threshold")]
        public BindableNumber<double> Threshold { get; } = new BindableNumber<double>(22)
        {
            MinValue = 0.0,
            MaxValue = 100.0,
            Precision = 1
        };

        [SettingSource("Display Duration", "(持续时间) Duration disappears")]
        public BindableNumber<double> DisplayDuration { get; } = new BindableNumber<double>(300)
        {
            MinValue = 10,
            MaxValue = 1000, // 最大持续时间
            Precision = 1, // 精度
        };

        [SettingSource("Symmetrical spacing", "(对称间距) Symmetrical spacing")]
        public BindableNumber<float> SymmetryOffset { get; } = new BindableNumber<float>(70)
        {
            MinValue = 0,
            MaxValue = 100,
            Precision = 1,
        };

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
            Size = new Vector2(300, 80);
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
                        offsetText = new EzCounterText(NumberNameDropdown)
                        {
                            Anchor = Anchor.BottomCentre,
                            Origin = Anchor.BottomCentre,
                            Text = "±000",
                        },
                        timingContainer = new FillFlowContainer
                        {
                            // Direction = FillDirection.Horizontal,
                            AutoSizeAxes = Axes.Both,
                            Anchor = Anchor.BottomCentre,
                            Origin = Anchor.BottomCentre,
                            Spacing = new Vector2(SymmetryOffset.Value),
                            Children = new Drawable[]
                            {
                                timingText1 = new EzCounterText(TextNameDropdown)
                                {
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.CentreRight,
                                    Text = "e",
                                    Alpha = 1
                                },
                                timingText = new EzCounterText(TextNameDropdown)
                                {
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    // Text = "e/l",
                                    Alpha = 0
                                },
                                timingText3 = new EzCounterText(TextNameDropdown)
                                {
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.CentreLeft,
                                    Text = "l",
                                    Alpha = 1
                                },
                            }
                        }
                    }
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            BoxAlpha.BindValueChanged(alpha => errorContainer.Alpha = alpha.NewValue, true);
            AccentColour.BindValueChanged(_ => errorContainer.Colour = AccentColour.Value, true);

            NumberNameDropdown.BindValueChanged(e =>
            {
                offsetText.FontName.Value = e.NewValue;
                offsetText.Invalidate();
            }, true);

            TextNameDropdown.BindValueChanged(e =>
            {
                timingText.FontName.Value = e.NewValue;
                timingText1.FontName.Value = e.NewValue;
                timingText3.FontName.Value = e.NewValue;
                timingText.Invalidate();
                timingText1.Invalidate();
                timingText3.Invalidate();
            }, true);

            AloneShow.BindValueChanged(_ => updateTimingTextVisibility(), true);
            SymmetryOffset.BindValueChanged(_ => updateTimingTextPositions(), true);
        }

        private void updateTimingTextVisibility()
        {
            timingText1.Alpha = AloneShow.Value == AloneShowMenu.None ? 1 : 0;
            timingText.Alpha = AloneShow.Value == AloneShowMenu.None ? 0 : 1;
            timingText3.Alpha = AloneShow.Value == AloneShowMenu.None ? 1 : 0;
        }

        private void updateTimingTextPositions()
        {
            timingContainer.Spacing = new Vector2(SymmetryOffset.Value);
            timingContainer.Invalidate();
        }

        protected override void OnNewJudgement(JudgementResult judgement)
        {
            if (!judgement.IsHit)
                return;

            if (!shouldDisplayJudgement(AloneShow.Value, judgement.TimeOffset))
                return;

            if (judgement.TimeOffset < 0)
            {
                timingText.Text = "e";
                timingText1.Text = "e";
                timingText3.Text = string.Empty;
            }
            else if (judgement.TimeOffset > 0)
            {
                timingText.Text = "l";
                timingText1.Text = string.Empty;
                timingText3.Text = "l";
            }
            else if (judgement.TimeOffset == 0)
            {
                timingText.Text = string.Empty;
                timingText1.Text = "e";
                timingText3.Text = "l";
            }

            offsetText.Text = judgement.TimeOffset == 0 ? "0" : $"{judgement.TimeOffset:+0;-0}";
            backgroundBox.Colour = GetColourForHitResult(judgement.Type);

            errorContainer.FadeTo(BoxAlpha.Value, 10); // 渐现动画
            resetDisappearTask();
        }

        private bool shouldDisplayJudgement(AloneShowMenu aloneShowMenu, double timeOffset)
        {
            if (timeOffset == 0)
                return true;
            if (Math.Abs(timeOffset) < Threshold.Value)
                return false;
            // return aloneShowMenu switch
            // {
            //     AloneShowMenu.Early => timeOffset < 0, // 仅显示负值
            //     AloneShowMenu.Late => timeOffset > 0, // 仅显示正值
            //     // AloneShowMenu.None => true, // 显示全部
            //     _ => true
            // };
            return true;
        }

        private ScheduledDelegate disappearTask = null!;

        private void resetDisappearTask()
        {
            // 如果已有任务在运行，取消它
            // ReSharper disable ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
            disappearTask?.Cancel();
            // ReSharper restore ConditionalAccessQualifierIsNonNullableAccordingToAPIContract

            // 启动新的任务，在持续时间后渐隐至透明度为零
            disappearTask = Scheduler.AddDelayed(() =>
            {
                errorContainer.FadeOutFromOne(300); // 渐隐动画
            }, DisplayDuration.Value); // 延时 DisplayDuration 的值（单位为毫秒）
        }

        public override void Clear()
        {
            timingText.Text = string.Empty;
            timingText1.Text = string.Empty;
            timingText3.Text = string.Empty;
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

    public partial class OffsetTextNameSelector : OffsetNumberNameSelector
    {
    }
}

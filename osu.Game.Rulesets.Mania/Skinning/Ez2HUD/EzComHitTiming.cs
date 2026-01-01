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

namespace osu.Game.Rulesets.Mania.Skinning.Ez2HUD
{
    public partial class EzComHitTiming : HitErrorMeter
    {
        [SettingSource("Offset Number Font", "Offset Number Font", SettingControlType = typeof(EzSelectorEnumList))]
        public Bindable<EzEnumGameThemeName> NumberNameDropdown { get; } = new Bindable<EzEnumGameThemeName>(EzSelectorEnumList.DEFAULT_NAME);

        [SettingSource("Offset Text Font", "Offset Text Font", SettingControlType = typeof(OffsetTextNameSelector))]
        public Bindable<EzEnumGameThemeName> TextNameDropdown { get; } = new Bindable<EzEnumGameThemeName>(EzSelectorEnumList.DEFAULT_NAME);

        [SettingSource("Single Show", "Show only Early or: Late separately")]
        public Bindable<AloneShowMenu> AloneShow { get; } = new Bindable<AloneShowMenu>(AloneShowMenu.None);

        [SettingSource("(显示阈值) Displaying Threshold", "(显示阈值) Displaying Threshold")]
        public BindableNumber<double> Threshold { get; } = new BindableNumber<double>(22)
        {
            MinValue = 0.0,
            MaxValue = 100.0,
            Precision = 1
        };

        [SettingSource("(持续时间) Display Duration", "(持续时间) Duration disappears")]
        public BindableNumber<double> DisplayDuration { get; } = new BindableNumber<double>(300)
        {
            MinValue = 10,
            MaxValue = 10000, // 最大持续时间
            Precision = 1, // 精度
        };

        [SettingSource("(对称间距) Symmetrical spacing", "(对称间距) Symmetrical spacing")]
        public BindableNumber<float> SymmetryOffset { get; } = new BindableNumber<float>(60)
        {
            MinValue = 0,
            MaxValue = 500,
            Precision = 1,
        };

        [SettingSource("Text Alpha", "The alpha value of this offset text")]
        public BindableNumber<float> TextAlpha { get; } = new BindableNumber<float>(1)
        {
            MinValue = 0,
            MaxValue = 1,
            Precision = 0.01f,
        };

        [SettingSource("Number Alpha", "The alpha value of the offset number")]
        public BindableNumber<float> NumberAlpha { get; } = new BindableNumber<float>(1)
        {
            MinValue = 0,
            MaxValue = 1,
            Precision = 0.01f,
        };

        [SettingSource(typeof(SkinnableComponentStrings), nameof(SkinnableComponentStrings.Colour))]
        public BindableColour4 AccentColour { get; } = new BindableColour4(Colour4.White);

        private Container timingContainer = null!;
        private FillFlowContainer errorContainer = null!;
        private EzComboText timingTextL = null!;
        private EzComboText timingText = null!;
        private EzComboText timingTextR = null!;
        private EzComboText offsetText = null!;
        private Box backgroundBox = null!;

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
                    Children = new Drawable[]
                    {
                        timingContainer = new Container
                        {
                            AutoSizeAxes = Axes.Both,
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Scale = new Vector2(2f),
                            // Spacing = new Vector2(SymmetryOffset.Value),
                            Children = new Drawable[]
                            {
                                timingTextL = new EzComboText(TextNameDropdown)
                                {
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    Text = "e",
                                    Alpha = 1,
                                    Position = new Vector2(-SymmetryOffset.Value, 0)
                                },
                                timingText = new EzComboText(TextNameDropdown)
                                {
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    Text = "e/l",
                                    Alpha = 0
                                },
                                timingTextR = new EzComboText(TextNameDropdown)
                                {
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    Text = "l",
                                    Alpha = 1,
                                    Position = new Vector2(SymmetryOffset.Value, 0)
                                },
                            }
                        },
                        offsetText = new EzComboText(NumberNameDropdown)
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Scale = new Vector2(1.5f),
                            Text = "±000",
                        },
                    }
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            TextAlpha.BindValueChanged(alpha => timingContainer.Alpha = alpha.NewValue, true);
            NumberAlpha.BindValueChanged(alpha => offsetText.Alpha = alpha.NewValue, true);
            AccentColour.BindValueChanged(_ => errorContainer.Colour = AccentColour.Value, true);

            NumberNameDropdown.BindValueChanged(e =>
            {
                offsetText.FontName.Value = e.NewValue;
                offsetText.Invalidate();
            }, true);

            TextNameDropdown.BindValueChanged(e =>
            {
                timingText.FontName.Value = e.NewValue;
                timingTextL.FontName.Value = e.NewValue;
                timingTextR.FontName.Value = e.NewValue;
                Invalidate();
                // timingText1.Invalidate();
                // timingText3.Invalidate();
            }, true);

            AloneShow.BindValueChanged(_ => updateAlpha(), true);
            SymmetryOffset.BindValueChanged(_ => updateSpacing(), true);
        }

        private void updateAlpha()
        {
            timingTextL.Alpha = AloneShow.Value == AloneShowMenu.None ? 1 : 0;
            timingText.Alpha = AloneShow.Value == AloneShowMenu.None ? 0 : 1;
            timingTextR.Alpha = AloneShow.Value == AloneShowMenu.None ? 1 : 0;
        }

        private void updateSpacing()
        {
            timingTextL.Position = new Vector2(-SymmetryOffset.Value, 0);
            timingTextR.Position = new Vector2(SymmetryOffset.Value, 0);

            timingContainer.Invalidate();
        }

        protected override void OnNewJudgement(JudgementResult judgement)
        {
            if (!judgement.IsHit)
                return;

            if (!shouldDisplayJudgement(AloneShow.Value, judgement.TimeOffset))
                return;

            if (judgement.TimeOffset == 0)
            {
                timingText.Text = "e/l";
                timingTextL.Text = "e";
                timingTextR.Text = "l";
            }
            else
            {
                timingTextL.Text = judgement.TimeOffset < 0 ? "e" : string.Empty;
                timingText.Text = judgement.TimeOffset < 0 ? "e" : "l";
                timingTextR.Text = judgement.TimeOffset < 0 ? string.Empty : "l";
            }

            offsetText.Text = judgement.TimeOffset == 0 ? "0" : $"{judgement.TimeOffset:+0;-0}";
            backgroundBox.Colour = GetColourForHitResult(judgement.Type);

            timingContainer.FadeTo(TextAlpha.Value, 10); // 渐现动画（上半文字）
            offsetText.FadeTo(NumberAlpha.Value, 10); // 渐现动画（下半数字）
            resetDisappearTask();
        }

        private bool hasTriggeredReset;

        private bool shouldDisplayJudgement(AloneShowMenu aloneShowMenu, double timeOffset)
        {
            if (!hasTriggeredReset)
            {
                resetDisappearTask(); // 第一次判定时触发任务
                hasTriggeredReset = true;
            }

            if (timeOffset == 0)
                return true;

            if (Math.Abs(timeOffset) < Threshold.Value)
            {
                return false;
            }

            return aloneShowMenu switch
            {
                AloneShowMenu.Early => timeOffset < 0,
                AloneShowMenu.Late => timeOffset > 0,
                _ => true
            };
            // return true;
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
                timingContainer.FadeOut(300); // 渐隐动画（上半文字）
                offsetText.FadeOut(300); // 渐隐动画（下半数字）
            }, DisplayDuration.Value);
        }

        public override void Clear()
        {
            timingText.Text = string.Empty;
            timingTextL.Text = string.Empty;
            timingTextR.Text = string.Empty;
            offsetText.Text = string.Empty;
            backgroundBox.Colour = Colour4.Black;

            foreach (var j in errorContainer)
            {
                j.ClearTransforms();
                j.Expire();
            }
        }
    }

    public enum AloneShowMenu
    {
        Early,
        Late,
        None,
    }

    public partial class OffsetTextNameSelector : EzSelectorEnumList
    {
    }
}

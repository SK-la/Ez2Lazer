// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Threading;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.HUD;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Localisation.SkinComponents;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.EzMania.Localization;
using osu.Game.Screens.Play.HUD.HitErrorMeters;
using osuTK;

namespace osu.Game.Rulesets.Mania.EzMania.HUD
{
    public partial class EzHUDHitTiming : HitErrorMeter
    {
        [SettingSource(typeof(EzHUDManiaStrings), nameof(EzHUDManiaStrings.OFFSET_NUMBER_FONT_LABEL), nameof(EzHUDManiaStrings.OFFSET_NUMBER_FONT_DESCRIPTION), SettingControlType = typeof(EzSelectorEnumList))]
        public Bindable<EzEnumGameThemeName> NumberFont { get; } = new Bindable<EzEnumGameThemeName>(EzSelectorEnumList.DEFAULT_NAME);

        [SettingSource(typeof(EzHUDManiaStrings), nameof(EzHUDManiaStrings.OFFSET_TEXT_FONT_LABEL), nameof(EzHUDManiaStrings.OFFSET_TEXT_FONT_DESCRIPTION), SettingControlType = typeof(OffsetTextNameSelector))]
        public Bindable<EzEnumGameThemeName> TextFont { get; } = new Bindable<EzEnumGameThemeName>(EzSelectorEnumList.DEFAULT_NAME);

        [SettingSource(typeof(EzHUDManiaStrings), nameof(EzHUDManiaStrings.SINGLE_SHOW_EL_LABEL), nameof(EzHUDManiaStrings.SINGLE_SHOW_EL_DESCRIPTION))]
        public Bindable<AloneShowMenu> AloneShow { get; } = new Bindable<AloneShowMenu>(AloneShowMenu.None);

        [SettingSource(typeof(EzHUDManiaStrings), nameof(EzHUDManiaStrings.DISPLAYING_THRESHOLD_LABEL), nameof(EzHUDManiaStrings.DISPLAYING_THRESHOLD_DESCRIPTION))]
        public BindableNumber<double> Threshold { get; } = new BindableNumber<double>(22)
        {
            MinValue = 0.0,
            MaxValue = 100.0,
            Precision = 1
        };

        [SettingSource(typeof(EzHUDManiaStrings), nameof(EzHUDManiaStrings.DISPLAY_DURATION_LABEL), nameof(EzHUDManiaStrings.DISPLAY_DURATION_DESCRIPTION))]
        public BindableNumber<double> DisplayDuration { get; } = new BindableNumber<double>(300)
        {
            MinValue = 10,
            MaxValue = 10000, // 最大持续时间
            Precision = 1, // 精度
        };

        [SettingSource(typeof(EzHUDManiaStrings), nameof(EzHUDManiaStrings.SYMMETRY_OFFSET_LABEL), nameof(EzHUDManiaStrings.SYMMETRY_OFFSET_DESCRIPTION))]
        public BindableNumber<float> SymmetryOffset { get; } = new BindableNumber<float>(60)
        {
            MinValue = 0,
            MaxValue = 500,
            Precision = 1,
        };

        [SettingSource(typeof(EzHUDManiaStrings), nameof(EzHUDManiaStrings.TEXT_ALPHA_LABEL), nameof(EzHUDManiaStrings.TEXT_ALPHA_DESCRIPTION))]
        public BindableNumber<float> TextAlpha { get; } = new BindableNumber<float>(1)
        {
            MinValue = 0,
            MaxValue = 1,
            Precision = 0.01f,
        };

        [SettingSource(typeof(EzHUDManiaStrings), nameof(EzHUDManiaStrings.NUMBER_ALPHA_LABEL), nameof(EzHUDManiaStrings.NUMBER_ALPHA_DESCRIPTION))]
        public BindableNumber<float> NumberAlpha { get; } = new BindableNumber<float>(1)
        {
            MinValue = 0,
            MaxValue = 1,
            Precision = 0.01f,
        };

        [SettingSource(typeof(SkinnableComponentStrings), nameof(SkinnableComponentStrings.Colour))]
        public BindableColour4 AccentColour { get; } = new BindableColour4(Colour4.White);

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.TEST_MODE_LABEL), nameof(EzHUDStrings.TEST_MODE_DESCRIPTION))]
        public Bindable<bool> TestMode { get; } = new Bindable<bool>();

        private Container timingContainer = null!;
        private FillFlowContainer errorContainer = null!;
        private EzComboText timingTextL = null!;
        private EzComboText timingText = null!;
        private EzComboText timingTextR = null!;
        private EzComboText offsetText = null!;
        private Box backgroundBox = null!;

        public EzHUDHitTiming()
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
                                timingTextL = new EzComboText
                                {
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    Text = "e",
                                    Alpha = 1,
                                    Position = new Vector2(-SymmetryOffset.Value, 0)
                                },
                                timingText = new EzComboText
                                {
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    Text = "e/l",
                                    Alpha = 0
                                },
                                timingTextR = new EzComboText
                                {
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    Text = "l",
                                    Alpha = 1,
                                    Position = new Vector2(SymmetryOffset.Value, 0)
                                },
                            }
                        },
                        offsetText = new EzComboText
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

            NumberFont.BindValueChanged(e =>
            {
                offsetText.FontName.Value = e.NewValue;
                offsetText.Invalidate();
            }, true);

            TextFont.BindValueChanged(e =>
            {
                timingText.FontName.Value = e.NewValue;
                timingTextL.FontName.Value = e.NewValue;
                timingTextR.FontName.Value = e.NewValue;
                Invalidate();
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
                if (TestMode.Value)
                {
                    // 测试模式：显示 E 和 L
                    timingText.Text = "e/l";
                    timingTextL.Text = "e";
                    timingTextR.Text = "l";
                }
                else
                {
                    // 默认：不显示
                    timingText.Text = string.Empty;
                    timingTextL.Text = string.Empty;
                    timingTextR.Text = string.Empty;
                }
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

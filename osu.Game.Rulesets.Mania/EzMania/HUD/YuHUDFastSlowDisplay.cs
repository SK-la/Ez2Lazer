// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation.SkinComponents;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play.HUD.HitErrorMeters;

namespace osu.Game.Rulesets.Mania.EzMania.HUD
{
    /// <summary>
    /// 判定快慢显示 HUD 组件。可以自定义 表示Early/Late的字符
    /// 代码文件来自于 YuLiangSSS。
    /// </summary>
    public partial class YuHUDFastSlowDisplay : HitErrorMeter
    {
        public const float DEFAULT_FONT_SIZE = 25f;

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private IBindable<IReadOnlyList<Mod>> mods { get; set; } = null!;

        [Resolved]
        private IBindable<WorkingBeatmap> beatmap { get; set; } = null!;

        [SettingSource(typeof(FastSlowDisplayStrings), nameof(FastSlowDisplayStrings.SHOW_JUDGEMENT), nameof(FastSlowDisplayStrings.SHOW_STYLE_DESCRIPTION))]
        public Bindable<EzEnumHitResult> Judgement { get; } = new Bindable<EzEnumHitResult>();

        [SettingSource(typeof(FastSlowDisplayStrings), nameof(FastSlowDisplayStrings.GAP), nameof(FastSlowDisplayStrings.GAP_DESCRIPTION))]
        public BindableNumber<float> Gap { get; } = new BindableNumber<float>(50)
        {
            MinValue = -200,
            MaxValue = 200,
            Precision = 0.1f,
        };

        [SettingSource(typeof(FastSlowDisplayStrings), nameof(FastSlowDisplayStrings.FADE_DURATION), nameof(FastSlowDisplayStrings.FADE_DURATION_DESCRIPTION))]
        public BindableNumber<double> FadeDuration { get; } = new BindableNumber<double>(430)
        {
            MinValue = 0,
            MaxValue = 2000,
            Precision = 10,
        };

        [SettingSource(typeof(SkinnableComponentStrings), nameof(SkinnableComponentStrings.Font))]
        public Bindable<Typeface> Font { get; } = new Bindable<Typeface>(Typeface.Torus);

        [SettingSource(typeof(FastSlowDisplayStrings), nameof(FastSlowDisplayStrings.FONT_SIZE), nameof(FastSlowDisplayStrings.FONT_SIZE_DESCRIPTION))]
        public BindableNumber<float> FontSize { get; } = new BindableNumber<float>(DEFAULT_FONT_SIZE)
        {
            MinValue = 1,
            MaxValue = 100,
            Precision = 0.1f,
        };

        [SettingSource(typeof(FastSlowDisplayStrings), nameof(FastSlowDisplayStrings.FAST_TEXT), nameof(FastSlowDisplayStrings.TEXT_DESCRIPTION))]
        public Bindable<string> FastText { get; } = new Bindable<string>("Fast");

        [SettingSource(typeof(FastSlowDisplayStrings), nameof(FastSlowDisplayStrings.SLOW_TEXT), nameof(FastSlowDisplayStrings.TEXT_DESCRIPTION))]
        public Bindable<string> SlowText { get; } = new Bindable<string>("Slow");

        [SettingSource(typeof(FastSlowDisplayStrings), nameof(FastSlowDisplayStrings.FAST_COLOUR_STYLE), nameof(FastSlowDisplayStrings.FAST_COLOUR_STYLE_DESCRIPTION))]
        public Bindable<YuColourStyle> FastColourStyle { get; } = new Bindable<YuColourStyle>();

        [SettingSource(typeof(FastSlowDisplayStrings), nameof(FastSlowDisplayStrings.FAST_COLOUR), nameof(FastSlowDisplayStrings.TEXT_COLOUR_DESCRIPTION))]
        public BindableColour4 FastColour { get; } = new BindableColour4(Colour4.FromHex("#97A5FF"));

        [SettingSource(typeof(FastSlowDisplayStrings), nameof(FastSlowDisplayStrings.FAST_COLOUR), nameof(FastSlowDisplayStrings.TEXT_COLOUR_DESCRIPTION))]
        public BindableColour4 FastColourGradient { get; } = new BindableColour4(Colour4.LightPink);

        [SettingSource(typeof(FastSlowDisplayStrings), nameof(FastSlowDisplayStrings.SLOW_COLOUR_STYLE), nameof(FastSlowDisplayStrings.SLOW_COLOUR_STYLE_DESCRIPTION))]
        public Bindable<YuColourStyle> SlowColourStyle { get; } = new Bindable<YuColourStyle>();

        [SettingSource(typeof(FastSlowDisplayStrings), nameof(FastSlowDisplayStrings.SLOW_COLOUR), nameof(FastSlowDisplayStrings.TEXT_COLOUR_DESCRIPTION))]
        public BindableColour4 SlowColour { get; } = new BindableColour4(Colour4.FromHex("#D1FF74"));

        [SettingSource(typeof(FastSlowDisplayStrings), nameof(FastSlowDisplayStrings.SLOW_COLOUR), nameof(FastSlowDisplayStrings.TEXT_COLOUR_DESCRIPTION))]
        public BindableColour4 SlowColourGradient { get; } = new BindableColour4(Colour4.LightCyan);

        [SettingSource(typeof(FastSlowDisplayStrings), nameof(FastSlowDisplayStrings.DISPLAY_STYLE), nameof(FastSlowDisplayStrings.DISPLAY_STYLE_DESCRIPTION))]
        public BindableBool DisplayStyle { get; } = new BindableBool(false);

        [SettingSource(typeof(FastSlowDisplayStrings), nameof(FastSlowDisplayStrings.LOWER_COLUMN), nameof(FastSlowDisplayStrings.LOWER_COLUMN_DESCRIPTION))]
        public BindableNumber<int> LowerColumnBound { get; } = new BindableNumber<int>(1)
        {
            MinValue = 1,
            MaxValue = 18,
            Precision = 1,
        };

        [SettingSource(typeof(FastSlowDisplayStrings), nameof(FastSlowDisplayStrings.UPPER_COLUMN), nameof(FastSlowDisplayStrings.UPPER_COLUMN_DESCRIPTION))]
        public BindableNumber<int> UpperColumnBound { get; } = new BindableNumber<int>(18)
        {
            MinValue = 1,
            MaxValue = 18,
            Precision = 1,
        };

        [SettingSource(typeof(FastSlowDisplayStrings), nameof(FastSlowDisplayStrings.ONLY_DISPLAY_ONE), nameof(FastSlowDisplayStrings.ONLY_DISPLAY_ONE_DESCRIPTION))]
        public BindableBool OnlyDisplayOne { get; } = new BindableBool(false);

        [SettingSource(typeof(FastSlowDisplayStrings), nameof(FastSlowDisplayStrings.SELECT_COLUMN), nameof(FastSlowDisplayStrings.SELECT_COLUMN_DESCRIPTION))]
        public Bindable<YuColumnPosition> SelectColumn { get; } = new Bindable<YuColumnPosition>();

        private Container textContainer = null!;
        private Container fast = null!;
        private Container slow = null!;
        private Container test = null!;

        private OsuSpriteText displayFastText = null!;
        private OsuSpriteText displaySlowText = null!;
        private OsuSpriteText testText = null!;

        private string fastTextString = string.Empty;
        private string slowTextString = string.Empty;
        private string fastTextLNString = string.Empty;
        private string slowTextLNString = string.Empty;

        private readonly BindableNumber<float> gap = new BindableNumber<float>();

        public YuHUDFastSlowDisplay()
        {
            AutoSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            const int text_height = 20;
            const int text_width = 250;

            InternalChild = new Container
            {
                Height = text_height,
                Width = text_width,
                Margin = new MarginPadding(2),
                Children = new Drawable[]
                {
                    textContainer = new Container
                    {
                        Name = "fast slow text",
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        RelativeSizeAxes = Axes.Y,
                        Children = new Drawable[]
                        {
                            fast = new Container
                            {
                                Name = "fast",
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                X = Gap.Value,
                                Children = new Drawable[]
                                {
                                    displayFastText = new OsuSpriteText
                                    {
                                        Font = OsuFont.Numeric.With(size: FontSize.Value),
                                        Anchor = Anchor.Centre,
                                        Origin = Anchor.Centre,
                                    }
                                }
                            },
                            slow = new Container
                            {
                                Name = "slow",
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                X = -Gap.Value,
                                Children = new Drawable[]
                                {
                                    displaySlowText = new OsuSpriteText
                                    {
                                        Font = OsuFont.Numeric.With(size: FontSize.Value),
                                        Anchor = Anchor.Centre,
                                        Origin = Anchor.Centre,
                                    }
                                }
                            },

                            test = new Container
                            {
                                Name = "test",
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Y = Gap.Value,
                                Children = new Drawable[]
                                {
                                    testText = new OsuSpriteText
                                    {
                                        Text = "Test",
                                        Font = OsuFont.Numeric.With(size: FontSize.Value),
                                        Anchor = Anchor.Centre,
                                        Origin = Anchor.Centre,
                                        Colour = Colour4.White,
                                        Alpha = 0
                                    }
                                }
                            }
                        }
                    }
                }
            };

            //displayFastText.Current.BindTo(FastText);
            //displaySlowText.Current.BindTo(SlowText);

            displayFastText.Text = FastText.Value;
            displaySlowText.Text = SlowText.Value;
            testText.Current.BindTo(TestText);
            gap.BindTo(Gap);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Gap.BindValueChanged(e => SetGap(e.NewValue), true);

            DisplayStyle.BindValueChanged(e => SetDisplayStyle(e.NewValue), true);

            SaveText();

            FastText.BindValueChanged(e => SaveText(), true);
            SlowText.BindValueChanged(e => SaveText(), true);
            FastTextLN.BindValueChanged(e => SaveText(), true);
            SlowTextLN.BindValueChanged(e => SaveText(), true);

            FastColour.BindValueChanged(e => SetFastTextColour(e.NewValue, FastColourGradient.Value), true);
            SlowColour.BindValueChanged(e => SetSlowTextColour(e.NewValue, SlowColourGradient.Value), true);

            FastColourGradient.BindValueChanged(e => SetFastTextColour(FastColour.Value, e.NewValue), true);
            SlowColourGradient.BindValueChanged(e => SetSlowTextColour(SlowColour.Value, e.NewValue), true);

            FastColourStyle.BindValueChanged(e => applyFastColourStyle(e.NewValue), true);
            SlowColourStyle.BindValueChanged(e => applySlowColourStyle(e.NewValue), true);

            FontSize.BindValueChanged(e => SetFontSize(e.NewValue), true);
            Font.BindValueChanged(e =>
            {
                // We only have bold weight for venera, so let's force that.
                var fontWeight = e.NewValue == Typeface.Venera ? FontWeight.Bold : FontWeight.Regular;

                var f = OsuFont.GetFont(e.NewValue, weight: fontWeight);
                SetFastFont(f);
                SetSlowFont(f);
                SetTestFont(f);
            }, true);

            beatmap.BindValueChanged(_ => Reset(), true);

            //fastText.FadeOut(FadeDuration.Value, Easing.OutQuint);
            //slowText.FadeOut(FadeDuration.Value, Easing.OutQuint);
            //testText.FadeOut(FadeDuration.Value, Easing.OutQuint);
            displayFastText.Alpha = 0;
            displaySlowText.Alpha = 0;

            testText.Colour = randomColourInfo();

            Test.BindValueChanged(e => testText.Alpha = e.NewValue ? 1 : 0, true);
        }

        /// <summary>
        /// 应用 Fast 颜色样式
        /// </summary>
        private void applyFastColourStyle(YuColourStyle style)
        {
            var colour = style == YuColourStyle.SingleColour ? FastColour.Value : FastColourGradient.Value;
            SetFastTextColour(FastColour.Value, colour);
        }

        /// <summary>
        /// 应用 Slow 颜色样式
        /// </summary>
        private void applySlowColourStyle(YuColourStyle style)
        {
            var colour = style == YuColourStyle.SingleColour ? SlowColour.Value : SlowColourGradient.Value;
            SetSlowTextColour(SlowColour.Value, colour);
        }

        protected void Reset()
        {
        }

        protected void SaveText()
        {
            fastTextString = FastText.Value;
            slowTextString = SlowText.Value;
            fastTextLNString = FastTextLN.Value;
            slowTextLNString = SlowTextLN.Value;
        }

        private ColourInfo randomColourInfo()
        {
            var random = new Random();
            return random.Next(3) switch
            {
                0 => ColourInfo.SingleColour(randomColour()),
                1 => ColourInfo.GradientHorizontal(randomColour(), randomColour()),
                2 => ColourInfo.GradientVertical(randomColour(), randomColour()),
                _ => ColourInfo.SingleColour(Colour4.White)
            };
        }

        private Colour4 randomColour()
        {
            var random = new Random();
            return new Colour4((float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble(), 1);
        }

        protected void SetDisplayStyle(bool value)
        {
            float gapValue = Gap.Value;

            if (value)
            {
                // 垂直布局
                fast.X = slow.X = test.Y = 0;
                fast.Y = gapValue;
                slow.Y = -gapValue;
                test.X = gapValue;
            }
            else
            {
                // 水平布局
                fast.Y = slow.Y = test.X = 0;
                fast.X = gapValue;
                slow.X = -gapValue;
                test.Y = gapValue;
            }
        }

        protected void SetFontSize(float value)
        {
            FontSize.Value = value;
            SetFastFont(displayFastText.Font.With(size: value));
            SetSlowFont(displaySlowText.Font.With(size: value));
            SetTestFont(testText.Font.With(size: value));
        }

        protected void SetFastFont(FontUsage font)
        {
            displayFastText.Font = font.With(size: FontSize.Value);
        }

        protected void SetSlowFont(FontUsage font)
        {
            displaySlowText.Font = font.With(size: FontSize.Value);
        }

        protected void SetTestFont(FontUsage font)
        {
            testText.Font = font.With(size: FontSize.Value);
        }

        protected void SetGap(float value)
        {
            gap.Value = value;

            if (DisplayStyle.Value)
            {
                // 垂直布局
                fast.X = slow.X = test.Y = 0;
                fast.Y = value;
                slow.Y = -value;
                test.X = value;
            }
            else
            {
                // 水平布局
                fast.Y = slow.Y = test.X = 0;
                fast.X = value;
                slow.X = -value;
                test.Y = value;
            }
        }

        protected void SetFastTextColour(Colour4 colour, Colour4? gradient = null)
        {
            FastColour.Value = colour;
            displayFastText.Colour = applyGradient(colour, gradient, FastColourStyle.Value);

            if (gradient.HasValue && FastColourStyle.Value != YuColourStyle.SingleColour)
                FastColourGradient.Value = gradient.Value;
        }

        protected void SetSlowTextColour(Colour4 colour, Colour4? gradient = null)
        {
            SlowColour.Value = colour;
            displaySlowText.Colour = applyGradient(colour, gradient, SlowColourStyle.Value);

            if (gradient.HasValue && SlowColourStyle.Value != YuColourStyle.SingleColour)
                SlowColourGradient.Value = gradient.Value;
        }

        /// <summary>
        /// 根据颜色样式应用渐变效果
        /// </summary>
        private ColourInfo applyGradient(Colour4 baseColour, Colour4? gradient, YuColourStyle style)
        {
            if (!gradient.HasValue || style == YuColourStyle.SingleColour)
                return baseColour;

            return style switch
            {
                YuColourStyle.HorizontalGradient => ColourInfo.GradientHorizontal(baseColour, gradient.Value),
                YuColourStyle.VerticalGradient =>   ColourInfo.GradientVertical(baseColour, gradient.Value),
                _ => baseColour
            };
        }

        public override void Clear()
        {
        }

        protected override void OnNewJudgement(JudgementResult judgement)
        {
            if (!judgement.IsHit || judgement.HitObject.HitWindows == null)
                return;

            var hitResult = judgement.Type;

            if (!hitResult.IsBasic())
                return;

            bool shouldSkip = hitResult.GetIndexForOrderedDisplay() < Judgement.Value.GetIndexForOrderedDisplay();

            if (shouldSkip || Test.Value)
            {
                var originalColumn = (IHasColumn)judgement.HitObject;
                checkColumn(judgement, originalColumn);
            }
        }

        /// <summary>
        /// 检查判定是否应该显示在指定列上
        /// </summary>
        private void checkColumn(JudgementResult judgement, IHasColumn? originalColumn)
        {
            if (originalColumn is null)
                return;

            try
            {
                int column = originalColumn.Column + 1;
                int keys = ruleset.Value.CreateInstance().GetVariantForBeatmap(beatmap.Value.BeatmapInfo, mods.Value);

                if (isTargetColumn(column, keys))
                    displayResult(judgement);
            }
            catch (Exception)
            {
                // Ignore
            }
        }

        /// <summary>
        /// 判断是否为需要显示的目标列
        /// </summary>
        private bool isTargetColumn(int column, int keys)
        {
            return SelectColumn.Value switch
            {
                YuColumnPosition.Middle =>    keys % 2 != 0 && column == (keys / 2) + 1,
                YuColumnPosition.RightHalf => column > (keys / 2.0) && (keys % 2 == 0 || column > (keys / 2) + 1),
                YuColumnPosition.LeftHalf =>  column <= keys / 2.0,
                YuColumnPosition.None =>      column >= LowerColumnBound.Value && column <= UpperColumnBound.Value,
                _ => false
            };
        }

        /// <summary>
        /// 显示判定结果（Early/Late）
        /// </summary>
        private void displayResult(JudgementResult judgement)
        {
            // 测试模式
            if (Test.Value)
            {
                fadeOutAllTexts();
                updateTextsForTest(judgement);
                return;
            }

            // 根据时间偏移显示 Early 或 Late
            if (judgement.TimeOffset < 0)
            {
                showEarly(judgement);
            }
            else if (judgement.TimeOffset > 0)
            {
                showLate(judgement);
            }
        }

        /// <summary>
        /// 淡出所有文本
        /// </summary>
        private void fadeOutAllTexts()
        {
            displayFastText.FadeOutFromOne(FadeDuration.Value, Easing.OutQuint);
            displaySlowText.FadeOutFromOne(FadeDuration.Value, Easing.OutQuint);
            testText.FadeOutFromOne(FadeDuration.Value, Easing.OutQuint);
        }

        /// <summary>
        /// 测试模式下更新文本
        /// </summary>
        private void updateTextsForTest(JudgementResult judgement)
        {
            if (!LNSwitch.Value)
                return;

            bool isTailNote = judgement.HitObject is TailNote;
            displayFastText.Text = isTailNote ? fastTextLNString : fastTextString;
            displaySlowText.Text = isTailNote ? slowTextLNString : slowTextString;
        }

        /// <summary>
        /// 显示 Early（按下过早）
        /// </summary>
        private void showEarly(JudgementResult judgement)
        {
            updateFastText(judgement);
            displayFastText.FadeOutFromOne(FadeDuration.Value, Easing.OutQuint);

            if (OnlyDisplayOne.Value)
                displaySlowText.FadeOut(0);
        }

        /// <summary>
        /// 显示 Late（按下过晚）
        /// </summary>
        private void showLate(JudgementResult judgement)
        {
            updateSlowText(judgement);
            displaySlowText.FadeOutFromOne(FadeDuration.Value, Easing.OutQuint);

            if (OnlyDisplayOne.Value)
                displayFastText.FadeOut(0);
        }

        /// <summary>
        /// 根据音符类型更新 Fast 文本
        /// </summary>
        private void updateFastText(JudgementResult judgement)
        {
            if (!LNSwitch.Value)
                return;

            displayFastText.Text = judgement.HitObject switch
            {
                TailNote => fastTextLNString,
                _ =>        fastTextString
            };
        }

        /// <summary>
        /// 根据音符类型更新 Slow 文本
        /// </summary>
        private void updateSlowText(JudgementResult judgement)
        {
            if (!LNSwitch.Value)
                return;

            displaySlowText.Text = judgement.HitObject switch
            {
                TailNote => slowTextLNString,
                _ =>        slowTextString
            };
        }

        [SettingSource(typeof(FastSlowDisplayStrings), nameof(FastSlowDisplayStrings.LN_SWITCH), nameof(FastSlowDisplayStrings.LN_SWITCH_DESCRIPTION))]
        public BindableBool LNSwitch { get; } = new BindableBool(false);

        [SettingSource(typeof(FastSlowDisplayStrings), nameof(FastSlowDisplayStrings.FAST_TEXT_LN), nameof(FastSlowDisplayStrings.TEXT_DESCRIPTION))]
        public Bindable<string> FastTextLN { get; } = new Bindable<string>("Fast");

        [SettingSource(typeof(FastSlowDisplayStrings), nameof(FastSlowDisplayStrings.SLOW_TEXT_LN), nameof(FastSlowDisplayStrings.TEXT_DESCRIPTION))]
        public Bindable<string> SlowTextLN { get; } = new Bindable<string>("Slow");

        [SettingSource(typeof(FastSlowDisplayStrings), nameof(FastSlowDisplayStrings.TEST), nameof(FastSlowDisplayStrings.TEST_DESCRIPTION))]
        public BindableBool Test { get; } = new BindableBool();

        [SettingSource(typeof(SkinnableComponentStrings), nameof(SkinnableComponentStrings.TextElementText))]
        public Bindable<string> TestText { get; } = new Bindable<string>("Test");
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation.SkinComponents;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.LAsEZMania;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play.HUD.HitErrorMeters;

namespace osu.Game.Rulesets.Mania.Skinning.Ez2HUD
{
    /// <summary>
    /// 判定快慢显示 HUD 组件。可以自定义 表示Early/Late的字符
    /// 代码文件来自于 YuLiangSSS。
    /// </summary>
    public partial class YuComFastSlowDisplay : HitErrorMeter
    {
        public const float DEFAULT_FONT_SIZE = 25f;

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private IBindable<IReadOnlyList<Mod>> mods { get; set; } = null!;

        [Resolved]
        private IBindable<WorkingBeatmap> beatmap { get; set; } = null!;

        [SettingSource(typeof(FastSlowDisplayStrings), nameof(FastSlowDisplayStrings.ShowJudgement), nameof(FastSlowDisplayStrings.ShowStyleDescription))]
        public Bindable<Judgements> Judgement { get; } = new Bindable<Judgements>(Judgements.Perfect);

        [SettingSource(typeof(FastSlowDisplayStrings), nameof(FastSlowDisplayStrings.Gap), nameof(FastSlowDisplayStrings.GapDescription))]
        public BindableNumber<float> Gap { get; } = new BindableNumber<float>
        {
            MinValue = -200,
            MaxValue = 200,
            Precision = 0.1f,
        };

        [SettingSource(typeof(FastSlowDisplayStrings), nameof(FastSlowDisplayStrings.FadeDuration), nameof(FastSlowDisplayStrings.FadeDurationDescription))]
        public BindableNumber<double> FadeDuration { get; } = new BindableNumber<double>(100)
        {
            MinValue = 0,
            MaxValue = 2000,
            Precision = 10,
        };

        [SettingSource(typeof(SkinnableComponentStrings), nameof(SkinnableComponentStrings.Font))]
        public Bindable<Typeface> Font { get; } = new Bindable<Typeface>(Typeface.Torus);

        [SettingSource(typeof(FastSlowDisplayStrings), nameof(FastSlowDisplayStrings.FontSize), nameof(FastSlowDisplayStrings.FontSizeDescription))]
        public BindableNumber<float> FontSize { get; } = new BindableNumber<float>(DEFAULT_FONT_SIZE)
        {
            MinValue = 1,
            MaxValue = 100,
            Precision = 0.1f,
        };

        [SettingSource(typeof(FastSlowDisplayStrings), nameof(FastSlowDisplayStrings.FastText), nameof(FastSlowDisplayStrings.TextDescription))]
        public Bindable<string> FastText { get; } = new Bindable<string>("Fast");

        [SettingSource(typeof(FastSlowDisplayStrings), nameof(FastSlowDisplayStrings.SlowText), nameof(FastSlowDisplayStrings.TextDescription))]
        public Bindable<string> SlowText { get; } = new Bindable<string>("Slow");

        [SettingSource(typeof(FastSlowDisplayStrings), nameof(FastSlowDisplayStrings.FastColourStyle), nameof(FastSlowDisplayStrings.FastColourStyleDescription))]
        public Bindable<ColourStyle> FastColourStyle { get; } = new Bindable<ColourStyle>();

        [SettingSource(typeof(FastSlowDisplayStrings), nameof(FastSlowDisplayStrings.FastColour), nameof(FastSlowDisplayStrings.TextColourDescription))]
        public BindableColour4 FastColour { get; } = new BindableColour4(Colour4.FromHex("#97A5FF"));

        [SettingSource(typeof(FastSlowDisplayStrings), nameof(FastSlowDisplayStrings.FastColour), nameof(FastSlowDisplayStrings.TextColourDescription))]
        public BindableColour4 FastColourGradient { get; } = new BindableColour4(Colour4.LightPink);

        [SettingSource(typeof(FastSlowDisplayStrings), nameof(FastSlowDisplayStrings.SlowColourStyle), nameof(FastSlowDisplayStrings.SlowColourStyleDescription))]
        public Bindable<ColourStyle> SlowColourStyle { get; } = new Bindable<ColourStyle>();

        [SettingSource(typeof(FastSlowDisplayStrings), nameof(FastSlowDisplayStrings.SlowColour), nameof(FastSlowDisplayStrings.TextColourDescription))]
        public BindableColour4 SlowColour { get; } = new BindableColour4(Colour4.FromHex("#D1FF74"));

        [SettingSource(typeof(FastSlowDisplayStrings), nameof(FastSlowDisplayStrings.SlowColour), nameof(FastSlowDisplayStrings.TextColourDescription))]
        public BindableColour4 SlowColourGradient { get; } = new BindableColour4(Colour4.LightCyan);

        [SettingSource(typeof(FastSlowDisplayStrings), nameof(FastSlowDisplayStrings.DisplayStyle), nameof(FastSlowDisplayStrings.DisplayStyleDescription))]
        public BindableBool DisplayStyle { get; } = new BindableBool(false);

        [SettingSource(typeof(FastSlowDisplayStrings), nameof(FastSlowDisplayStrings.LowerColumn), nameof(FastSlowDisplayStrings.LowerColumnDescription))]
        public BindableNumber<int> LowerColumnBound { get; } = new BindableNumber<int>(1)
        {
            MinValue = 1,
            MaxValue = 18,
            Precision = 1,
        };

        [SettingSource(typeof(FastSlowDisplayStrings), nameof(FastSlowDisplayStrings.UpperColumn), nameof(FastSlowDisplayStrings.UpperColumnDescription))]
        public BindableNumber<int> UpperColumnBound { get; } = new BindableNumber<int>(18)
        {
            MinValue = 1,
            MaxValue = 18,
            Precision = 1,
        };

        [SettingSource(typeof(FastSlowDisplayStrings), nameof(FastSlowDisplayStrings.OnlyDisplayOne), nameof(FastSlowDisplayStrings.OnlyDisplayOneDescription))]
        public BindableBool OnlyDisplayOne { get; } = new BindableBool(false);

        [SettingSource(typeof(FastSlowDisplayStrings), nameof(FastSlowDisplayStrings.SelectColumn), nameof(FastSlowDisplayStrings.SelectColumnDescription))]
        public Bindable<Column> SelectColumn { get; } = new Bindable<Column>();

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

        private BindableNumber<float> gap = new BindableNumber<float>();

        private (HitResult result, double length)[] hitWindows = null!;

        public YuComFastSlowDisplay()
        {
            AutoSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            const int text_height = 20;
            const int text_width = 250;

            hitWindows = HitWindows.GetAllAvailableWindows().ToArray();

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

            FastColourStyle.BindValueChanged(e =>
            {
                if (e.NewValue == ColourStyle.SingleColour)
                {
                    SetFastTextColour(FastColour.Value);
                }
                else if (e.NewValue == ColourStyle.HorizontalGradient)
                {
                    SetFastTextColour(FastColour.Value, FastColourGradient.Value);
                }
                else if (e.NewValue == ColourStyle.VerticalGradient)
                {
                    SetFastTextColour(FastColour.Value, FastColourGradient.Value);
                }
            }, true);

            SlowColourStyle.BindValueChanged(e =>
            {
                if (e.NewValue == ColourStyle.SingleColour)
                {
                    SetSlowTextColour(SlowColour.Value);
                }
                else if (e.NewValue == ColourStyle.HorizontalGradient)
                {
                    SetSlowTextColour(SlowColour.Value, SlowColourGradient.Value);
                }
                else if (e.NewValue == ColourStyle.VerticalGradient)
                {
                    SetSlowTextColour(SlowColour.Value, SlowColourGradient.Value);
                }
            }, true);

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

            switch (random.Next(3))
            {
                case 0:

                    return ColourInfo.SingleColour(randomColour());

                case 1:

                    return ColourInfo.GradientHorizontal(randomColour(), randomColour());

                case 2:

                    return ColourInfo.GradientVertical(randomColour(), randomColour());
            }

            return ColourInfo.SingleColour(Colour4.White);
        }

        private Colour4 randomColour()
        {
            var random = new Random();
            return new Colour4((float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble(), 1);
        }

        protected void SetDisplayStyle(bool value)
        {
            if (value)
            {
                fast.Y = Gap.Value;
                slow.Y = -Gap.Value;
                test.X = Gap.Value;
                fast.X = 0;
                slow.X = 0;
                test.Y = 0;
            }
            else
            {
                fast.Y = 0;
                slow.Y = 0;
                test.X = 0;
                fast.X = Gap.Value;
                slow.X = -Gap.Value;
                test.Y = Gap.Value;
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
            if (DisplayStyle.Value)
            {
                gap.Value = value;
                fast.X = 0;
                slow.X = 0;
                test.Y = 0;
                fast.Y = value;
                slow.Y = -value;
                test.X = value;
            }
            else
            {
                gap.Value = value;
                fast.Y = 0;
                slow.Y = 0;
                test.X = 0;
                fast.X = value;
                slow.X = -value;
                test.Y = value;
            }
        }

        protected void SetFastTextColour(Colour4 colour, Colour4? gradient = null)

        {
            FastColour.Value = colour;

            displayFastText.Colour = colour;

            if (gradient != null && FastColourStyle.Value != ColourStyle.SingleColour)
            {
                FastColourGradient.Value = gradient.Value;

                if (FastColourStyle.Value == ColourStyle.HorizontalGradient)
                {
                    displayFastText.Colour = ColourInfo.GradientHorizontal(colour, gradient.Value);
                }
                else if (FastColourStyle.Value == ColourStyle.VerticalGradient)
                {
                    displayFastText.Colour = ColourInfo.GradientVertical(colour, gradient.Value);
                }
            }
        }

        protected void SetSlowTextColour(Colour4 colour, Colour4? gradient = null)
        {
            SlowColour.Value = colour;
            displaySlowText.Colour = colour;

            if (gradient != null && FastColourStyle.Value != ColourStyle.SingleColour)
            {
                SlowColourGradient.Value = gradient.Value;

                if (SlowColourStyle.Value == ColourStyle.HorizontalGradient)
                {
                    displaySlowText.Colour = ColourInfo.GradientHorizontal(colour, gradient.Value);
                }
                else if (SlowColourStyle.Value == ColourStyle.VerticalGradient)
                {
                    displaySlowText.Colour = ColourInfo.GradientVertical(colour, gradient.Value);
                }
            }
        }

        public override void Clear()
        {
        }

        protected override void OnNewJudgement(JudgementResult judgement)
        {
            if ((!judgement.IsHit || judgement.HitObject.HitWindows?.WindowFor(HitResult.Miss) == 0) && judgement.Type != HitResult.Miss)
            {
                return;
            }

            if (!judgement.Type.IsScorable() || judgement.Type.IsBonus())
            {
                return;
            }

            var originalColumn = (IHasColumn)judgement.HitObject;

            if (checkHitResult(judgement.Type))
            {
                checkColumn(judgement, originalColumn);
            }
            else // Higher than or equal to the selected judge.
            {
            }

            if (Test.Value)
            {
                checkColumn(judgement, originalColumn);
            }
        }

        private void checkColumn(JudgementResult judgement, IHasColumn? originalColumn)
        {
            if (originalColumn is null)
            {
                return;
            }

            try
            {
                int column = originalColumn.Column + 1;
                var legacyRuleset = (ILegacyRuleset)ruleset.Value.CreateInstance();
                int keys = legacyRuleset.GetKeyCount(beatmap.Value.BeatmapInfo, mods.Value);

                if (SelectColumn.Value == Column.Middle && keys / 2.0 != Math.Truncate(keys / 2.0) && column == (keys / 2) + 1)
                {
                    displayResult(judgement);
                }
                else if (SelectColumn.Value == Column.RightHalf && column > keys / 2.0)
                {
                    if (keys % 2 != 0 && column > (keys / 2) + 1)
                    {
                        displayResult(judgement);
                    }
                    else if (keys % 2 == 0)
                    {
                        displayResult(judgement);
                    }
                }
                else if (SelectColumn.Value == Column.LeftHalf && column <= keys / 2.0)
                {
                    displayResult(judgement);
                }
                else if (column >= LowerColumnBound.Value && column <= UpperColumnBound.Value && SelectColumn.Value == Column.None)
                {
                    displayResult(judgement);
                }
            }
            catch (Exception)
            {
                // Ignore
            }
        }

        private void displayResult(JudgementResult judgement)
        {
            if (Test.Value)
            {
                displayFastText.FadeOutFromOne(FadeDuration.Value, Easing.OutQuint);
                displaySlowText.FadeOutFromOne(FadeDuration.Value, Easing.OutQuint);
                testText.FadeOutFromOne(FadeDuration.Value, Easing.OutQuint);

                if (LNSwitch.Value)
                {
                    if (judgement.HitObject is TailNote)
                    {
                        displayFastText.Text = fastTextLNString;
                        displaySlowText.Text = slowTextLNString;
                    }
                    else if (judgement.HitObject is Note)
                    {
                        displayFastText.Text = fastTextString;
                        displaySlowText.Text = slowTextString;
                    }
                }

                return;
            }

            if (judgement.TimeOffset < 0)
            {
                if (LNSwitch.Value)
                {
                    if (judgement.HitObject is HeadNote)
                    {
                        displayFastText.Text = fastTextString;
                    }
                    else if (judgement.HitObject is TailNote)
                    {
                        displayFastText.Text = fastTextLNString;
                    }
                    else if (judgement.HitObject is Note)
                    {
                        displayFastText.Text = fastTextString;
                    }
                }

                displayFastText.FadeOutFromOne(FadeDuration.Value, Easing.OutQuint);

                if (OnlyDisplayOne.Value)
                {
                    displaySlowText.FadeOut(0);
                }
            }

            if (judgement.TimeOffset > 0)
            {
                if (LNSwitch.Value)
                {
                    if (judgement.HitObject is HeadNote)
                    {
                        displaySlowText.Text = slowTextString;
                    }
                    else if (judgement.HitObject is TailNote)
                    {
                        displaySlowText.Text = slowTextLNString;
                    }
                    else if (judgement.HitObject is Note)
                    {
                        displaySlowText.Text = slowTextString;
                    }
                }

                displaySlowText.FadeOutFromOne(FadeDuration.Value, Easing.OutQuint);

                if (OnlyDisplayOne.Value)
                {
                    displayFastText.FadeOut(0);
                }
            }
        }

        private bool checkHitResult(HitResult result)
        {
            int byHit = (int)result - 1;

            if (byHit <= (int)Judgement.Value)
            {
                return true; // true for display judge.
            }

            return false;
        }

        public enum Judgements
        {
            [LocalisableDescription(typeof(FastSlowDisplayStrings), nameof(FastSlowDisplayStrings.Miss))]
            Miss,

            [LocalisableDescription(typeof(FastSlowDisplayStrings), nameof(FastSlowDisplayStrings.Meh))]
            Meh,

            [LocalisableDescription(typeof(FastSlowDisplayStrings), nameof(FastSlowDisplayStrings.Ok))]
            Ok,

            [LocalisableDescription(typeof(FastSlowDisplayStrings), nameof(FastSlowDisplayStrings.Good))]
            Good,

            [LocalisableDescription(typeof(FastSlowDisplayStrings), nameof(FastSlowDisplayStrings.Great))]
            Great,

            [LocalisableDescription(typeof(FastSlowDisplayStrings), nameof(FastSlowDisplayStrings.Perfect))]
            Perfect
        }

        public enum Column
        {
            [LocalisableDescription(typeof(FastSlowDisplayStrings), nameof(FastSlowDisplayStrings.None))]
            None,

            [LocalisableDescription(typeof(FastSlowDisplayStrings), nameof(FastSlowDisplayStrings.LeftHalf))]
            LeftHalf,

            [LocalisableDescription(typeof(FastSlowDisplayStrings), nameof(FastSlowDisplayStrings.RightHalf))]
            RightHalf,

            [LocalisableDescription(typeof(FastSlowDisplayStrings), nameof(FastSlowDisplayStrings.Middle))]
            Middle
        }

        public enum ColourStyle
        {
            [LocalisableDescription(typeof(FastSlowDisplayStrings), nameof(FastSlowDisplayStrings.SingleColour))]
            SingleColour,

            [LocalisableDescription(typeof(FastSlowDisplayStrings), nameof(FastSlowDisplayStrings.HorizontalGradient))]
            HorizontalGradient,

            [LocalisableDescription(typeof(FastSlowDisplayStrings), nameof(FastSlowDisplayStrings.VerticalGradient))]
            VerticalGradient
        }

        [SettingSource(typeof(FastSlowDisplayStrings), nameof(FastSlowDisplayStrings.LNSwitch), nameof(FastSlowDisplayStrings.LNSwitchDescription))]
        public BindableBool LNSwitch { get; } = new BindableBool(false);

        [SettingSource(typeof(FastSlowDisplayStrings), nameof(FastSlowDisplayStrings.FastTextLN), nameof(FastSlowDisplayStrings.TextDescription))]
        public Bindable<string> FastTextLN { get; } = new Bindable<string>("Fast");

        [SettingSource(typeof(FastSlowDisplayStrings), nameof(FastSlowDisplayStrings.SlowTextLN), nameof(FastSlowDisplayStrings.TextDescription))]
        public Bindable<string> SlowTextLN { get; } = new Bindable<string>("Slow");

        [SettingSource(typeof(FastSlowDisplayStrings), nameof(FastSlowDisplayStrings.Test), nameof(FastSlowDisplayStrings.TestDescription))]
        public BindableBool Test { get; } = new BindableBool();

        [SettingSource(typeof(SkinnableComponentStrings), nameof(SkinnableComponentStrings.TextElementText))]
        public Bindable<string> TestText { get; } = new Bindable<string>("Test");
    }
}

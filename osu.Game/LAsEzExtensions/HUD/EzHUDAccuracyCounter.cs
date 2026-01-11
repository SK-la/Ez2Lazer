// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.ComponentModel;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Rulesets.Mods;
using osu.Game.Localisation.HUD;
using osu.Game.Localisation.SkinComponents;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play.HUD.HitErrorMeters;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.LAsEzExtensions.HUD
{
    public partial class EzHUDAccuracyCounter : HitErrorMeter
    {
        // [SettingSource("Wireframe opacity", "Controls the opacity of the wireframes behind the digits.")]
        // public BindableFloat WireframeOpacity { get; } = new BindableFloat(0.25f)
        // {
        //     Precision = 0.01f,
        //     MinValue = 0,
        //     MaxValue = 1,
        // };

        // [SettingSource(typeof(SkinnableComponentStrings), nameof(SkinnableComponentStrings.ShowLabel))]
        // public Bindable<bool> ShowLabel { get; } = new BindableBool(true);

        // [SettingSource("Font", "Font", SettingControlType = typeof(EzSelectorEnumList))]
        // public Bindable<EzEnumGameThemeName> FontNameDropdown { get; } = new Bindable<EzEnumGameThemeName>(EzSelectorEnumList.DEFAULT_NAME);

        [SettingSource(typeof(SkinnableComponentStrings), nameof(SkinnableComponentStrings.Font))]
        public Bindable<Typeface> Font { get; } = new Bindable<Typeface>(Typeface.Torus);

        [SettingSource("Fill Direction", "排列方向")]
        public Bindable<Direction> FlowDirection { get; } = new Bindable<Direction>(Direction.Vertical);

        [SettingSource("Accuracy1 display mode")]
        public Bindable<EzAccuracyDisplayMode> AccuracyDisplay1 { get; } = new Bindable<EzAccuracyDisplayMode>(EzAccuracyDisplayMode.Standard);

        [SettingSource("Accuracy2 display mode")]
        public Bindable<EzAccuracyDisplayMode> AccuracyDisplay2 { get; } = new Bindable<EzAccuracyDisplayMode>(EzAccuracyDisplayMode.Classic);

        [SettingSource("Accuracy3 display mode")]
        public Bindable<EzAccuracyDisplayMode> AccuracyDisplay3 { get; } = new Bindable<EzAccuracyDisplayMode>(EzAccuracyDisplayMode.None);

        [SettingSource("Accuracy4 display mode")]
        public Bindable<EzAccuracyDisplayMode> AccuracyDisplay4 { get; } = new Bindable<EzAccuracyDisplayMode>(EzAccuracyDisplayMode.None);

        [Resolved]
        private IBindable<WorkingBeatmap> beatmap { get; set; } = null!;

        [Resolved]
        private ScoreProcessor scoreProcessor { get; set; } = null!;

        // private ScoreProcessor scoreProcessorV1 => scoreProcessor.Ruleset.CreateScoreProcessor();

        private FillFlowContainer counterFlow = null!;
        private readonly PercentageCounter[] accuracyCounters = new PercentageCounter[4];
        private readonly Bindable<EzAccuracyDisplayMode>[] accuracyDisplays;
        private readonly Bindable<double> v1Accuracy = new Bindable<double>();

        // public EzScoreText[] Text = new EzScoreText[4];
        private readonly OsuSpriteText[] text = new OsuSpriteText[4];

        private long v1MaxScore;
        private long v1TotalScore;
        private bool hasClassicMode;

        private void updateHasClassicMode()
        {
            hasClassicMode = accuracyDisplays.Any(d => d.Value == EzAccuracyDisplayMode.Classic);
        }

        public EzHUDAccuracyCounter()
        {
            AutoSizeAxes = Axes.Both;
            accuracyDisplays = new[] { AccuracyDisplay1, AccuracyDisplay2, AccuracyDisplay3, AccuracyDisplay4 };
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            v1MaxScore = beatmap.Value.Beatmap.HitObjects.Count * 300;
            float od = beatmap.Value.Beatmap.Difficulty.OverallDifficulty;
            CalculateV1Range(od);

            InternalChild = counterFlow = new FillFlowContainer
            {
                Spacing = new Vector2(16),
                AutoSizeAxes = Axes.Both,
                Direction = getFillDirection(FlowDirection.Value),
            };

            for (int i = 0; i < 4; i++)
            {
                // Text[i] = new EzScoreText();
                // Text[i].FontName.BindTo(FontNameDropdown);

                text[i] = new OsuSpriteText();
                // Text[i].Text = $"Acc{i + 1}:";
                text[i].Text = accuracyDisplays[i].Value.GetLocalisableDescription();
                text[i].Anchor = Anchor.TopRight;
                text[i].Origin = Anchor.TopRight;

                accuracyCounters[i] = new PercentageCounter();
                accuracyCounters[i].Anchor = Anchor.TopRight;
                accuracyCounters[i].Origin = Anchor.TopRight;
                accuracyCounters[i].Y = 12; // Offset the counter below the label

                var counterContainer = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Direction = FillDirection.Vertical,
                    Children = new Drawable[]
                    {
                        text[i],
                        accuracyCounters[i]
                    }
                };

                counterFlow.Add(counterContainer);
            }

            FlowDirection.BindValueChanged(_ => counterFlow.Direction = getFillDirection(FlowDirection.Value), true);
            Font.BindValueChanged(font =>
            {
                for (int i = 0; i < 4; i++)
                {
                    text[i].Font = OsuFont.GetFont(font.NewValue);
                }
            }, true);

            // FontNameDropdown.BindValueChanged(font =>
            // {
            //     foreach (var counter in accuracyCounters)
            //         counter.Font = font.NewValue.ToString(); // 假设 PercentageCounter 有 Font 属性
            // }, true);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            for (int i = 0; i < 4; i++)
            {
                int index = i;
                accuracyDisplays[i].BindValueChanged(mode =>
                {
                    accuracyCounters[index].Current.UnbindBindings();
                    accuracyCounters[index].ClearTransforms();

                    switch (mode.NewValue)
                    {
                        case EzAccuracyDisplayMode.Standard:
                            accuracyCounters[index].Current.UnbindBindings();
                            accuracyCounters[index].Current.BindTo(scoreProcessor.Accuracy);
                            accuracyCounters[index].Show();
                            text[index].Text = mode.NewValue.GetLocalisableDescription();
                            text[index].Show();
                            break;

                        case EzAccuracyDisplayMode.MinimumAchievable:
                            accuracyCounters[index].Current.UnbindBindings();
                            accuracyCounters[index].Current.BindTo(scoreProcessor.MinimumAccuracy);
                            accuracyCounters[index].Show();
                            text[index].Text = mode.NewValue.GetLocalisableDescription();
                            text[index].Show();
                            break;

                        case EzAccuracyDisplayMode.MaximumAchievable:
                            accuracyCounters[index].Current.UnbindBindings();
                            accuracyCounters[index].Current.BindTo(scoreProcessor.MaximumAccuracy);
                            accuracyCounters[index].Show();
                            text[index].Text = mode.NewValue.GetLocalisableDescription();
                            text[index].Show();
                            break;

                        case EzAccuracyDisplayMode.Classic:
                            accuracyCounters[index].Current.UnbindBindings();
                            accuracyCounters[index].Current.BindTo(v1Accuracy);
                            accuracyCounters[index].Show();
                            text[index].Text = mode.NewValue.GetLocalisableDescription();
                            text[index].Show();
                            break;

                        case EzAccuracyDisplayMode.None:
                            accuracyCounters[index].Current.UnbindBindings();
                            accuracyCounters[index].Hide();
                            text[index].Hide();
                            break;
                    }

                    updateHasClassicMode();
                }, true);
            }

            updateHasClassicMode();
        }

        protected override void OnNewJudgement(JudgementResult judgement)
        {
            if (!hasClassicMode)
                return;

            double offset = Math.Abs(judgement.TimeOffset);

            HitResult result;
            if (offset <= PerfectRange)
                result = HitResult.Perfect;
            else if (offset <= GreatRange)
                result = HitResult.Great;
            else if (offset <= GoodRange)
                result = HitResult.Good;
            else if (offset <= OkRange)
                result = HitResult.Ok;
            else if (offset <= MehRange)
                result = HitResult.Meh;
            else if (offset <= MissRange)
                result = HitResult.Miss;
            else
                return;

            v1TotalScore += scoreProcessor.GetBaseScoreForResult(result);

            double accuracy = v1MaxScore > 0 ? v1TotalScore / (double)v1MaxScore : 0;
            v1Accuracy.Value = accuracy;
        }

        public override void Clear()
        {
            v1TotalScore = 0;
            v1Accuracy.Value = 0;

            for (int i = 0; i < 4; i++)
            {
                if (accuracyDisplays[i].Value == EzAccuracyDisplayMode.Classic)
                {
                    accuracyCounters[i].Current.Value = 0;
                }

                // text[i].Text = string.Empty;
            }
        }

        public void CalculateV1Range(double od)
        {
            double invertedOd = 10 - od;
            const double total_multiplier = 1.0;

            PerfectRange = Math.Floor(16 * total_multiplier) + 0.5;
            GreatRange = Math.Floor((34 + 3 * invertedOd)) * total_multiplier + 0.5;
            GoodRange = Math.Floor((67 + 3 * invertedOd)) * total_multiplier + 0.5;
            OkRange = Math.Floor((97 + 3 * invertedOd)) * total_multiplier + 0.5;
            MehRange = Math.Floor((121 + 3 * invertedOd)) * total_multiplier + 0.5;
            MissRange = Math.Floor((158 + 3 * invertedOd)) * total_multiplier + 0.5;
        }

        public double PerfectRange = 16 + 0.5;
        public double GreatRange = 34 + 0.5;
        public double GoodRange = 67 + 0.5;
        public double OkRange = 97 + 0.5;
        public double MehRange = 121 + 0.5;
        public double MissRange = 158 + 0.5;

        private FillDirection getFillDirection(Direction flow)
        {
            switch (flow)
            {
                case Direction.Horizontal:
                    return FillDirection.Horizontal;

                default:
                    return FillDirection.Vertical;
            }
        }

        public enum EzAccuracyDisplayMode
        {
            [LocalisableDescription(typeof(GameplayAccuracyCounterStrings), nameof(GameplayAccuracyCounterStrings.AccuracyDisplayModeStandard))]
            Standard,

            [LocalisableDescription(typeof(GameplayAccuracyCounterStrings), nameof(GameplayAccuracyCounterStrings.AccuracyDisplayModeMax))]
            MaximumAchievable,

            [LocalisableDescription(typeof(GameplayAccuracyCounterStrings), nameof(GameplayAccuracyCounterStrings.AccuracyDisplayModeMin))]
            MinimumAchievable,

            [Description("Classic")]
            Classic,

            [Description("None")]
            None
        }
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.ComponentModel;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation.HUD;
using osu.Game.Localisation.SkinComponents;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play.HUD.HitErrorMeters;
using osuTK;

namespace osu.Game.EzOsuGame.HUD
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

        // 是否提供本地自定义图片支持待定。这种需求量很小。
        // [SettingSource("Font", "Font", SettingControlType = typeof(EzSelectorEnumList))]
        // public Bindable<EzEnumGameThemeName> ThemeName { get; } = new Bindable<EzEnumGameThemeName>(EzSelectorEnumList.DEFAULT_NAME);

        [SettingSource(typeof(SkinnableComponentStrings), nameof(SkinnableComponentStrings.Font))]
        public Bindable<Typeface> FontType { get; } = new Bindable<Typeface>(Typeface.Torus);

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.FILL_DIRECTION_LABEL), nameof(EzHUDStrings.FILL_DIRECTION_DESCRIPTION))]
        public Bindable<Direction> FlowDirection { get; } = new Bindable<Direction>(Direction.Vertical);

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.ACCURACY_DISPLAY_MODE_LABEL), nameof(EzHUDStrings.ACCURACY_DISPLAY_MODE_DESCRIPTION))]
        public Bindable<EzAccuracyDisplayMode> AccuracyDisplay1 { get; } = new Bindable<EzAccuracyDisplayMode>(EzAccuracyDisplayMode.Standard);

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.ACCURACY_DISPLAY_MODE_LABEL), nameof(EzHUDStrings.ACCURACY_DISPLAY_MODE_DESCRIPTION))]
        public Bindable<EzAccuracyDisplayMode> AccuracyDisplay2 { get; } = new Bindable<EzAccuracyDisplayMode>(EzAccuracyDisplayMode.Classic);

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.ACCURACY_DISPLAY_MODE_LABEL), nameof(EzHUDStrings.ACCURACY_DISPLAY_MODE_DESCRIPTION))]
        public Bindable<EzAccuracyDisplayMode> AccuracyDisplay3 { get; } = new Bindable<EzAccuracyDisplayMode>(EzAccuracyDisplayMode.None);

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.ACCURACY_DISPLAY_MODE_LABEL), nameof(EzHUDStrings.ACCURACY_DISPLAY_MODE_DESCRIPTION))]
        public Bindable<EzAccuracyDisplayMode> AccuracyDisplay4 { get; } = new Bindable<EzAccuracyDisplayMode>(EzAccuracyDisplayMode.None);

        // [Resolved]
        // private IBindable<WorkingBeatmap> beatmap { get; set; } = null!;

        [Resolved]
        private ScoreProcessor scoreProcessor { get; set; } = null!;

        // private ScoreProcessor scoreProcessorV1 => scoreProcessor.Ruleset.CreateScoreProcessor();

        private FillFlowContainer counterFlow = null!;
        private readonly PercentageCounter[] accuracyCounters = new PercentageCounter[4];
        private readonly Bindable<EzAccuracyDisplayMode>[] accuracyDisplays;
        private readonly OsuSpriteText[] text = new OsuSpriteText[4];
        private readonly Action<ValueChangedEvent<bool>>?[] stableAvailabilityHandlers = new Action<ValueChangedEvent<bool>>?[4];
        private readonly Action<ValueChangedEvent<double>>?[] stableValueHandlers = new Action<ValueChangedEvent<double>>?[4];

        /// <summary>规则集自管的 stable 结果源；未实现的规则集为 null，Stable 槽位隐藏。</summary>
        private IStableScoreSource? stableSource;

        // public EzScoreText[] Text = new EzScoreText[4];

        public EzHUDAccuracyCounter()
        {
            AutoSizeAxes = Axes.Both;
            accuracyDisplays = new[] { AccuracyDisplay1, AccuracyDisplay2, AccuracyDisplay3, AccuracyDisplay4 };
        }

        [BackgroundDependencyLoader]
        private void load()
        {
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
            FontType.BindValueChanged(font =>
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

            stableSource = scoreProcessor as IStableScoreSource;

            for (int i = 0; i < 4; i++)
            {
                int index = i;
                accuracyDisplays[i].BindValueChanged(mode =>
                {
                    unbindStableAvailability(index);
                    unbindStableValue(index);
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
                            // 可用性随局内判定档变化，必须订阅而不是加载时一次性判断：
                            // ApplyEzGameplayEnvironment 与 HUD 的加载时序不应成为隐含依赖。
                            // TODO(Ez live stable 影子)：非 stable 档暂时隐藏，等 Session 可推进引擎。
                            bindStableSlot(index);
                            break;

                        case EzAccuracyDisplayMode.None:
                            accuracyCounters[index].Current.UnbindBindings();
                            accuracyCounters[index].Hide();
                            text[index].Hide();
                            break;
                    }
                }, true);
            }
        }

        private void bindStableSlot(int index)
        {
            if (stableSource == null)
            {
                applyStableSlotAvailability(index, false);
                return;
            }

            Action<ValueChangedEvent<bool>> handler = _ => applyStableSlotAvailability(index, stableSource.StableAvailable.Value);
            stableAvailabilityHandlers[index] = handler;
            stableSource.StableAvailable.ValueChanged += handler;

            applyStableSlotAvailability(index, stableSource.StableAvailable.Value);
        }

        private void unbindStableAvailability(int index)
        {
            if (stableAvailabilityHandlers[index] is not { } handler)
                return;

            stableSource?.StableAvailable.ValueChanged -= handler;
            stableAvailabilityHandlers[index] = null;
        }

        private void applyStableSlotAvailability(int index, bool available)
        {
            unbindStableValue(index);

            if (!available)
            {
                accuracyCounters[index].Current.UnbindBindings();
                accuracyCounters[index].Hide();
                text[index].Hide();
                return;
            }

            // 结果源按接口暴露 IBindable，而 RollingCounter.Current 只接受具体 Bindable，
            // 因此这里用显式订阅而不是 BindTo，并在切换槽位时解绑。
            Action<ValueChangedEvent<double>> handler = v => accuracyCounters[index].Current.Value = v.NewValue;
            stableValueHandlers[index] = handler;
            stableSource!.StableAccuracy.BindValueChanged(handler, true);

            accuracyCounters[index].Show();
            text[index].Text = EzAccuracyDisplayMode.Classic.GetLocalisableDescription();
            text[index].Show();
        }

        private void unbindStableValue(int index)
        {
            if (stableValueHandlers[index] is not { } handler)
                return;

            stableSource?.StableAccuracy.ValueChanged -= handler;
            stableValueHandlers[index] = null;
        }

        protected override void OnNewJudgement(JudgementResult judgement)
        {
        }

        public override void Clear()
        {
        }

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
    }

    public enum EzAccuracyDisplayMode
    {
        [LocalisableDescription(typeof(GameplayAccuracyCounterStrings), nameof(GameplayAccuracyCounterStrings.AccuracyDisplayModeStandard))]
        Standard,

        [LocalisableDescription(typeof(GameplayAccuracyCounterStrings), nameof(GameplayAccuracyCounterStrings.AccuracyDisplayModeMax))]
        MaximumAchievable,

        [LocalisableDescription(typeof(GameplayAccuracyCounterStrings), nameof(GameplayAccuracyCounterStrings.AccuracyDisplayModeMin))]
        MinimumAchievable,

        [Description("Stable")]
        Classic,

        [Description("None")]
        None
    }
}

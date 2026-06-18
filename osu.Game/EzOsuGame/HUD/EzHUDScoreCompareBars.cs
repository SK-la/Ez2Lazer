// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.ComponentModel;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Acrylic;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation.SkinComponents;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Scoring;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;
using Container = osu.Framework.Graphics.Containers.Container;

namespace osu.Game.EzOsuGame.HUD
{
    public enum EzScoreCompareBarDirection
    {
        [Description("Upward")]
        Upward,

        [Description("Downward")]
        Downward,
    }

    /// <summary>
    /// BMS 风格三柱分数对比：当前局 + 两条按对比条件选出的参考柱。
    /// 柱高与标签数值始终按 TotalScore 绘制；柱图总高度对应整谱理论满分。
    /// Ghost 查询与时间线经 <see cref="EzScoreRaceSession"/> 统一提供。
    /// </summary>
    public partial class EzHUDScoreCompareBars : EzHUDScoreRaceComponent, ISerialisableDrawable, IAcrylicBackdropConsumer
    {
        private const float backdrop_blur_strength = 16;

        public bool UsesFixedAnchor { get; set; }

        protected override bool ContributesMaxEntryCount => false;

        public bool WantsAcrylicCapture => BackgroundVisible.Value && BackdropBlurEnabled.Value;

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.SCORE_COMPARE_CONDITION1_LABEL), nameof(EzHUDStrings.SCORE_COMPARE_CONDITION1_DESCRIPTION))]
        public Bindable<EzScoreRaceMetric> CompareCondition1Setting { get; } = new Bindable<EzScoreRaceMetric>(EzScoreRaceMetric.Accuracy);

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.SCORE_COMPARE_CONDITION2_LABEL), nameof(EzHUDStrings.SCORE_COMPARE_CONDITION2_DESCRIPTION))]
        public Bindable<EzScoreRaceMetric> CompareCondition2Setting { get; } = new Bindable<EzScoreRaceMetric>(EzScoreRaceMetric.TotalScore);

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.SCORE_RACE_BAR_DIRECTION_LABEL), nameof(EzHUDStrings.SCORE_RACE_BAR_DIRECTION_DESCRIPTION))]
        public Bindable<EzScoreCompareBarDirection> BarDirection { get; } = new Bindable<EzScoreCompareBarDirection>(EzScoreCompareBarDirection.Upward);

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.SCORE_COMPARE_BAR_HEIGHT_LABEL), nameof(EzHUDStrings.SCORE_COMPARE_BAR_HEIGHT_DESCRIPTION))]
        public BindableNumber<float> BarHeight { get; } = new BindableNumber<float>(120)
        {
            MinValue = 40,
            MaxValue = 400,
            Precision = 1,
        };

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.SCORE_COMPARE_BAR_WIDTH_LABEL), nameof(EzHUDStrings.SCORE_COMPARE_BAR_WIDTH_DESCRIPTION))]
        public BindableNumber<float> BarWidth { get; } = new BindableNumber<float>(38)
        {
            MinValue = 10,
            MaxValue = 80,
            Precision = 1,
        };

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.SCORE_COMPARE_BACKGROUND_VISIBLE_LABEL), nameof(EzHUDStrings.SCORE_COMPARE_BACKGROUND_VISIBLE_DESCRIPTION))]
        public BindableBool BackgroundVisible { get; } = new BindableBool(true);

        [SettingSource(typeof(SkinnableComponentStrings), nameof(SkinnableComponentStrings.CornerRadius), nameof(SkinnableComponentStrings.CornerRadiusDescription),
            SettingControlType = typeof(SettingsPercentageSlider<float>))]
        public new BindableFloat CornerRadius { get; } = new BindableFloat(0.12f)
        {
            MinValue = 0,
            MaxValue = 0.5f,
            Precision = 0.01f,
        };

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.SCORE_COMPARE_BACKDROP_BLUR_LABEL), nameof(EzHUDStrings.SCORE_COMPARE_BACKDROP_BLUR_DESCRIPTION))]
        public BindableBool BackdropBlurEnabled { get; } = new BindableBool(false);

        private const float bar_spacing = 4;
        private const float label_area_height = 36;
        private const float container_padding = 6;

        private readonly AcrylicBackdropDrawable acrylicBackdrop;
        private readonly Box backgroundTint;
        private readonly Container backgroundLayer;
        private Container barsContainer = null!;
        private readonly CompareBar[] bars = new CompareBar[3];
        private EzAcrylicCaptureController? captureController;

        private EzScoreRaceEntry? pickedEntryForCondition1;
        private EzScoreRaceEntry? pickedEntryForCondition2;
        private EzScoreRaceTimelineScoreProcessor? ghostProcessor1;
        private EzScoreRaceTimelineScoreProcessor? ghostProcessor2;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private IAcrylicCaptureRegistrar? acrylicCaptureRegistrar { get; set; }

        [Resolved]
        private IRenderer renderer { get; set; } = null!;

        public EzHUDScoreCompareBars()
        {
            Width = 3 * BarWidth.Value + 2 * bar_spacing + container_padding * 2;
            Height = BarHeight.Value + label_area_height + container_padding * 2;
            Masking = true;

            InternalChild = backgroundLayer = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    acrylicBackdrop = new AcrylicBackdropDrawable
                    {
                        RelativeSizeAxes = Axes.Both,
                        EffectEnabled = false,
                        FrameBufferScale = Vector2.One,
                    },
                    backgroundTint = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.White.Opacity(0.12f),
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            captureController = new EzAcrylicCaptureController(acrylicCaptureRegistrar, renderer, acrylicBackdrop);

            barsContainer = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Padding = new MarginPadding(container_padding),
            };

            for (int i = 0; i < bars.Length; i++)
            {
                bars[i] = new CompareBar();
                barsContainer.Add(bars[i]);
            }

            AddInternal(barsContainer);

            ghostProcessor1 = new EzScoreRaceTimelineScoreProcessor();
            ghostProcessor2 = new EzScoreRaceTimelineScoreProcessor();
            AddInternal(ghostProcessor1);
            AddInternal(ghostProcessor2);
            ghostProcessor1.TotalScore.BindValueChanged(_ => updateGhostCompareBar(1));
            ghostProcessor2.TotalScore.BindValueChanged(_ => updateGhostCompareBar(2));

            EnsureLoadingOverlay();

            base.LoadComplete();

            BackgroundVisible.BindValueChanged(_ => updateBackgroundVisibility(), true);
            BackdropBlurEnabled.BindValueChanged(_ => SyncAcrylicCaptureState(), true);

            CompareCondition1Setting.BindValueChanged(_ => onCompareConditionChanged(), true);
            CompareCondition2Setting.BindValueChanged(_ => onCompareConditionChanged(), true);
            BarDirection.BindValueChanged(_ => layoutBars(), true);
            BarHeight.BindValueChanged(_ => layoutBars(), true);
            BarWidth.BindValueChanged(_ => layoutBars(), true);

            SyncAcrylicCaptureState();
            layoutBars();
        }

        public void SyncAcrylicCaptureState()
            => captureController?.Sync(WantsAcrylicCapture, backdrop_blur_strength);

        protected override void Update()
        {
            base.Update();

            updateCurrentAndTheoreticalBars();
            base.CornerRadius = CornerRadius.Value * Math.Min(DrawWidth, DrawHeight);
        }

        protected override void OnSessionReady()
        {
            Session?.IsReady.BindValueChanged(_ => onCompareConditionChanged(), true);
        }

        protected override void OnEntriesChangedScheduled()
        {
            refreshPickedEntries();
            updateGhostCompareBar(1);
            updateGhostCompareBar(2);
        }

        private void updateCurrentAndTheoreticalBars()
        {
            long barScoreScale = getBarScoreScale();
            long nowBarScore = GetLiveDisplayScore();

            bars[0].UpdateValues(EzHUDStrings.SCORE_COMPARE_NOW_LABEL, formatScore(nowBarScore), nowBarScore, barScoreScale, getBarColour(isCurrent: true));

            if (CompareCondition1Setting.Value == EzScoreRaceMetric.TheoreticalMaxScore)
                updateTheoreticalCompareBar(bars[1], CompareCondition1Setting.Value, barScoreScale);

            if (CompareCondition2Setting.Value == EzScoreRaceMetric.TheoreticalMaxScore)
                updateTheoreticalCompareBar(bars[2], CompareCondition2Setting.Value, barScoreScale);
        }

        private void updateGhostCompareBar(int conditionIndex)
        {
            var metric = conditionIndex == 1 ? CompareCondition1Setting.Value : CompareCondition2Setting.Value;

            if (metric == EzScoreRaceMetric.TheoreticalMaxScore)
                return;

            var pickedEntry = conditionIndex == 1 ? pickedEntryForCondition1 : pickedEntryForCondition2;
            var processor = conditionIndex == 1 ? ghostProcessor1 : ghostProcessor2;
            long barScoreScale = getBarScoreScale();

            if (Session == null || !Session.IsReady.Value || processor == null)
            {
                bars[conditionIndex].UpdateValues(metric.GetLocalisableDescription(), string.Empty, 0, barScoreScale, getBarColour(metric));
                return;
            }

            long barScore = processor.TotalScore.Value;
            bars[conditionIndex].UpdateValues(
                metric.GetLocalisableDescription(),
                formatBarValue(barScore, pickedEntry),
                barScore,
                barScoreScale,
                getBarColour(metric));
        }

        private void updateTheoreticalCompareBar(CompareBar bar, EzScoreRaceMetric metric, long barScoreScale)
        {
            long theoreticalScore = getTheoreticalScoreAtTime();
            bar.UpdateValues(
                metric.GetLocalisableDescription(),
                formatScore(theoreticalScore),
                theoreticalScore,
                barScoreScale,
                getBarColour(metric));
        }

        private void onCompareConditionChanged()
        {
            refreshPickedEntries();
            bars[1].ResetVisualCache();
            bars[2].ResetVisualCache();
            updateGhostCompareBar(1);
            updateGhostCompareBar(2);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
                captureController?.Dispose();

            base.Dispose(isDisposing);
        }

        private void updateBackgroundVisibility()
        {
            backgroundLayer.Alpha = BackgroundVisible.Value ? 1 : 0;
            SyncAcrylicCaptureState();
        }

        /// <summary>
        /// 与角逐榜面板色系一致：当前=绿、最高分数=黄、最低 Miss=红、其余=蓝。
        /// </summary>
        private Color4 getBarColour(EzScoreRaceMetric metric = default, bool isCurrent = false)
        {
            const float alpha = 0.85f;

            if (isCurrent)
                return colours.Lime2.Opacity(alpha);

            return metric switch
            {
                EzScoreRaceMetric.TotalScore => colours.YellowLight.Opacity(alpha),
                EzScoreRaceMetric.MissCount => colours.Red2.Opacity(alpha),
                _ => colours.Blue4.Opacity(alpha),
            };
        }

        private void refreshPickedEntries()
        {
            if (!SupportsGhostRace || Session == null || !Session.IsReady.Value)
            {
                pickedEntryForCondition1 = null;
                pickedEntryForCondition2 = null;
                ghostProcessor1?.SetTimeline(null);
                ghostProcessor2?.SetTimeline(null);
                return;
            }

            pickedEntryForCondition1 = CompareCondition1Setting.Value == EzScoreRaceMetric.TheoreticalMaxScore
                ? null
                : Session.PickGhost(CompareCondition1Setting.Value);
            pickedEntryForCondition2 = CompareCondition2Setting.Value == EzScoreRaceMetric.TheoreticalMaxScore
                ? null
                : Session.PickGhost(CompareCondition2Setting.Value);

            ghostProcessor1?.SetTimeline(pickedEntryForCondition1?.Timeline);
            ghostProcessor2?.SetTimeline(pickedEntryForCondition2?.Timeline);
        }

        private static string formatScore(long score) => score.ToString("N0");

        private static string formatBarValue(long barScore, EzScoreRaceEntry? pickedEntry)
        {
            if (pickedEntry == null || barScore <= 0)
                return string.Empty;

            return formatScore(barScore);
        }

        /// <summary>
        /// 当前时刻「已判定区间全 Perfect」应达到的分数（≥ 当前实际分，与 live SP 同源）。
        /// </summary>
        private long getTheoreticalScoreAtTime()
        {
            if (ScoreProcessor == null)
                return 0;

            return ScoreProcessor.GetTheoreticalPerfectJudgedDisplayScore();
        }

        /// <summary>
        /// 柱图满刻度 = 整谱理论满分（<see cref="ScoreProcessor.MaximumTotalScore"/>）。
        /// </summary>
        private long getBarScoreScale()
        {
            if (ScoreProcessor != null && ScoreProcessor.MaximumTotalScore > 0)
                return ScoreProcessor.MaximumTotalScore;

            return (long)ScoreProcessor.MAX_SCORE;
        }

        private void layoutBars()
        {
            float barThickness = BarWidth.Value;
            float barAxisHeight = BarHeight.Value;
            var direction = BarDirection.Value;

            Width = bars.Length * barThickness + (bars.Length - 1) * bar_spacing + container_padding * 2;
            Height = barAxisHeight + label_area_height + container_padding * 2;

            for (int i = 0; i < bars.Length; i++)
            {
                var bar = bars[i];
                bar.Configure(direction, barAxisHeight, barThickness);

                bar.Anchor = Anchor.BottomLeft;
                bar.Origin = Anchor.BottomLeft;
                bar.Width = barThickness;
                bar.Height = barAxisHeight + label_area_height;
                bar.X = i * (barThickness + bar_spacing);
            }
        }

        private partial class CompareBar : CompositeDrawable
        {
            private const float min_bar_height = 3;

            private Box bar = null!;
            private OsuSpriteText titleText = null!;
            private OsuSpriteText valueText = null!;
            private Container barTrack = null!;
            private EzScoreCompareBarDirection direction = EzScoreCompareBarDirection.Upward;
            private float maxBarSize;
            private float barThickness;

            private LocalisableString lastTitle;
            private string lastValue = string.Empty;
            private float lastBarHeight = -1;
            private Color4 lastBarColour;

            public CompareBar()
            {
                RelativeSizeAxes = Axes.None;
            }

            public void Configure(EzScoreCompareBarDirection barDirection, float maxSize, float thickness)
            {
                direction = barDirection;
                maxBarSize = maxSize;
                barThickness = thickness;

                barTrack.Size = new Vector2(barThickness, maxSize);
                bar.Width = barThickness;
                applyBarAnchor();
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                InternalChild = new FillFlowContainer
                {
                    Direction = FillDirection.Vertical,
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Spacing = new Vector2(0, 2),
                    Children = new Drawable[]
                    {
                        titleText = new OsuSpriteText
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Font = OsuFont.GetFont(size: 11, weight: FontWeight.Bold),
                        },
                        barTrack = new Container
                        {
                            RelativeSizeAxes = Axes.None,
                            Size = new Vector2(barThickness, maxBarSize),
                            Child = bar = new Box
                            {
                                RelativeSizeAxes = Axes.None,
                                Width = barThickness,
                            },
                        },
                        valueText = new OsuSpriteText
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Font = OsuFont.GetFont(size: 11),
                        },
                    },
                };

                applyBarAnchor();
            }

            private void applyBarAnchor()
            {
                if (direction == EzScoreCompareBarDirection.Upward)
                {
                    bar.Anchor = Anchor.BottomCentre;
                    bar.Origin = Anchor.BottomCentre;
                }
                else
                {
                    bar.Anchor = Anchor.TopCentre;
                    bar.Origin = Anchor.TopCentre;
                }
            }

            public void ResetVisualCache()
            {
                lastBarHeight = -1;
            }

            public void UpdateValues(LocalisableString title, string value, long barScore, long maxBarScore, Color4 barColour)
            {
                Alpha = 1;

                if (!lastTitle.Equals(title))
                {
                    titleText.Text = title;
                    lastTitle = title;
                }

                if (lastValue != value)
                {
                    valueText.Text = value;
                    lastValue = value;
                }

                if (lastBarColour != barColour)
                {
                    bar.Colour = barColour;
                    lastBarColour = barColour;
                }

                float ratio = maxBarScore <= 0 ? 0 : (float)barScore / maxBarScore;
                ratio = Math.Clamp(ratio, 0, 1);

                float barHeight = maxBarSize * ratio;

                if (barScore > 0 && barHeight < min_bar_height)
                    barHeight = min_bar_height;

                if (Math.Abs(lastBarHeight - barHeight) < 0.01f)
                    return;

                lastBarHeight = barHeight;
                bar.RelativeSizeAxes = Axes.None;
                bar.Width = barThickness;
                bar.Height = barHeight;
            }
        }
    }
}

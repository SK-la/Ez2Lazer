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
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
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
    public partial class EzHUDScoreCompareBars : EzHUDScoreRaceComponent, ISerialisableDrawable
    {
        public bool UsesFixedAnchor { get; set; }

        protected override bool ContributesMaxEntryCount => false;

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.SCORE_RACE_MOD_FILTER_LABEL), nameof(EzHUDStrings.SCORE_RACE_MOD_FILTER_DESCRIPTION))]
        public Bindable<EzScoreModFilter> ModFilterSetting { get; } = new Bindable<EzScoreModFilter>(EzScoreModFilter.Any);

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.SCORE_COMPARE_CONDITION1_LABEL), nameof(EzHUDStrings.SCORE_COMPARE_CONDITION1_DESCRIPTION))]
        public Bindable<EzScoreRaceMetric> CompareCondition1 { get; } = new Bindable<EzScoreRaceMetric>(EzScoreRaceMetric.Accuracy);

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.SCORE_COMPARE_CONDITION2_LABEL), nameof(EzHUDStrings.SCORE_COMPARE_CONDITION2_DESCRIPTION))]
        public Bindable<EzScoreRaceMetric> CompareCondition2 { get; } = new Bindable<EzScoreRaceMetric>(EzScoreRaceMetric.TheoreticalMaxScore);

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

        private const float bar_spacing = 4;
        private const float label_area_height = 36;
        private const float container_padding = 6;

        private Container barsContainer = null!;
        private readonly CompareBar[] bars = new CompareBar[3];

        private EzScoreRaceEntry? pickedEntryForCondition1;
        private EzScoreRaceEntry? pickedEntryForCondition2;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        public EzHUDScoreCompareBars()
        {
            Width = 3 * BarWidth.Value + 2 * bar_spacing + container_padding * 2;
            Height = BarHeight.Value + label_area_height + container_padding * 2;
        }

        protected override void ConfigureSession()
        {
            // 仅同步 Mod 过滤；条目数由角逐榜组件负责。
            Session?.ReloadIfNeeded(ModFilter.Value, Session.MaxEntryCount);
        }

        protected override void LoadComplete()
        {
            ModFilter.BindTo(ModFilterSetting);

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
            EnsureLoadingOverlay();

            base.LoadComplete();

            CompareCondition1.BindValueChanged(_ => refreshPickedEntries(), true);
            CompareCondition2.BindValueChanged(_ => refreshPickedEntries(), true);
            BarDirection.BindValueChanged(_ => layoutBars(), true);
            BarHeight.BindValueChanged(_ => layoutBars(), true);
            BarWidth.BindValueChanged(_ => layoutBars(), true);

            layoutBars();
        }

        protected override void OnSessionReady()
        {
            Session?.IsReady.BindValueChanged(_ => refreshPickedEntries(), true);
        }

        protected override void OnEntriesChangedScheduled()
        {
            refreshPickedEntries();
        }

        protected override void UpdateDisplay()
        {
            if (Session == null || !Session.IsReady.Value)
                return;

            double clockTime = GetCurrentClockTime();
            long barScoreScale = getBarScoreScale();

            long nowBarScore = GetLiveDisplayScore();
            long condition1BarScore = getBarScoreForMetric(CompareCondition1.Value, pickedEntryForCondition1, clockTime);
            long condition2BarScore = getBarScoreForMetric(CompareCondition2.Value, pickedEntryForCondition2, clockTime);

            bars[0].UpdateValues(EzHUDStrings.SCORE_COMPARE_NOW_LABEL, formatScore(nowBarScore), nowBarScore, barScoreScale, getBarColour(isCurrent: true));
            bars[1].UpdateValues(
                CompareCondition1.Value.GetLocalisableDescription(),
                formatScoreOrDash(condition1BarScore, pickedEntryForCondition1, CompareCondition1.Value),
                condition1BarScore,
                barScoreScale,
                getBarColour(CompareCondition1.Value));
            bars[2].UpdateValues(
                CompareCondition2.Value.GetLocalisableDescription(),
                formatScoreOrDash(condition2BarScore, pickedEntryForCondition2, CompareCondition2.Value),
                condition2BarScore,
                barScoreScale,
                getBarColour(CompareCondition2.Value));
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
            if (Session == null || !Session.IsReady.Value)
            {
                pickedEntryForCondition1 = null;
                pickedEntryForCondition2 = null;
                return;
            }

            pickedEntryForCondition1 = Session.PickGhost(CompareCondition1.Value);
            pickedEntryForCondition2 = Session.PickGhost(CompareCondition2.Value, pickedEntryForCondition1?.ScoreInfo);
        }

        private long getBarScoreForMetric(EzScoreRaceMetric metric, EzScoreRaceEntry? pickedEntry, double clockTime)
        {
            if (metric == EzScoreRaceMetric.TheoreticalMaxScore)
                return getTheoreticalScoreAtTime();

            return EzScoreRaceSession.QueryTimelineScore(pickedEntry, clockTime);
        }

        /// <summary>
        /// 当前时刻「已判定区间全 Perfect」应达到的分数（≥ 当前实际分，与 live SP 同源）。
        /// </summary>
        private long getTheoreticalScoreAtTime()
        {
            if (ScoreProcessor == null)
                return 0;

            double accuracy = ScoreProcessor.Accuracy.Value;

            if (accuracy <= double.Epsilon)
                return 0;

            long currentScore = GetLiveDisplayScore();

            if (currentScore <= 0)
                return 0;

            // MinimumAccuracy/Accuracy = 当前已出现 ex 上限 / 整谱 ex（Perfect 时等于 MinimumAccuracy）。
            double perfectProgressRatio = ScoreProcessor.MinimumAccuracy.Value / accuracy;
            return (long)Math.Round(currentScore * perfectProgressRatio);
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

        private static string formatScore(long score) => score.ToString("N0");

        private static string formatScoreOrDash(long score, EzScoreRaceEntry? pickedEntry, EzScoreRaceMetric metric)
        {
            if (metric == EzScoreRaceMetric.TheoreticalMaxScore || pickedEntry == null)
                return formatScore(score);

            return score > 0 ? formatScore(score) : "-";
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
            private Box trackBackground = null!;
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

                trackBackground.Size = new Vector2(barThickness, maxSize);

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
                            Size = new Vector2(barThickness, maxBarSize),
                            Children = new Drawable[]
                            {
                                trackBackground = new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Colour = Color4.White.Opacity(0.12f),
                                },
                                bar = new Box
                                {
                                    RelativeSizeAxes = Axes.None,
                                    Width = barThickness,
                                },
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

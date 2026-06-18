// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.ComponentModel;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Scoring;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

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
    /// BMS 风格三柱分数对比：当前局 + 两条按条件选出的历史最佳成绩。
    /// 柱高始终按 TotalScore 绘制；上下文字分别为指标名与指标值。
    /// </summary>
    public partial class EzHUDScoreCompareBars : EzHUDScoreRaceComponent, ISerialisableDrawable
    {
        public bool UsesFixedAnchor { get; set; }

        protected override bool ContributesMaxEntryCount => true;

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.SCORE_RACE_MOD_FILTER_LABEL), nameof(EzHUDStrings.SCORE_RACE_MOD_FILTER_DESCRIPTION))]
        public Bindable<EzScoreModFilter> ModFilterSetting { get; } = new Bindable<EzScoreModFilter>(EzScoreModFilter.Any);

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.SCORE_RACE_MAX_ENTRIES_LABEL), nameof(EzHUDStrings.SCORE_RACE_MAX_ENTRIES_DESCRIPTION))]
        public BindableNumber<int> MaxEntriesSetting { get; } = new BindableNumber<int>(10)
        {
            MinValue = 1,
            MaxValue = 10,
        };

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.SCORE_RACE_SLOT1_CRITERION_LABEL), nameof(EzHUDStrings.SCORE_RACE_SLOT1_CRITERION_DESCRIPTION))]
        public Bindable<EzScorePickCriterion> Slot1Criterion { get; } = new Bindable<EzScorePickCriterion>(EzScorePickCriterion.Accuracy);

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.SCORE_RACE_SLOT2_CRITERION_LABEL), nameof(EzHUDStrings.SCORE_RACE_SLOT2_CRITERION_DESCRIPTION))]
        public Bindable<EzScorePickCriterion> Slot2Criterion { get; } = new Bindable<EzScorePickCriterion>(EzScorePickCriterion.TotalScore);

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.SCORE_RACE_BAR_DIRECTION_LABEL), nameof(EzHUDStrings.SCORE_RACE_BAR_DIRECTION_DESCRIPTION))]
        public Bindable<EzScoreCompareBarDirection> BarDirection { get; } = new Bindable<EzScoreCompareBarDirection>(EzScoreCompareBarDirection.Upward);

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.SCORE_RACE_MAX_BAR_HEIGHT_LABEL), nameof(EzHUDStrings.SCORE_RACE_MAX_BAR_HEIGHT_DESCRIPTION))]
        public BindableNumber<float> MaxBarHeight { get; } = new BindableNumber<float>(120)
        {
            MinValue = 40,
            MaxValue = 400,
            Precision = 1,
        };

        private const float bar_spacing = 4;
        private const float label_area_height = 36;

        private osu.Framework.Graphics.Containers.Container barsContainer = null!;
        private readonly CompareBar[] bars = new CompareBar[3];

        public EzHUDScoreCompareBars()
        {
            Width = 340;
            Height = 210;
        }

        protected override void LoadComplete()
        {
            ModFilter.BindTo(ModFilterSetting);
            MaxEntries.BindTo(MaxEntriesSetting);

            barsContainer = new osu.Framework.Graphics.Containers.Container
            {
                RelativeSizeAxes = Axes.Both,
                Padding = new MarginPadding(6),
            };

            for (int i = 0; i < bars.Length; i++)
            {
                bars[i] = new CompareBar();
                barsContainer.Add(bars[i]);
            }

            AddInternal(barsContainer);
            EnsureLoadingOverlay();

            base.LoadComplete();

            Slot1Criterion.BindValueChanged(_ => refreshHistoricalBars(), true);
            Slot2Criterion.BindValueChanged(_ => refreshHistoricalBars(), true);
            BarDirection.BindValueChanged(_ => layoutBars(), true);
            MaxBarHeight.BindValueChanged(_ => layoutBars(), true);

            layoutBars();
        }

        protected override void OnSessionReady()
        {
            Session?.IsReady.BindValueChanged(_ => refreshHistoricalBars(), true);
        }

        protected override void OnEntriesChangedScheduled()
        {
            refreshHistoricalBars();
        }

        protected override void UpdateDisplay()
        {
            if (Session == null || !Session.IsReady.Value)
                return;

            double clockTime = GetCurrentClockTime();

            long nowBarScore = GetLiveDisplayScore();
            var slot1Info = getSlotScoreInfo(1);
            var slot2Info = getSlotScoreInfo(2);
            long slot1BarScore = getTotalScoreAtTime(slot1Info, clockTime);
            long slot2BarScore = getTotalScoreAtTime(slot2Info, clockTime);

            long maxBarScore = Math.Max(1, nowBarScore);
            maxBarScore = Math.Max(maxBarScore, slot1BarScore);
            maxBarScore = Math.Max(maxBarScore, slot2BarScore);

            bars[0].UpdateValues("Now", formatMetricValue(EzScorePickCriterion.TotalScore, nowBarScore, null), nowBarScore, maxBarScore);
            bars[1].UpdateValues(
                formatCriterion(Slot1Criterion.Value),
                formatMetricValue(Slot1Criterion.Value, 0, slot1Info, clockTime),
                slot1BarScore,
                maxBarScore);
            bars[2].UpdateValues(
                formatCriterion(Slot2Criterion.Value),
                formatMetricValue(Slot2Criterion.Value, 0, slot2Info, clockTime),
                slot2BarScore,
                maxBarScore);
        }

        private void refreshHistoricalBars()
        {
            if (Session == null || !Session.IsReady.Value)
                return;

            UpdateDisplay();
        }

        private ScoreInfo? getSlotScoreInfo(int slot)
        {
            if (Session == null)
                return null;

            var candidates = Session.Entries.Where(e => !e.Tracked).Select(e => e.ScoreInfo).ToList();

            return slot switch
            {
                1 => EzLocalScoreQueries.PickBest(candidates, Slot1Criterion.Value),
                2 => EzLocalScoreQueries.PickBest(candidates, Slot2Criterion.Value, exclude: EzLocalScoreQueries.PickBest(candidates, Slot1Criterion.Value)),
                _ => null,
            };
        }

        private long getTotalScoreAtTime(ScoreInfo? scoreInfo, double clockTime)
        {
            if (scoreInfo == null || Session == null)
                return 0;

            var entry = Session.Entries.FirstOrDefault(e => !e.Tracked && e.ScoreInfo.ID == scoreInfo.ID);

            if (entry?.Timeline != null)
                return entry.Timeline.QueryAtTime(clockTime).TotalScore;

            return scoreInfo.TotalScore;
        }

        private long getMetricAtTime(ScoreInfo? scoreInfo, EzScorePickCriterion criterion, double clockTime)
        {
            if (scoreInfo == null || Session == null)
                return 0;

            var entry = Session.Entries.FirstOrDefault(e => !e.Tracked && e.ScoreInfo.ID == scoreInfo.ID);

            if (entry?.Timeline != null)
                return getSnapshotMetric(entry.Timeline.QueryAtTime(clockTime), criterion, scoreInfo);

            return getStaticMetric(scoreInfo, criterion);
        }

        private string formatMetricValue(EzScorePickCriterion criterion, long liveValue, ScoreInfo? scoreInfo, double clockTime = 0)
        {
            if (criterion == EzScorePickCriterion.TotalScore && scoreInfo == null)
                return liveValue.ToString("N0");

            if (scoreInfo == null)
                return "-";

            long metric = getMetricAtTime(scoreInfo, criterion, clockTime);

            return criterion switch
            {
                EzScorePickCriterion.TotalScore => metric.ToString("N0"),
                EzScorePickCriterion.Accuracy => (metric / 1_000_000.0).ToString("P2"),
                EzScorePickCriterion.MaxCombo => metric.ToString("N0"),
                EzScorePickCriterion.MissCount => metric.ToString("N0"),
                _ => metric.ToString("N0"),
            };
        }

        private static long getSnapshotMetric(EzScoreTimelineSnapshot snapshot, EzScorePickCriterion criterion, ScoreInfo scoreInfo)
        {
            return criterion switch
            {
                EzScorePickCriterion.TotalScore => snapshot.TotalScore,
                EzScorePickCriterion.Accuracy => (long)Math.Round(snapshot.Accuracy * 1_000_000),
                EzScorePickCriterion.MaxCombo => snapshot.HighestCombo,
                EzScorePickCriterion.MissCount => snapshot.MissCount,
                _ => 0,
            };
        }

        private static long getStaticMetric(ScoreInfo scoreInfo, EzScorePickCriterion criterion)
        {
            return criterion switch
            {
                EzScorePickCriterion.TotalScore => scoreInfo.TotalScore,
                EzScorePickCriterion.Accuracy => (long)Math.Round(scoreInfo.Accuracy * 1_000_000),
                EzScorePickCriterion.MaxCombo => scoreInfo.MaxCombo,
                EzScorePickCriterion.MissCount => EzLocalScoreQueries.GetMissCount(scoreInfo),
                _ => 0,
            };
        }

        private void layoutBars()
        {
            float barThickness = Math.Max(88, (barsContainer.DrawWidth - bar_spacing * 2) / 3);
            var direction = BarDirection.Value;

            for (int i = 0; i < bars.Length; i++)
            {
                var bar = bars[i];
                bar.Configure(direction, MaxBarHeight.Value);

                bar.Anchor = Anchor.BottomLeft;
                bar.Origin = Anchor.BottomLeft;
                bar.Width = barThickness;
                bar.Height = MaxBarHeight.Value + label_area_height;
                bar.X = i * (barThickness + bar_spacing);
            }
        }

        private static string formatCriterion(EzScorePickCriterion criterion) => criterion switch
        {
            EzScorePickCriterion.TotalScore => "Score",
            EzScorePickCriterion.Accuracy => "Acc",
            EzScorePickCriterion.MaxCombo => "Combo",
            EzScorePickCriterion.MissCount => "Miss",
            _ => criterion.ToString(),
        };

        private partial class CompareBar : CompositeDrawable
        {
            private Box bar = null!;
            private OsuSpriteText titleText = null!;
            private OsuSpriteText valueText = null!;
            private osu.Framework.Graphics.Containers.Container barTrack = null!;
            private EzScoreCompareBarDirection direction = EzScoreCompareBarDirection.Upward;
            private float maxBarSize;

            public CompareBar()
            {
                RelativeSizeAxes = Axes.None;
            }

            public void Configure(EzScoreCompareBarDirection barDirection, float maxSize)
            {
                direction = barDirection;
                maxBarSize = maxSize;

                if (barTrack == null)
                    return;

                barTrack.Height = maxSize;
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
                        barTrack = new osu.Framework.Graphics.Containers.Container
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = maxBarSize,
                            Child = bar = new Box
                            {
                                Colour = new Color4(0.4f, 0.75f, 1f, 0.85f),
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
                if (bar == null)
                    return;

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

            public void UpdateValues(string title, string value, long barScore, long maxBarScore)
            {
                Alpha = 1;
                titleText.Text = title;
                valueText.Text = value;

                float ratio = maxBarScore <= 0 ? 0 : (float)barScore / maxBarScore;
                ratio = Math.Clamp(ratio, 0, 1);

                bar.RelativeSizeAxes = Axes.X;
                bar.Height = maxBarSize * ratio;
            }
        }
    }
}

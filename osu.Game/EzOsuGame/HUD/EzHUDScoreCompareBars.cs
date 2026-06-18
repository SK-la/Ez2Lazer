// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
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
using osu.Game.Scoring;
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
    /// 柱高与标签数值始终按 TotalScore 绘制；对比条件仅用于筛选对比哪条成绩。
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
        public BindableNumber<float> BarWidth { get; } = new BindableNumber<float>(88)
        {
            MinValue = 40,
            MaxValue = 200,
            Precision = 1,
        };

        private const float bar_spacing = 4;
        private const float label_area_height = 36;
        private const float container_padding = 6;

        private Container barsContainer = null!;
        private readonly CompareBar[] bars = new CompareBar[3];

        public EzHUDScoreCompareBars()
        {
            Width = 3 * BarWidth.Value + 2 * bar_spacing + container_padding * 2;
            Height = BarHeight.Value + label_area_height + container_padding * 2;
        }

        protected override void ConfigureSession()
        {
            if (Session == null)
                return;

            // 仅同步 Mod 过滤；条目数由角逐榜组件负责。
            Session.ReloadIfNeeded(ModFilter.Value, Session.MaxEntryCount);
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

            CompareCondition1.BindValueChanged(_ => refreshHistoricalBars(), true);
            CompareCondition2.BindValueChanged(_ => refreshHistoricalBars(), true);
            BarDirection.BindValueChanged(_ => layoutBars(), true);
            BarHeight.BindValueChanged(_ => layoutBars(), true);
            BarWidth.BindValueChanged(_ => layoutBars(), true);

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
            long condition1BarScore = getBarScoreAtTime(CompareCondition1.Value, clockTime, exclude: null);
            long condition2BarScore = getBarScoreAtTime(CompareCondition2.Value, clockTime, getExcludeForCondition2());

            long maxBarScore = Math.Max(1, nowBarScore);
            maxBarScore = Math.Max(maxBarScore, condition1BarScore);
            maxBarScore = Math.Max(maxBarScore, condition2BarScore);

            bars[0].UpdateValues(EzHUDStrings.SCORE_COMPARE_NOW_LABEL, formatScore(nowBarScore), nowBarScore, maxBarScore);
            bars[1].UpdateValues(
                CompareCondition1.Value.GetLocalisableDescription(),
                formatScore(condition1BarScore),
                condition1BarScore,
                maxBarScore);
            bars[2].UpdateValues(
                CompareCondition2.Value.GetLocalisableDescription(),
                formatScore(condition2BarScore),
                condition2BarScore,
                maxBarScore);
        }

        private void refreshHistoricalBars()
        {
            if (Session == null || !Session.IsReady.Value)
                return;

            UpdateDisplay();
        }

        private ScoreInfo? getExcludeForCondition2()
        {
            if (CompareCondition1.Value == EzScoreRaceMetric.TheoreticalMaxScore)
                return null;

            return EzLocalScoreQueries.PickBest(getCompareCandidates(), CompareCondition1.Value);
        }

        private long getBarScoreAtTime(EzScoreRaceMetric metric, double clockTime, ScoreInfo? exclude)
        {
            if (metric == EzScoreRaceMetric.TheoreticalMaxScore)
                return getTheoreticalScoreAtTime();

            var scoreInfo = EzLocalScoreQueries.PickBest(getCompareCandidates(), metric, exclude);
            return getTotalScoreAtTime(scoreInfo, clockTime);
        }

        private List<ScoreInfo> getCompareCandidates()
        {
            if (GameplayState == null)
                return new List<ScoreInfo>();

            var localScores = EzLocalScoreQueries.GetLocalScoresWithReplay(Realm, GameplayState.Beatmap.BeatmapInfo, GameplayState.Ruleset.RulesetInfo);
            return EzLocalScoreQueries.FilterByMods(localScores, GameplayState.Mods.ToArray(), ModFilter.Value).ToList();
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

        private long getTheoreticalScoreAtTime()
        {
            if (ScoreProcessor == null)
                return 0;

            return (long)Math.Round(ScoreProcessor.MinimumAccuracy.Value * ScoreProcessor.MAX_SCORE);
        }

        private static string formatScore(long score) => score.ToString("N0");

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
                bar.Configure(direction, barAxisHeight);

                bar.Anchor = Anchor.BottomLeft;
                bar.Origin = Anchor.BottomLeft;
                bar.Width = barThickness;
                bar.Height = barAxisHeight + label_area_height;
                bar.X = i * (barThickness + bar_spacing);
            }
        }

        private partial class CompareBar : CompositeDrawable
        {
            private Box bar = null!;
            private OsuSpriteText titleText = null!;
            private OsuSpriteText valueText = null!;
            private Container barTrack = null!;
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
                        barTrack = new Container
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

            public void UpdateValues(LocalisableString title, string value, long barScore, long maxBarScore)
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

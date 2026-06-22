// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Caching;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Logging;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Graphics.Containers;
using osu.Game.Screens.Play.HUD;
using osu.Game.Screens.Play.Leaderboards;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.HUD
{
    /// <summary>
    /// 本地多成绩实时角逐排行榜。对齐官方 Leaderboard 架构：
    /// - <see cref="EzScoreRaceService"/> 负责选歌界面预加载，提供 <see cref="IEzScoreRaceStateLookup.States"/> 字典
    /// - 本组件订阅字典变化，按需创建/销毁 processor，每个 processor 绑定到一个 ghost state
    /// - HUD 直接绑定 processor 的 bindable，不需要 Session/Entry 中间层
    /// </summary>
    public partial class EzHUDScoreRaceLeaderboard : EzHUDScoreRaceComponent, ISerialisableDrawable
    {
        public bool UsesFixedAnchor { get; set; }

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.SCORE_RACE_MOD_FILTER_LABEL), nameof(EzHUDStrings.SCORE_RACE_MOD_FILTER_DESCRIPTION))]
        public Bindable<EzScoreModFilter> ModFilterSetting { get; } = new Bindable<EzScoreModFilter>(EzScoreModFilter.Any);

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.SCORE_RACE_MAX_ENTRIES_LABEL), nameof(EzHUDStrings.SCORE_RACE_MAX_ENTRIES_DESCRIPTION))]
        public BindableNumber<int> MaxEntriesSetting { get; } = new BindableNumber<int>(5)
        {
            MinValue = 1,
            MaxValue = 10,
        };

        [SettingSource(typeof(EzHUDStrings), nameof(EzHUDStrings.SCORE_RACE_SORT_CRITERION_LABEL), nameof(EzHUDStrings.SCORE_RACE_SORT_CRITERION_DESCRIPTION))]
        public Bindable<EzScoreRaceMetric> SortCriterionSetting { get; } = new Bindable<EzScoreRaceMetric>(EzScoreRaceMetric.TotalScore);

        protected readonly FillFlowContainer<DrawableGameplayLeaderboardScore> Flow;

        private bool requiresScroll;
        private readonly InputDisabledScrollContainer scroll;
        private DrawableGameplayLeaderboardScore? trackedScore;
        private readonly BindableBool expanded = new BindableBool(true);
        private readonly List<LeaderboardEntryState> entryStates = new List<LeaderboardEntryState>();
        private readonly Cached sorting = new Cached();

        private IBindableDictionary<string, EzScoreRaceState>? stateLookup;

        public EzHUDScoreRaceLeaderboard()
        {
            float xOffset = DrawableGameplayLeaderboardScore.SHEAR_WIDTH + DrawableGameplayLeaderboardScore.ELASTIC_WIDTH_LENIENCE;

            Width = 260 + xOffset;
            Height = 300;

            InternalChildren = new Drawable[]
            {
                scroll = new InputDisabledScrollContainer
                {
                    ClampExtension = 0,
                    RelativeSizeAxes = Axes.Both,
                    Child = Flow = new FillFlowContainer<DrawableGameplayLeaderboardScore>
                    {
                        RelativeSizeAxes = Axes.X,
                        X = xOffset,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(2.5f),
                        LayoutDuration = 450,
                        LayoutEasing = Easing.OutQuint,
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            SortCriterionSetting.BindValueChanged(_ =>
            {
                sorting.Invalidate();
                sort();
            });

            EnsureLoadingOverlay();

            base.LoadComplete();

            Scheduler.AddDelayed(sort, 1000, true);
        }

        private void bindStateLookupWhenAvailable()
        {
            if (stateLookup != null)
                return;

            var service = (EzScoreRaceService?)Dependencies.Get(typeof(EzScoreRaceService));

            if (service == null)
            {
                Schedule(bindStateLookupWhenAvailable);
                return;
            }

            stateLookup = service.States;
            stateLookup!.BindCollectionChanged(onStatesChanged, true);

            updateLoadingState();
        }

        private void onStatesChanged(object? sender, NotifyDictionaryChangedEventArgs<string, EzScoreRaceState> e)
        {
            Schedule(() =>
            {
                switch (e.Action)
                {
                    case NotifyDictionaryChangedAction.Add:
                    case NotifyDictionaryChangedAction.Remove:
                        rebuildRowsIfNeeded();
                        break;
                }
            });
        }

        private void updateLoadingState()
        {
            if (LoadingText == null)
                return;

            LoadingText.Alpha = SupportsGhostRace && stateLookup!.Count == 0 ? 1 : 0;
        }

        protected override void OnSessionReady()
        {
            bindStateLookupWhenAvailable();
        }

        protected override void Update()
        {
            base.Update();
            updateScoreDisplay();
        }

        private void updateScoreDisplay()
        {
            Width = Math.Max(Width, Flow.X + DrawableGameplayLeaderboardScore.MIN_WIDTH);
            Height = Math.Max(Height, DrawableGameplayLeaderboardScore.PANEL_HEIGHT);

            requiresScroll = Flow.DrawHeight > Height;

            if (requiresScroll && trackedScore != null)
            {
                double scrollTarget = scroll.GetChildPosInContent(trackedScore) + trackedScore.DrawHeight / 2 - scroll.DrawHeight / 2;
                scroll.ScrollTo(scrollTarget);
            }

            const float panel_height = DrawableGameplayLeaderboardScore.PANEL_HEIGHT;

            float fadeBottom = (float)(scroll.Current + scroll.DrawHeight);
            float fadeTop = (float)(scroll.Current + panel_height);

            if (scroll.IsScrolledToStart())
                fadeTop -= panel_height;

            if (!scroll.IsScrolledToEnd())
                fadeBottom -= panel_height;

            foreach (var c in Flow)
            {
                float topY = c.ToSpaceOfOtherDrawable(Vector2.Zero, Flow).Y;
                float bottomY = topY + panel_height;

                bool requireTopFade = requiresScroll && topY <= fadeTop;
                bool requireBottomFade = requiresScroll && bottomY >= fadeBottom;

                if (!requireTopFade && !requireBottomFade)
                    c.Colour = Color4.White;
                else if (topY > fadeBottom + panel_height || bottomY < fadeTop - panel_height)
                    c.Colour = Color4.Transparent;
                else
                {
                    if (requireBottomFade)
                    {
                        c.Colour = ColourInfo.GradientVertical(
                            Color4.White.Opacity(Math.Min(1 - (topY - fadeBottom) / panel_height, 1)),
                            Color4.White.Opacity(Math.Min(1 - (bottomY - fadeBottom) / panel_height, 1)));
                    }
                    else if (requiresScroll)
                    {
                        c.Colour = ColourInfo.GradientVertical(
                            Color4.White.Opacity(Math.Min(1 - (fadeTop - topY) / panel_height, 1)),
                            Color4.White.Opacity(Math.Min(1 - (fadeTop - bottomY) / panel_height, 1)));
                    }
                }
            }
        }

        private void rebuildRowsIfNeeded()
        {
            if (!needsStructuralRebuild())
            {
                refreshExistingRows();
                return;
            }

            Flow.Clear();
            entryStates.Clear();
            trackedScore = null;
            scroll.ScrollToStart(false);

            int i = 0;

            foreach (var kvp in stateLookup!.OrderByDescending(kvp => kvp.Value.ScoreInfo.TotalScore))
            {
                if (i >= MaxEntriesSetting.Value)
                    break;

                var state = kvp.Value;
                var drawable = createDrawableForState(state, out var entryState);
                entryStates.Add(entryState);
                Flow.Add(drawable);
                Logger.Log($"[EzScoreRace] Leaderboard rebuild: state[{i}] {state.ScoreInfo.ID}, HasTimeline={state.Timeline != null}", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
                i++;
            }

            sorting.Invalidate();
            sort();
            updateLoadingState();
            Logger.Log($"[EzScoreRace] Leaderboard rebuild done: {entryStates.Count} rows", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
        }

        private void refreshExistingRows()
        {
            if (entryStates.Count != stateLookup!.Count)
            {
                rebuildRowsIfNeeded();
                return;
            }

            sorting.Invalidate();
            sort();
        }

        private DrawableGameplayLeaderboardScore createDrawableForState(EzScoreRaceState state, out LeaderboardEntryState entryState)
        {
            var processor = new EzScoreRaceTimelineScoreProcessor();
            AddInternal(processor);

            processor.BindTo(state);

            var leaderboardScore = new GameplayLeaderboardScore(state.ScoreInfo, false, GameplayLeaderboardScore.ComboDisplayMode.Highest);
            var scoreInfo = state.ScoreInfo;
            leaderboardScore.TotalScore.BindTarget = processor.TotalScore;
            leaderboardScore.Accuracy.BindTarget = processor.Accuracy;
            leaderboardScore.Combo.BindTarget = processor.Combo;
            leaderboardScore.GetDisplayScore = mode => EzScoreRaceDisplayScore.ForLeaderboardScore(leaderboardScore, scoreInfo, mode);

            var drawable = new DrawableGameplayLeaderboardScore(leaderboardScore);
            drawable.Expanded.BindTo(expanded);
            drawable.DisplayOrder.BindValueChanged(_ => Scheduler.AddOnce(sort), true);

            processor.TotalScore.BindValueChanged(_ => sorting.Invalidate());

            entryState = new LeaderboardEntryState(state, leaderboardScore, drawable, processor);

            return drawable;
        }

        protected override void OnEntriesChangedScheduled()
        {
            rebuildRowsIfNeeded();
        }

        private bool needsStructuralRebuild()
        {
            if (stateLookup!.Count == 0)
                return entryStates.Count > 0;

            string[] boundIds = stateLookup!.Keys.OrderBy(k => k).ToArray();
            string[] stateIds = entryStates.Select(s => s.ScoreInfoId).OrderBy(id => id).ToArray();

            if (boundIds.Length != stateIds.Length)
                return true;

            for (int i = 0; i < boundIds.Length; i++)
            {
                if (boundIds[i] != stateIds[i])
                    return true;
            }

            return false;
        }

        private void sort()
        {
            if (sorting.IsValid)
                return;

            applySortOrder(getOrderedEntryStates());
            sorting.Validate();
        }

        private List<LeaderboardEntryState> getOrderedEntryStates()
        {
            var ordered = EzScoreRaceMetricOrdering.ApplyMetricOrdering(
                entryStates,
                SortCriterionSetting.Value,
                s => s.LeaderboardScore.TotalScore.Value,
                s => s.LeaderboardScore.Accuracy.Value,
                s => s.LeaderboardScore.Combo.Value,
                getMissCount);

            return ordered.ThenBy(s => s.Tiebreaker).ToList();
        }

        private void applySortOrder(List<LeaderboardEntryState> orderedList)
        {
            for (int i = 0; i < orderedList.Count; i++)
            {
                var state = orderedList[i];
                int rank = i + 1;
                state.LeaderboardScore.DisplayOrder.Value = rank;
                state.LeaderboardScore.Position.Value = rank;
                Flow.SetLayoutPosition(state.Drawable, rank);
            }
        }

        private int getMissCount(LeaderboardEntryState state)
        {
            return state.Processor?.MissCount.Value ?? 0;
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                foreach (var entry in entryStates)
                    entry.Processor?.Dispose();

                entryStates.Clear();
            }

            base.Dispose(isDisposing);
        }

        private sealed class LeaderboardEntryState
        {
            public string ScoreInfoId { get; }
            public long Tiebreaker { get; }
            public EzScoreRaceTimelineScoreProcessor? Processor { get; }
            public GameplayLeaderboardScore LeaderboardScore { get; }
            public DrawableGameplayLeaderboardScore Drawable { get; }

            public LeaderboardEntryState(EzScoreRaceState state,
                                         GameplayLeaderboardScore leaderboardScore,
                                         DrawableGameplayLeaderboardScore drawable,
                                         EzScoreRaceTimelineScoreProcessor processor)
            {
                ScoreInfoId = state.ScoreInfo.ID.ToString();
                Tiebreaker = state.ScoreInfo.Date.ToUnixTimeSeconds();
                Processor = processor;
                LeaderboardScore = leaderboardScore;
                Drawable = drawable;
            }
        }

        private partial class InputDisabledScrollContainer : OsuScrollContainer
        {
            public InputDisabledScrollContainer()
            {
                ScrollbarVisible = false;
            }

            public override bool HandlePositionalInput => false;
            public override bool HandleNonPositionalInput => false;
        }
    }
}

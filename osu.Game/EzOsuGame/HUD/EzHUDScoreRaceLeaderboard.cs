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
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Graphics.Containers;
using osu.Game.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Screens.Play.HUD;
using osu.Game.Screens.Play.Leaderboards;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.HUD
{
    /// <summary>
    /// 本地多成绩实时角逐排行榜。对齐官方 Leaderboard 架构：
    /// - <see cref="EzScoreRaceService"/> 提供 ghost 元数据（选歌）与进局 timeline build
    /// - 本组件订阅字典变化，按需创建/销毁 processor，每个 processor 绑定到一个 ghost state
    /// - 服务未注册时：当前玩家实时绑定 ScoreProcessor，ghost 展示 ScoreInfo 静态终值
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
        private bool interestRegistered;
        private bool passiveMode;
        private bool passiveSettingsBound;
        private bool sortTimerScheduled;

        private LeaderboardEntryState? currentPlayerEntry;
        private double lastUpdateScoreDisplayScroll = double.MinValue;
        private double lastScrollTarget = double.MinValue;
        private double lastProcessorUpdateTime;
        private bool rebuildScheduled;

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
                    Alpha = 0,
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
        }

        private void bindStateLookupWhenAvailable()
        {
            if (IsScoreRaceServiceAvailable)
                activateLive();
            else
                activatePassive();
        }

        private void activateLive()
        {
            passiveMode = false;
            var service = ScoreRaceService!;

            ModFilterSetting.BindTo(service.ModFilter);
            MaxEntriesSetting.BindTo(service.MaxEntries);

            if (!interestRegistered)
            {
                service.RegisterInterest();
                interestRegistered = true;
            }

            if (stateLookup == null)
            {
                stateLookup = service.States;
                stateLookup.BindCollectionChanged(onStatesChanged, true);
            }

            scroll.Alpha = 1;

            if (!sortTimerScheduled)
            {
                sortTimerScheduled = true;
                Scheduler.AddDelayed(sort, 1000, true);
            }

            updateLoadingState();
            rebuildRowsIfNeeded();
        }

        private void activatePassive()
        {
            passiveMode = true;

            if (interestRegistered && ScoreRaceService != null)
            {
                ScoreRaceService.UnregisterInterest();
                interestRegistered = false;
            }

            stateLookup = null;

            bindPassiveSettingsIfNeeded();

            scroll.Alpha = 1;

            LoadingText?.Alpha = 0;

            if (!sortTimerScheduled)
            {
                sortTimerScheduled = true;
                Scheduler.AddDelayed(sort, 1000, true);
            }

            rebuildPassiveRows();
        }

        private void bindPassiveSettingsIfNeeded()
        {
            if (passiveSettingsBound)
                return;

            passiveSettingsBound = true;

            ModFilterSetting.BindValueChanged(_ => rebuildPassiveRows());
            MaxEntriesSetting.BindValueChanged(_ => rebuildPassiveRows());
        }

        private void onStatesChanged(object? sender, NotifyDictionaryChangedEventArgs<string, EzScoreRaceState> e)
        {
            // 使用 AddOnce + 标志位合并同一帧内的多次字典变化事件，
            // 避免 publishStates 的 Clear + N 次 Add 触发 N 次 rebuildRowsIfNeeded。
            if (!rebuildScheduled)
            {
                rebuildScheduled = true;
                Scheduler.AddOnce(scheduleRebuild);
            }
        }

        private void scheduleRebuild()
        {
            rebuildScheduled = false;
            rebuildRowsIfNeeded();
        }

        private void updateLoadingState()
        {
            if (LoadingText == null || passiveMode)
                return;

            LoadingText.Alpha = SupportsGhostRace && shouldShowLoading() ? 1 : 0;
        }

        private bool shouldShowLoading()
        {
            if (!SupportsGhostRace || stateLookup == null || stateLookup.Count == 0)
                return false;

            bool anyPendingTimeline = false;

            foreach (var state in stateLookup)
            {
                if (state.Value.Timeline == null)
                {
                    anyPendingTimeline = true;
                    break;
                }
            }

            if (!anyPendingTimeline)
                return false;

            return ScoreRaceService?.IsTimelineBuildInProgress == true;
        }

        protected override void OnSessionReady()
        {
            bindStateLookupWhenAvailable();
        }

        protected override void OnGameplayClockResolved(GameplayClockContainer clock)
        {
            base.OnGameplayClockResolved(clock);

            foreach (var entry in entryStates)
            {
                entry.Processor?.ReferenceClock = clock;
            }
        }

        protected override void Update()
        {
            base.Update();

            if (scroll.Alpha <= 0)
                return;

            if (!passiveMode)
            {
                // 对齐官方 MultiSpectatorLeaderboardProvider：每帧驱动 processor 的 UpdateScore。
                if (Time.Current - lastProcessorUpdateTime >= 50)
                {
                    foreach (var entry in entryStates)
                        entry.Processor?.UpdateScore();

                    lastProcessorUpdateTime = Time.Current;
                }

                if (LoadingText?.Alpha > 0)
                    updateLoadingState();
            }

            updateScoreDisplay();
        }

        private void updateScoreDisplay()
        {
            Width = Math.Max(Width, Flow.X + DrawableGameplayLeaderboardScore.MIN_WIDTH);
            Height = Math.Max(Height, DrawableGameplayLeaderboardScore.PANEL_HEIGHT);

            requiresScroll = Flow.DrawHeight > Height;

            // 缓存滚动位置，仅在滚动位置变化时重新计算 fade 区域。
            // 避免每帧对每个子元素调用昂贵的坐标空间转换。
            double currentScroll = scroll.Current;

            if (requiresScroll && trackedScore != null)
            {
                double scrollTarget = scroll.GetChildPosInContent(trackedScore) + trackedScore.DrawHeight / 2 - scroll.DrawHeight / 2;

                if (Math.Abs(scrollTarget - lastScrollTarget) > 0.5f)
                {
                    scroll.ScrollTo(scrollTarget);
                    lastScrollTarget = scrollTarget;
                }
            }

            if (Math.Abs(currentScroll - lastUpdateScoreDisplayScroll) < 0.5f)
                return;

            lastUpdateScoreDisplayScroll = currentScroll;

            const float panel_height = DrawableGameplayLeaderboardScore.PANEL_HEIGHT;

            float fadeBottom = (float)(scroll.Current + scroll.DrawHeight);
            float fadeTop = (float)(scroll.Current + panel_height);

            if (scroll.IsScrolledToStart())
                fadeTop -= panel_height;

            if (!scroll.IsScrolledToEnd())
                fadeBottom -= panel_height;

            foreach (var c in Flow)
            {
                // 使用 Flow 子元素的 Position（布局坐标）代替 ToSpaceOfOtherDrawable（昂贵的坐标空间转换）。
                // FillFlowContainer 内子元素与 Flow 共享父坐标系，Position.Y 即为正确的布局位置。
                float topY = c.Position.Y;
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

        private void rebuildPassiveRows()
        {
            foreach (var entry in entryStates)
            {
                if (entry.Processor != null)
                    RemoveInternal(entry.Processor, true);
            }

            Flow.Clear();
            entryStates.Clear();
            currentPlayerEntry = null;
            trackedScore = null;
            scroll.ScrollToStart(false);

            createCurrentPlayerEntry();

            var ghostScores = QueryStaticGhostScores(ModFilterSetting.Value, MaxEntriesSetting.Value);

            foreach (var scoreInfo in ghostScores)
            {
                var drawable = createDrawableForScoreInfo(scoreInfo, out var entryState);
                entryStates.Add(entryState);
                Flow.Add(drawable);
            }

            sorting.Invalidate();
            sort();
        }

        private void rebuildRowsIfNeeded()
        {
            if (passiveMode)
            {
                rebuildPassiveRows();
                return;
            }

            if (!needsStructuralRebuild())
            {
                ensureCurrentPlayerEntry();
                refreshExistingRows();
                return;
            }

            foreach (var entry in entryStates)
            {
                if (entry.Processor != null)
                    RemoveInternal(entry.Processor, true);
            }

            Flow.Clear();
            entryStates.Clear();
            currentPlayerEntry = null;
            trackedScore = null;
            scroll.ScrollToStart(false);

            // 添加当前玩家条目（实时绑定 ScoreProcessor）
            createCurrentPlayerEntry();

            int i = 0;

            foreach (var kvp in stateLookup!.OrderByDescending(kvp => kvp.Value.ScoreInfo.TotalScore))
            {
                if (i >= MaxEntriesSetting.Value)
                    break;

                var state = kvp.Value;
                var drawable = createDrawableForState(state, out var entryState);
                entryStates.Add(entryState);
                Flow.Add(drawable);
                i++;
            }

            sorting.Invalidate();
            sort();
            updateLoadingState();
        }

        private void createCurrentPlayerEntry()
        {
            if (GameplayState == null || ScoreProcessor == null)
                return;

            var playerScore = new GameplayLeaderboardScore(GameplayState, true, GameplayLeaderboardScore.ComboDisplayMode.Current);
            var drawable = new DrawableGameplayLeaderboardScore(playerScore);
            drawable.Expanded.BindTo(expanded);
            drawable.DisplayOrder.BindValueChanged(_ => Scheduler.AddOnce(sort), true);

            playerScore.TotalScore.BindValueChanged(_ => sorting.Invalidate());

            var entry = new LeaderboardEntryState(playerScore, drawable);
            currentPlayerEntry = entry;
            trackedScore = drawable;
            entryStates.Add(entry);
            Flow.Add(drawable);
        }

        private bool shouldHaveCurrentPlayerEntry() => GameplayState != null && ScoreProcessor != null;

        private void ensureCurrentPlayerEntry()
        {
            if (currentPlayerEntry != null || !shouldHaveCurrentPlayerEntry())
                return;

            createCurrentPlayerEntry();
            sorting.Invalidate();
            sort();
            updateLoadingState();
        }

        private void refreshExistingRows()
        {
            int ghostCount = entryStates.Count - (currentPlayerEntry != null ? 1 : 0);

            if (ghostCount != stateLookup!.Count)
            {
                rebuildRowsIfNeeded();
                return;
            }

            sorting.Invalidate();
            sort();
        }

        private DrawableGameplayLeaderboardScore createDrawableForScoreInfo(ScoreInfo scoreInfo, out LeaderboardEntryState entryState)
        {
            var leaderboardScore = new GameplayLeaderboardScore(scoreInfo, false, GameplayLeaderboardScore.ComboDisplayMode.Highest);
            var drawable = new DrawableGameplayLeaderboardScore(leaderboardScore);
            drawable.Expanded.BindTo(expanded);
            drawable.DisplayOrder.BindValueChanged(_ => Scheduler.AddOnce(sort), true);

            entryState = new LeaderboardEntryState(scoreInfo, leaderboardScore, drawable);

            return drawable;
        }

        private DrawableGameplayLeaderboardScore createDrawableForState(EzScoreRaceState state, out LeaderboardEntryState entryState)
        {
            var processor = new EzScoreRaceTimelineScoreProcessor();
            if (GameplayClockContainer != null)
                processor.ReferenceClock = GameplayClockContainer;
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
            // currentPlayerEntry 不参与 ID 比较（它不是 ghost 条目）。
            // 只比较 ghost 条目数量：entryStates 含 player + ghosts，stateLookup 仅含 ghosts。
            int ghostCount = entryStates.Count - (currentPlayerEntry != null ? 1 : 0);

            if (shouldHaveCurrentPlayerEntry() && currentPlayerEntry == null)
                return true;

            if (stateLookup!.Count == 0)
                return ghostCount > 0;

            if (ghostCount != stateLookup.Count)
                return true;

            foreach (var kvp in stateLookup)
            {
                if (entryStates.All(e => e == currentPlayerEntry || e.ScoreInfoId != kvp.Key))
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

        private int getMissCount(LeaderboardEntryState state) => state.MissCount;

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                if (interestRegistered && ScoreRaceService != null)
                {
                    ScoreRaceService.UnregisterInterest();
                    interestRegistered = false;
                }

                foreach (var entry in entryStates)
                    entry.Processor?.Dispose();

                entryStates.Clear();
                currentPlayerEntry = null;
            }

            base.Dispose(isDisposing);
        }

        private sealed class LeaderboardEntryState
        {
            public string ScoreInfoId { get; }
            public long Tiebreaker { get; }
            public int MissCount { get; }
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
                MissCount = EzLocalScoreQueries.GetMissCount(state.ScoreInfo);
                Processor = processor;
                LeaderboardScore = leaderboardScore;
                Drawable = drawable;
            }

            public LeaderboardEntryState(ScoreInfo scoreInfo,
                                         GameplayLeaderboardScore leaderboardScore,
                                         DrawableGameplayLeaderboardScore drawable)
            {
                ScoreInfoId = scoreInfo.ID.ToString();
                Tiebreaker = scoreInfo.Date.ToUnixTimeSeconds();
                MissCount = EzLocalScoreQueries.GetMissCount(scoreInfo);
                LeaderboardScore = leaderboardScore;
                Drawable = drawable;
            }

            /// <summary>
            /// 当前玩家条目（无 ghost processor，直接绑定 ScoreProcessor）。
            /// </summary>
            public LeaderboardEntryState(GameplayLeaderboardScore leaderboardScore,
                                         DrawableGameplayLeaderboardScore drawable)
            {
                ScoreInfoId = "__current_player__";
                Tiebreaker = long.MaxValue;
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

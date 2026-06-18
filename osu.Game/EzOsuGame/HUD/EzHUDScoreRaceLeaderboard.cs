// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Game.Configuration;
using osu.Game.EzOsuGame.Localization;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Graphics.Containers;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play.HUD;
using osu.Game.Screens.Play.Leaderboards;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.HUD
{
    /// <summary>
    /// 本地多成绩实时角逐排行榜。外观与 <see cref="DrawableGameplayLeaderboard"/> 一致。
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
        private readonly List<RaceEntryState> entryStates = new List<RaceEntryState>();

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
            ModFilter.BindTo(ModFilterSetting);
            MaxEntries.BindTo(MaxEntriesSetting);
            SortCriterionSetting.BindValueChanged(_ => sortAndApply());

            EnsureLoadingOverlay();

            base.LoadComplete();
        }

        protected override void OnSessionReady()
        {
            Session?.IsReady.BindValueChanged(_ => rebuildRows(), true);
        }

        protected override void UpdateDisplay()
        {
            if (Session == null || !Session.IsReady.Value)
                return;

            double clockTime = GetCurrentClockTime();

            foreach (var state in entryStates)
            {
                if (state.Tracked)
                    continue;

                var snapshot = EzScoreRaceSession.QuerySnapshot(state.Timeline, clockTime);
                state.LeaderboardScore.TotalScore.Value = snapshot.TotalScore;
                state.LeaderboardScore.Accuracy.Value = snapshot.Accuracy;
                state.LeaderboardScore.Combo.Value = snapshot.HighestCombo;
                state.MissCount = snapshot.MissCount;
            }

            sortAndApplyIfNeeded();
        }

        protected override void Update()
        {
            base.Update();

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

        private void rebuildRows()
        {
            Flow.Clear();
            entryStates.Clear();
            trackedScore = null;
            scroll.ScrollToStart(false);

            if (Session == null)
                return;

            foreach (var entry in Session.Entries)
            {
                GameplayLeaderboardScore leaderboardScore = entry.Tracked
                    ? createTrackedLeaderboardScore()
                    : createGhostLeaderboardScore(entry);

                var drawable = new DrawableGameplayLeaderboardScore(leaderboardScore);
                drawable.Expanded.BindTo(expanded);

                if (entry.Tracked)
                    trackedScore = drawable;

                var state = new RaceEntryState(entry, leaderboardScore, drawable);
                entryStates.Add(state);
                Flow.Add(drawable);
            }

            sortAndApply();
        }

        protected override void OnEntriesChangedScheduled()
        {
            if (needsStructuralRebuild())
                rebuildRows();
            else
            {
                refreshTimelineRefs();
                UpdateDisplay();
            }
        }

        private void refreshTimelineRefs()
        {
            if (Session == null)
                return;

            foreach (var state in entryStates)
            {
                if (state.Tracked)
                    continue;

                state.Timeline = Session.Entries.FirstOrDefault(e => e.ScoreInfo.ID == state.ScoreInfoId)?.Timeline;
            }
        }

        private bool needsStructuralRebuild()
        {
            if (Session == null)
                return entryStates.Count > 0;

            if (entryStates.Count != Session.Entries.Count)
                return true;

            var sessionIds = Session.Entries.Select(e => e.ScoreInfo.ID).OrderBy(id => id).ToArray();
            var stateIds = entryStates.Select(s => s.ScoreInfoId).OrderBy(id => id).ToArray();

            for (int i = 0; i < sessionIds.Length; i++)
            {
                if (sessionIds[i] != stateIds[i])
                    return true;
            }

            return false;
        }

        private static GameplayLeaderboardScore createGhostLeaderboardScore(EzScoreRaceEntry entry)
        {
            var leaderboardScore = new GameplayLeaderboardScore(entry.ScoreInfo, false, GameplayLeaderboardScore.ComboDisplayMode.Highest);
            var scoreInfo = entry.ScoreInfo;
            leaderboardScore.TotalScore.Value = 0;
            leaderboardScore.Accuracy.Value = 0;
            leaderboardScore.Combo.Value = 0;
            leaderboardScore.GetDisplayScore = mode => EzScoreRaceDisplayScore.ForLeaderboardScore(leaderboardScore, scoreInfo, mode);
            return leaderboardScore;
        }

        private GameplayLeaderboardScore createTrackedLeaderboardScore()
        {
            if (GameplayState == null)
                throw new InvalidOperationException("Tracked score requires GameplayState.");

            return new GameplayLeaderboardScore(GameplayState, tracked: true, GameplayLeaderboardScore.ComboDisplayMode.Highest)
            {
                TotalScoreTiebreaker = long.MaxValue,
            };
        }

        private void sortAndApplyIfNeeded()
        {
            var orderedList = getOrderedEntryStates();

            for (int i = 0; i < orderedList.Count; i++)
            {
                if (orderedList[i].LeaderboardScore.DisplayOrder.Value != i + 1)
                {
                    applySortOrder(orderedList);
                    return;
                }
            }
        }

        private void sortAndApply()
            => applySortOrder(getOrderedEntryStates());

        private List<RaceEntryState> getOrderedEntryStates()
        {
            IOrderedEnumerable<RaceEntryState> ordered = SortCriterionSetting.Value switch
            {
                EzScoreRaceMetric.Accuracy => entryStates
                                              .OrderByDescending(s => s.LeaderboardScore.Accuracy.Value)
                                              .ThenByDescending(s => s.LeaderboardScore.TotalScore.Value),

                EzScoreRaceMetric.MaxCombo => entryStates
                                              .OrderByDescending(s => s.LeaderboardScore.Combo.Value)
                                              .ThenByDescending(s => s.LeaderboardScore.TotalScore.Value),

                EzScoreRaceMetric.MissCount => entryStates
                                               .OrderBy(s => getMissCount(s))
                                               .ThenByDescending(s => s.LeaderboardScore.TotalScore.Value),

                _ => entryStates.OrderByDescending(s => s.LeaderboardScore.TotalScore.Value),
            };

            return ordered.ThenBy(s => s.Tracked ? long.MaxValue : s.Tiebreaker).ToList();
        }

        private void applySortOrder(List<RaceEntryState> orderedList)
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

        private int getMissCount(RaceEntryState state)
        {
            if (state.Tracked)
                return ScoreProcessor?.Statistics.GetValueOrDefault(HitResult.Miss) ?? 0;

            return state.MissCount;
        }

        private sealed class RaceEntryState
        {
            public Guid ScoreInfoId { get; }
            public bool Tracked { get; }
            public long Tiebreaker { get; }
            public int MissCount { get; set; }
            public EzScoreTimeline? Timeline { get; set; }
            public GameplayLeaderboardScore LeaderboardScore { get; }
            public DrawableGameplayLeaderboardScore Drawable { get; }

            public RaceEntryState(EzScoreRaceEntry entry, GameplayLeaderboardScore leaderboardScore, DrawableGameplayLeaderboardScore drawable)
            {
                ScoreInfoId = entry.ScoreInfo.ID;
                Tracked = entry.Tracked;
                Tiebreaker = entry.ScoreInfo.Date.ToUnixTimeSeconds();
                Timeline = entry.Timeline;
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

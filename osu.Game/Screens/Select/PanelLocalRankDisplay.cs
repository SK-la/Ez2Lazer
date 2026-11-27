// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Leaderboards;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osuTK;
using Realms;

namespace osu.Game.Screens.Select
{
    public partial class PanelLocalRankDisplay : CompositeDrawable
    {
        private BeatmapInfo? beatmap;

        public BeatmapInfo? Beatmap
        {
            get => beatmap;
            set
            {
                if (beatmap?.Equals(value) == true)
                    return;

                beatmap = value;

                if (IsLoaded)
                    updateSubscription();
            }
        }

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private Ez2ConfigManager ezConfig { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private OsuConfigManager osuConfig { get; set; } = null!;

        [Resolved]
        private IBindable<IReadOnlyList<Mod>> selectedMods { get; set; } = null!;

        private readonly IBindable<APIUser> localUser = new Bindable<APIUser>();

        private IDisposable? scoreSubscription;

        private readonly UpdateableRank updateable;

        private Bindable<LeaderboardModFilter> modFilter = null!;
        private Bindable<bool> filterBySelectedMods = null!;
        private Bindable<LeaderboardSortMode> sorting = null!;
        private Bindable<BeatmapDetailTab> detailTab = null!;
        private Bindable<EzEnumJudgeMode> standardJudgeMode = null!;
        private Bindable<EzEnumJudgeMode> maniaJudgeMode = null!;

        /// <summary>
        /// A detached snapshot lets filter and sorting changes reuse the same score set.
        /// </summary>
        private readonly List<LocalScoreEntry> localScores = new List<LocalScoreEntry>();

        public bool HasRank => updateable.Rank != null;

        public PanelLocalRankDisplay(BeatmapInfo? beatmap = null)
        {
            AutoSizeAxes = Axes.Both;

            InternalChild = updateable = new UpdateableRank(animate: false)
            {
                Size = new Vector2(40, 20),
                Alpha = 0,
            };

            Beatmap = beatmap;
        }

        [BackgroundDependencyLoader]
        private void load(IAPIProvider api)
        {
            localUser.BindTo(api.LocalUser);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            modFilter = ezConfig.GetBindable<LeaderboardModFilter>(Ez2Setting.BeatmapDetailScoreModFilter);
            filterBySelectedMods = osuConfig.GetBindable<bool>(OsuSetting.BeatmapDetailModsFilter);
            sorting = osuConfig.GetBindable<LeaderboardSortMode>(OsuSetting.BeatmapLeaderboardSortMode);
            detailTab = osuConfig.GetBindable<BeatmapDetailTab>(OsuSetting.BeatmapDetailTab);
            standardJudgeMode = ezConfig.GetBindable<EzEnumJudgeMode>(Ez2Setting.StdJudgeMode);
            maniaJudgeMode = ezConfig.GetBindable<EzEnumJudgeMode>(Ez2Setting.ManiaJudgeMode);

            modFilter.BindValueChanged(_ => updateRank());
            filterBySelectedMods.BindValueChanged(_ => updateRank());
            sorting.BindValueChanged(_ => updateRank());
            detailTab.BindValueChanged(_ => updateRank());
            standardJudgeMode.BindValueChanged(onJudgeModeChanged);
            maniaJudgeMode.BindValueChanged(onJudgeModeChanged);
            selectedMods.BindValueChanged(_ => updateRank());

            ruleset.BindValueChanged(_ => updateSubscription());
            localUser.BindValueChanged(_ => updateSubscription(), true);
        }

        private void updateSubscription()
        {
            scoreSubscription?.Dispose();
            localScores.Clear();
            setRank(null);

            if (beatmap == null)
                return;

            scoreSubscription = realm.RegisterForNotifications(r => r.All<ScoreInfo>().Where(s => s.BeatmapHash == beatmap.Hash && !s.DeletePending), localScoresChanged);
        }

        private void localScoresChanged(IRealmCollection<ScoreInfo> sender, ChangeSet? changes)
        {
            // This subscription may fire from changes to linked beatmaps, which we don't care about.
            // It's currently not possible for a score to be modified after insertion, so we can safely ignore callbacks with only modifications.
            if (changes?.HasCollectionChanges() == false)
                return;

            localScores.Clear();

            foreach (var score in sender)
            {
                if (score.UserID != localUser.Value.Id && score.UserID > 1)
                    continue;

                if (!ruleset.Value.Equals(score.Ruleset))
                    continue;

                localScores.Add(new LocalScoreEntry(
                    score.Rank,
                    score.TotalScore,
                    score.Accuracy,
                    score.MaxCombo,
                    score.Statistics.GetValueOrDefault(HitResult.Miss, 0),
                    score.Date.UtcDateTime.Ticks,
                    score.Mods,
                    score.JudgeMode));
            }

            updateRank();
        }

        private void updateRank()
        {
            if (IsDisposed)
                return;

            if (beatmap == null)
            {
                setRank(null);
                return;
            }

            IEnumerable<LocalScoreEntry> matchingScores = localScores;

            if (filterBySelectedMods.Value)
            {
                if (modFilter.Value == LeaderboardModFilter.None)
                    matchingScores = matchingScores.Where(score => EzLocalScoreQueries.MatchesExactMods(score.Mods, selectedMods.Value));
                else
                {
                    var selectedJudgeMode = ezConfig.GetGameplayEnvironment(ruleset.Value).JudgeMode;
                    matchingScores = matchingScores.Where(score =>
                        EzLocalScoreQueries.MatchesLeaderboardMods(score.Mods, selectedMods.Value, modFilter.Value)
                        && EzLocalScoreQueries.JudgeModeMatches(score.JudgeMode, selectedJudgeMode, modFilter.Value));
                }
            }

            LeaderboardSortMode effectiveSorting = detailTab.Value == BeatmapDetailTab.Local
                ? sorting.Value
                : LeaderboardSortMode.Score;
            setRank(orderByCriteria(matchingScores, effectiveSorting).Select(score => (ScoreRank?)score.Rank).FirstOrDefault());
        }

        // Keep these keys aligned with ScoreInfoExtensions.OrderByCriteria.
        private static IOrderedEnumerable<LocalScoreEntry> orderByCriteria(IEnumerable<LocalScoreEntry> scores, LeaderboardSortMode sorting)
        {
            switch (sorting)
            {
                case LeaderboardSortMode.Score:
                    return scores.OrderByDescending(score => score.TotalScore);

                case LeaderboardSortMode.Accuracy:
                    return scores.OrderByDescending(score => score.Accuracy).ThenByDescending(score => score.TotalScore);

                case LeaderboardSortMode.MaxCombo:
                    return scores.OrderByDescending(score => score.MaxCombo).ThenByDescending(score => score.TotalScore);

                case LeaderboardSortMode.Misses:
                    return scores.OrderBy(score => score.Misses).ThenByDescending(score => score.TotalScore);

                case LeaderboardSortMode.Date:
                    return scores.OrderByDescending(score => score.DateTicks);

                default:
                    throw new ArgumentOutOfRangeException(nameof(sorting), sorting, null);
            }
        }

        private void onJudgeModeChanged(ValueChangedEvent<EzEnumJudgeMode> _)
        {
            if (filterBySelectedMods.Value && modFilter.Value.HasFlag(LeaderboardModFilter.Difficulty))
                updateRank();
        }

        private void setRank(ScoreRank? rank)
        {
            updateable.Rank = rank;
            updateable.Alpha = rank != null ? 1 : 0;
        }

        protected override void Dispose(bool isDisposing)
        {
            scoreSubscription?.Dispose();
            beatmap = null;
            localScores.Clear();

            if (IsLoaded)
            {
                modFilter.UnbindAll();
                filterBySelectedMods.UnbindAll();
                sorting.UnbindAll();
                detailTab.UnbindAll();
                standardJudgeMode.ValueChanged -= onJudgeModeChanged;
                maniaJudgeMode.ValueChanged -= onJudgeModeChanged;
            }

            base.Dispose(isDisposing);
        }

        private readonly record struct LocalScoreEntry(ScoreRank Rank, long TotalScore, double Accuracy, int MaxCombo, int Misses, long DateTicks, Mod[] Mods, int JudgeMode);
    }
}

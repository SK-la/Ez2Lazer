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
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private OsuConfigManager osuConfig { get; set; } = null!;

        [Resolved]
        private Ez2ConfigManager ezConfig { get; set; } = null!;

        [Resolved]
        private IBindable<IReadOnlyList<Mod>> selectedMods { get; set; } = null!;

        private readonly IBindable<APIUser> localUser = new Bindable<APIUser>();

        private IDisposable? scoreSubscription;

        private readonly UpdateableRank updateable;

        private Bindable<bool> filterBySelectedMods = null!;
        private Bindable<bool> rankFollowsModFilter = null!;

        /// <summary>
        /// 当前谱面本地成绩的最小快照：Mod / 筛选开关变化时要在同一批成绩上重算，不能只留一条 top。
        /// </summary>
        private readonly List<LocalScoreEntry> entries = new List<LocalScoreEntry>();

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

            filterBySelectedMods = osuConfig.GetBindable<bool>(OsuSetting.BeatmapDetailModsFilter);
            rankFollowsModFilter = ezConfig.GetBindable<bool>(Ez2Setting.SongSelectPanelRankFollowsModFilter);

            filterBySelectedMods.BindValueChanged(_ => updateRank());
            rankFollowsModFilter.BindValueChanged(_ => updateRank());
            selectedMods.BindValueChanged(_ => updateRank());

            ruleset.BindValueChanged(_ => updateSubscription());
            localUser.BindValueChanged(_ => updateSubscription(), true);
        }

        private void updateSubscription()
        {
            scoreSubscription?.Dispose();
            entries.Clear();
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

            entries.Clear();

            foreach (var score in sender)
            {
                if (score.UserID != localUser.Value.Id && score.UserID > 1)
                    continue;

                if (!ruleset.Value.Equals(score.Ruleset))
                    continue;

                entries.Add(new LocalScoreEntry(score.Rank, score.TotalScore, score.Date.UtcDateTime.Ticks, score.Mods));
            }

            updateRank();
        }

        /// <summary>
        /// 重算面板评级。筛选开关未生效时保持上游语义（最高分那条成绩的评级）。
        /// </summary>
        private void updateRank()
        {
            if (IsDisposed)
                return;

            if (beatmap == null)
            {
                setRank(null);
                return;
            }

            bool followsModFilter = rankFollowsModFilter.Value && filterBySelectedMods.Value;

            ScoreRank? best = null;
            long bestTotalScore = long.MinValue;
            long bestDateTicks = long.MaxValue;

            foreach (var entry in entries)
            {
                if (followsModFilter)
                {
                    if (!EzLocalScoreQueries.MatchesExactMods(entry.Mods, selectedMods.Value))
                        continue;

                    if (best == null || entry.Rank > best.Value)
                        best = entry.Rank;

                    continue;
                }

                // 未过滤：TotalScore 最高者优先，同分取更早的成绩。
                if (best == null
                    || entry.TotalScore > bestTotalScore
                    || (entry.TotalScore == bestTotalScore && entry.DateTicks < bestDateTicks))
                {
                    best = entry.Rank;
                    bestTotalScore = entry.TotalScore;
                    bestDateTicks = entry.DateTicks;
                }
            }

            setRank(best);
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
            entries.Clear();
            filterBySelectedMods?.UnbindAll();
            rankFollowsModFilter?.UnbindAll();
            base.Dispose(isDisposing);
        }

        private readonly record struct LocalScoreEntry(ScoreRank Rank, long TotalScore, long DateTicks, Mod[] Mods);
    }
}

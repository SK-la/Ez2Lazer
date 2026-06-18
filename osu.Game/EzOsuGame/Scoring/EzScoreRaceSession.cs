// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Bindables;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Scoring;
using osu.Game.Screens.Play;

namespace osu.Game.EzOsuGame.Scoring
{
    /// <summary>
    /// 单局共享角逐 Session：Mod 过滤、按指标 Pick ghost、时间线构建的唯一入口。
    /// 两个 HUD 只读 <see cref="Entries"/> / 调用 <see cref="PickGhost"/>，禁止各自查 Realm 或自建 timeline。
    /// </summary>
    public sealed class EzScoreRaceSession
    {
        private readonly RealmAccess realm;
        private readonly ScoreManager scoreManager;
        private readonly BeatmapManager beatmaps;
        private readonly GameplayState gameplayState;
        private readonly Action<Action>? scheduleCallback;

        private readonly BindableBool isReady = new BindableBool();
        private readonly List<EzScoreRaceEntry> entries = new List<EzScoreRaceEntry>();
        private readonly Dictionary<Guid, EzScoreRaceEntry> pickEntries = new Dictionary<Guid, EzScoreRaceEntry>();
        private readonly HashSet<Guid> timelineLoadsPending = new HashSet<Guid>();

        private CancellationTokenSource? loadCancellation;
        private EzScoreModFilter modFilter = EzScoreModFilter.SameAsCurrent;
        private List<ScoreInfo> modFilteredScores = new List<ScoreInfo>();
        private bool scorePoolDirty = true;

        public IBindable<bool> IsReady => isReady;
        public EzScoreRacePlayMode PlayMode { get; }

        public IReadOnlyList<EzScoreRaceEntry> Entries => entries;
        public int MaxEntryCount { get; private set; } = 10;

        public event Action? EntriesChanged;

        public EzScoreRaceSession(RealmAccess realm, ScoreManager scoreManager, BeatmapManager beatmaps, GameplayState gameplayState, EzScoreRacePlayMode playMode,
                                  Action<Action>? scheduleCallback = null)
        {
            this.realm = realm;
            this.scoreManager = scoreManager;
            this.beatmaps = beatmaps;
            this.gameplayState = gameplayState;
            PlayMode = playMode;
            this.scheduleCallback = scheduleCallback;
        }

        public void Configure(EzScoreModFilter filter, int maxEntriesCount)
        {
            modFilter = filter;
            MaxEntryCount = Math.Clamp(maxEntriesCount, 1, 10);
        }

        public void EnsureLoaded()
        {
            if (loadCancellation != null)
                return;

            loadCancellation = new CancellationTokenSource();
            beginLoad(loadCancellation.Token);
        }

        public void ReloadIfNeeded(EzScoreModFilter filter, int maxEntriesCount)
        {
            int clamped = Math.Clamp(maxEntriesCount, 1, 10);

            if (modFilter == filter && MaxEntryCount == clamped && loadCancellation != null)
                return;

            modFilter = filter;
            MaxEntryCount = clamped;
            cancelLoad();
            entries.RemoveAll(e => !e.Tracked);
            pickEntries.Clear();
            scorePoolDirty = true;
            loadCancellation = new CancellationTokenSource();
            beginLoad(loadCancellation.Token);
        }

        /// <summary>
        /// 在 Mod 过滤后的全量候选里，按指标选出 ghost 并确保 Session 条目与时间线加载。
        /// </summary>
        public EzScoreRaceEntry? PickGhost(EzScoreRaceMetric metric, ScoreInfo? exclude = null)
        {
            if (metric == EzScoreRaceMetric.TheoreticalMaxScore)
                return null;

            var scoreInfo = EzLocalScoreQueries.PickBest(GetModFilteredScores(), metric, exclude);

            if (scoreInfo == null)
                return null;

            var entry = getOrCreatePickEntry(scoreInfo);
            syncTimelineFromEntries(entry);

            if (entry.Timeline == null)
                ensureTimelinesLoaded(new[] { scoreInfo });

            return entry;
        }

        /// <summary>
        /// 读取 ghost 在指定时钟的 timeline 分数；未就绪返回 0（禁止终局分回退）。
        /// </summary>
        public static long QueryTimelineScore(EzScoreRaceEntry? entry, double clockTime)
        {
            if (entry == null || entry.Tracked || entry.Timeline == null)
                return 0;

            return entry.Timeline.QueryAtTime(clockTime).TotalScore;
        }

        public IReadOnlyList<ScoreInfo> GetModFilteredScores()
        {
            if (!scorePoolDirty)
                return modFilteredScores;

            var beatmapInfo = gameplayState.Beatmap.BeatmapInfo;
            var rulesetInfo = gameplayState.Ruleset.RulesetInfo;
            var currentMods = gameplayState.Mods.ToArray();

            var localScores = EzLocalScoreQueries.GetLocalScoresWithReplay(realm, beatmapInfo, rulesetInfo);
            modFilteredScores = EzLocalScoreQueries.FilterByMods(localScores, currentMods, modFilter).ToList();
            scorePoolDirty = false;
            return modFilteredScores;
        }

        public void Dispose()
        {
            cancelLoad();
        }

        private void beginLoad(CancellationToken cancellationToken)
        {
            schedule(() =>
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                ensureTrackedEntry();

                if (PlayMode != EzScoreRacePlayMode.SpectatingLive)
                    ensureLeaderboardGhostPlaceholders();

                isReady.Value = true;
                notifyEntriesChanged();
            });

            if (PlayMode == EzScoreRacePlayMode.SpectatingLive)
                return;

            ensureTimelinesLoaded(queryLeaderboardGhostScores(), cancellationToken);
        }

        private void ensureTrackedEntry()
        {
            if (entries.Any(e => e.Tracked))
                return;

            entries.Add(new EzScoreRaceEntry(gameplayState.Score.ScoreInfo, tracked: true));
        }

        private IReadOnlyList<ScoreInfo> queryLeaderboardGhostScores()
            => EzLocalScoreQueries.GetTopByTotalScore(GetModFilteredScores(), MaxEntryCount);

        private void ensureLeaderboardGhostPlaceholders()
        {
            foreach (var scoreInfo in queryLeaderboardGhostScores())
                ensureGhostEntry(scoreInfo);
        }

        private EzScoreRaceEntry getOrCreatePickEntry(ScoreInfo scoreInfo)
        {
            if (pickEntries.TryGetValue(scoreInfo.ID, out var existing))
            {
                syncTimelineFromEntries(existing);
                return existing;
            }

            var pickEntry = new EzScoreRaceEntry(scoreInfo);
            syncTimelineFromEntries(pickEntry);
            pickEntries[scoreInfo.ID] = pickEntry;
            return pickEntry;
        }

        private EzScoreRaceEntry ensureGhostEntry(ScoreInfo scoreInfo)
        {
            var existing = entries.FirstOrDefault(e => !e.Tracked && e.ScoreInfo.ID == scoreInfo.ID);

            if (existing != null)
                return existing;

            int trackedIndex = entries.FindIndex(e => e.Tracked);
            var ghostEntry = new EzScoreRaceEntry(scoreInfo);

            if (trackedIndex >= 0)
                entries.Insert(trackedIndex, ghostEntry);
            else
                entries.Add(ghostEntry);

            syncTimelineFromEntries(ghostEntry);
            return ghostEntry;
        }

        private void syncTimelineFromEntries(EzScoreRaceEntry entry)
        {
            if (entry.Timeline != null)
                return;

            var sessionEntry = entries.FirstOrDefault(e => e.ScoreInfo.ID == entry.ScoreInfo.ID);

            if (sessionEntry?.Timeline != null)
                entry.Timeline = sessionEntry.Timeline;
        }

        private void assignTimeline(ScoreInfo scoreInfo, EzScoreTimeline timeline)
        {
            var sessionEntry = entries.FirstOrDefault(e => !e.Tracked && e.ScoreInfo.ID == scoreInfo.ID);

            if (sessionEntry != null)
                sessionEntry.Timeline = timeline;

            if (pickEntries.TryGetValue(scoreInfo.ID, out var pickEntry))
                pickEntry.Timeline = timeline;
        }

        private void ensureTimelinesLoaded(IEnumerable<ScoreInfo> scoreInfos, CancellationToken cancellationToken = default)
        {
            if (PlayMode == EzScoreRacePlayMode.SpectatingLive)
                return;

            cancellationToken = cancellationToken == CancellationToken.None ? loadCancellation?.Token ?? CancellationToken.None : cancellationToken;

            if (cancellationToken == CancellationToken.None)
                return;

            var scoresToLoad = scoreInfos
                               .Where(s => !hasTimeline(s) && timelineLoadsPending.Add(s.ID))
                               .ToList();

            if (scoresToLoad.Count == 0)
                return;

            var currentMods = gameplayState.Mods.ToArray();
            IBeatmap sharedPlayableBeatmap = gameplayState.Beatmap;

            Task.Run(async () =>
            {
                try
                {
                    var buildTasks = scoresToLoad.Select(scoreInfo => Task.Run(() =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        try
                        {
                            IBeatmap? beatmapForBuild = EzLocalScoreQueries.ModsMatch(scoreInfo.Mods, currentMods)
                                ? sharedPlayableBeatmap
                                : null;

                            return (scoreInfo, EzScoreTimelineBuilder.TryBuild(scoreManager, beatmaps, scoreInfo, beatmapForBuild, cancellationToken));
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            Logger.Error(ex, $"[EzScoreRace] Failed to build timeline for score {scoreInfo.ID}", Ez2ConfigManager.LOGGER_NAME);
                            return (scoreInfo, null);
                        }
                    }, cancellationToken)).ToArray();

                    var results = await Task.WhenAll(buildTasks).ConfigureAwait(false);

                    foreach (var (scoreInfo, timeline) in results)
                    {
                        if (cancellationToken.IsCancellationRequested)
                            return;

                        schedule(() =>
                        {
                            timelineLoadsPending.Remove(scoreInfo.ID);

                            if (cancellationToken.IsCancellationRequested)
                                return;

                            if (timeline == null)
                                return;

                            if (hasTimeline(scoreInfo))
                                return;

                            assignTimeline(scoreInfo, timeline);
                            notifyEntriesChanged();
                        });
                    }
                }
                catch (OperationCanceledException)
                {
                }
            }, cancellationToken);
        }

        private bool hasTimeline(ScoreInfo scoreInfo)
        {
            if (entries.FirstOrDefault(e => e.ScoreInfo.ID == scoreInfo.ID)?.Timeline != null)
                return true;

            return pickEntries.TryGetValue(scoreInfo.ID, out var pickEntry) && pickEntry.Timeline != null;
        }

        private void notifyEntriesChanged() => EntriesChanged?.Invoke();

        private void schedule(Action action)
        {
            if (scheduleCallback != null)
                scheduleCallback(action);
            else
                action();
        }

        private void cancelLoad()
        {
            loadCancellation?.Cancel();
            loadCancellation?.Dispose();
            loadCancellation = null;
            timelineLoadsPending.Clear();
        }
    }
}

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
        private readonly HashSet<Guid> timelineLoadsPending = new HashSet<Guid>();

        private CancellationTokenSource? loadCancellation;
        private EzScoreModFilter modFilter = EzScoreModFilter.SameAsCurrent;
        private List<ScoreInfo> modFilteredScores = new List<ScoreInfo>();
        private bool scorePoolDirty = true;
        private bool entriesChangedScheduled;

        public IBindable<bool> IsReady => isReady;
        public bool SupportsGhostRace { get; }

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
            SupportsGhostRace = EzScoreRaceRulesetSupport.SupportsGhostRace(gameplayState.Ruleset.RulesetInfo)
                                && playMode != EzScoreRacePlayMode.SpectatingLive;
            this.scheduleCallback = scheduleCallback;
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
            scorePoolDirty = true;
            loadCancellation = new CancellationTokenSource();
            beginLoad(loadCancellation.Token);
        }

        /// <summary>
        /// 在 Mod 过滤后的全量候选里，按指标选出 ghost 并确保 Session 条目与时间线加载。
        /// </summary>
        public EzScoreRaceEntry? PickGhost(EzScoreRaceMetric metric)
        {
            if (!SupportsGhostRace)
                return null;

            var scoreInfo = EzLocalScoreQueries.PickBest(getModFilteredScores(), metric);

            if (scoreInfo == null)
                return null;

            var entry = ensureGhostEntry(scoreInfo);

            if (entry.Timeline == null)
                ensureTimelinesLoaded(new[] { scoreInfo });

            return entry;
        }

        /// <summary>
        /// 读取 ghost 在指定时钟的 timeline 快照；未就绪返回 <see cref="EzScoreTimelineSnapshot.Empty"/>。
        /// </summary>
        public static EzScoreTimelineSnapshot QuerySnapshot(EzScoreRaceEntry? entry, double clockTime)
        {
            if (entry == null || entry.Tracked)
                return EzScoreTimelineSnapshot.Empty;

            return QuerySnapshot(entry.Timeline, clockTime);
        }

        public static EzScoreTimelineSnapshot QuerySnapshot(EzScoreTimeline? timeline, double clockTime)
        {
            if (timeline == null)
                return EzScoreTimelineSnapshot.Empty;

            return timeline.QueryAtTime(clockTime);
        }

        /// <summary>
        /// 读取 ghost 在指定时钟的 timeline 分数；未就绪返回 0（禁止终局分回退）。
        /// </summary>
        public static long QueryTimelineScore(EzScoreRaceEntry? entry, double clockTime)
            => QuerySnapshot(entry, clockTime).TotalScore;

        private IReadOnlyList<ScoreInfo> getModFilteredScores()
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
            Logger.Log(
                $"[EzScore] Session.beginLoad: supportsGhost={SupportsGhostRace} filter={modFilter} maxEntries={MaxEntryCount}",
                level: LogLevel.Debug,
                name: Ez2ConfigManager.LOGGER_NAME);

            schedule(() =>
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                ensureTrackedEntry();
            });

            if (!SupportsGhostRace)
            {
                schedule(() =>
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        Logger.Log(
                            "[EzScore] Session.beginLoad: no ghost support, marking ready immediately",
                            level: LogLevel.Debug,
                            name: Ez2ConfigManager.LOGGER_NAME);
                        isReady.Value = true;
                        notifyEntriesChanged();
                    }
                });

                return;
            }

            // 在后台线程查询本地成绩，避免同步 Realm 查询阻塞主线程加载流程。
            // 查询完成后回主线程创建 ghost 占位、标记 isReady、启动时间线构建。
            Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var ghostScores = queryLeaderboardGhostScores();

                Logger.Log(
                    $"[EzScore] Session.beginLoad: queried {ghostScores.Count} ghost scores from DB",
                    level: LogLevel.Debug,
                    name: Ez2ConfigManager.LOGGER_NAME);

                schedule(() =>
                {
                    if (cancellationToken.IsCancellationRequested)
                        return;

                    ensureLeaderboardGhostPlaceholders(ghostScores);
                    Logger.Log(
                        $"[EzScore] Session.beginLoad: set isReady=true after {ghostScores.Count} ghost placeholders",
                        level: LogLevel.Debug,
                        name: Ez2ConfigManager.LOGGER_NAME);
                    isReady.Value = true;
                    notifyEntriesChanged();
                });

                ensureTimelinesLoaded(ghostScores, cancellationToken);
            }, cancellationToken);
        }

        private void ensureTrackedEntry()
        {
            if (entries.Any(e => e.Tracked))
                return;

            entries.Add(new EzScoreRaceEntry(gameplayState.Score.ScoreInfo, tracked: true));
        }

        private IReadOnlyList<ScoreInfo> queryLeaderboardGhostScores()
            => EzLocalScoreQueries.GetTopByTotalScore(getModFilteredScores(), MaxEntryCount);

        private void ensureLeaderboardGhostPlaceholders()
        {
            foreach (var scoreInfo in queryLeaderboardGhostScores())
                ensureGhostEntry(scoreInfo);
        }

        private void ensureLeaderboardGhostPlaceholders(IReadOnlyList<ScoreInfo> ghostScores)
        {
            foreach (var scoreInfo in ghostScores)
                ensureGhostEntry(scoreInfo);
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

            return ghostEntry;
        }

        private void assignTimeline(ScoreInfo scoreInfo, EzScoreTimeline timeline)
        {
            var entry = entries.FirstOrDefault(e => e.ScoreInfo.ID == scoreInfo.ID);

            if (entry != null)
            {
                entry.Timeline = timeline;

                Logger.Log(
                    $"[EzScore] assignTimeline: score {scoreInfo.ID} assigned timeline, FinalTotalScore={timeline.FinalTotalScore}",
                    level: LogLevel.Debug,
                    name: Ez2ConfigManager.LOGGER_NAME);
            }
            else
            {
                Logger.Log(
                    $"[EzScore] assignTimeline: score {scoreInfo.ID} has timeline but entry not found in session entries",
                    level: LogLevel.Debug,
                    name: Ez2ConfigManager.LOGGER_NAME);
            }
        }

        private void ensureTimelinesLoaded(IEnumerable<ScoreInfo> scoreInfos, CancellationToken cancellationToken = default)
        {
            if (!SupportsGhostRace)
                return;

            cancellationToken = cancellationToken == CancellationToken.None ? loadCancellation?.Token ?? CancellationToken.None : cancellationToken;

            if (cancellationToken == CancellationToken.None)
                return;

            var scoresToLoad = scoreInfos
                               .Where(s => !hasTimeline(s) && timelineLoadsPending.Add(s.ID))
                               .ToList();

            if (scoresToLoad.Count == 0)
                return;

            Logger.Log(
                $"[EzScore] ensureTimelinesLoaded: building {scoresToLoad.Count} timelines in background",
                level: LogLevel.Debug,
                name: Ez2ConfigManager.LOGGER_NAME);

            var currentMods = gameplayState.Mods.ToArray();
            IBeatmap sharedPlayableBeatmap = gameplayState.Beatmap;

            Task.Run(async () =>
            {
                try
                {
                    Logger.Log(
                        "[EzScore] ensureTimelinesLoaded: inner Task.Run started",
                        level: LogLevel.Debug,
                        name: Ez2ConfigManager.LOGGER_NAME);

                    var buildTasks = scoresToLoad.Select(scoreInfo => Task.Run(() =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        try
                        {
                            IBeatmap? beatmapForBuild = EzLocalScoreQueries.GameplayModsMatch(scoreInfo.Mods, currentMods)
                                ? sharedPlayableBeatmap
                                : null;

                            var timeline = EzScoreTimelineBuilder.TryBuild(scoreManager, beatmaps, scoreInfo, beatmapForBuild, cancellationToken);

                            Logger.Log(
                                $"[EzScore] ensureTimelinesLoaded: TryBuild for score {scoreInfo.ID} returned {(timeline != null ? $"timeline(FinalTotal={timeline.FinalTotalScore})" : "null")}",
                                level: LogLevel.Debug,
                                name: Ez2ConfigManager.LOGGER_NAME);

                            return (scoreInfo, timeline);
                        }
                        catch (OperationCanceledException)
                        {
                            Logger.Log(
                                $"[EzScore] ensureTimelinesLoaded: TryBuild cancelled for score {scoreInfo.ID}",
                                level: LogLevel.Debug,
                                name: Ez2ConfigManager.LOGGER_NAME);
                            throw;
                        }
                        catch (Exception ex)
                        {
                            Logger.Error(ex, $"[EzScoreRace] Failed to build timeline for score {scoreInfo.ID}", Ez2ConfigManager.LOGGER_NAME);
                            return (scoreInfo, null);
                        }
                    }, cancellationToken)).ToArray();

                    Logger.Log(
                        $"[EzScore] ensureTimelinesLoaded: awaiting {buildTasks.Length} build tasks",
                        level: LogLevel.Debug,
                        name: Ez2ConfigManager.LOGGER_NAME);

                    var results = await Task.WhenAll(buildTasks).ConfigureAwait(false);

                    int successCount = results.Count(r => r.Item2 != null);

                    Logger.Log(
                        $"[EzScore] ensureTimelinesLoaded: {successCount}/{results.Length} timelines built successfully",
                        level: LogLevel.Debug,
                        name: Ez2ConfigManager.LOGGER_NAME);

                    schedule(() =>
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            Logger.Log(
                                "[EzScore] ensureTimelinesLoaded: schedule skipped (cancelled)",
                                level: LogLevel.Debug,
                                name: Ez2ConfigManager.LOGGER_NAME);
                            return;
                        }

                        Logger.Log(
                            $"[EzScore] ensureTimelinesLoaded: schedule assigning {results.Count(r => r.Item2 != null)} timelines to {results.Length} entries",
                            level: LogLevel.Debug,
                            name: Ez2ConfigManager.LOGGER_NAME);

                        foreach (var (scoreInfo, timeline) in results)
                        {
                            timelineLoadsPending.Remove(scoreInfo.ID);

                            if (timeline == null)
                                continue;

                            if (hasTimeline(scoreInfo))
                                continue;

                            assignTimeline(scoreInfo, timeline);
                        }

                        notifyEntriesChanged();
                    });
                }
                catch (OperationCanceledException)
                {
                    Logger.Log(
                        "[EzScore] ensureTimelinesLoaded: cancelled",
                        level: LogLevel.Debug,
                        name: Ez2ConfigManager.LOGGER_NAME);
                }
            }, cancellationToken);
        }

        private bool hasTimeline(ScoreInfo scoreInfo)
            => entries.FirstOrDefault(e => e.ScoreInfo.ID == scoreInfo.ID)?.Timeline != null;

        private void notifyEntriesChanged()
        {
            if (entriesChangedScheduled)
                return;

            entriesChangedScheduled = true;
            schedule(() =>
            {
                entriesChangedScheduled = false;
                EntriesChanged?.Invoke();
            });
        }

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

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
    public sealed class EzScoreRaceSession
    {
        private readonly RealmAccess realm;
        private readonly ScoreManager scoreManager;
        private readonly BeatmapManager beatmaps;
        private readonly GameplayState gameplayState;
        private readonly Action<Action>? scheduleCallback;

        private readonly BindableBool isReady = new BindableBool();
        private readonly List<EzScoreRaceEntry> entries = new List<EzScoreRaceEntry>();

        private CancellationTokenSource? loadCancellation;
        private EzScoreModFilter modFilter = EzScoreModFilter.SameAsCurrent;

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
            this.PlayMode = playMode;
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
            loadCancellation = new CancellationTokenSource();
            beginLoad(loadCancellation.Token);
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
                    ensureGhostPlaceholders();

                isReady.Value = true;
                notifyEntriesChanged();
            });

            if (PlayMode == EzScoreRacePlayMode.SpectatingLive)
                return;

            loadGhostTimelinesAsync(cancellationToken);
        }

        private void ensureTrackedEntry()
        {
            if (entries.Any(e => e.Tracked))
                return;

            entries.Add(new EzScoreRaceEntry(gameplayState.Score.ScoreInfo, tracked: true));
        }

        private IReadOnlyList<ScoreInfo> queryGhostScores()
        {
            var beatmapInfo = gameplayState.Beatmap.BeatmapInfo;
            var rulesetInfo = gameplayState.Ruleset.RulesetInfo;
            var currentMods = gameplayState.Mods.ToArray();

            var localScores = EzLocalScoreQueries.GetLocalScoresWithReplay(realm, beatmapInfo, rulesetInfo);
            var filteredScores = EzLocalScoreQueries.FilterByMods(localScores, currentMods, modFilter).ToList();
            return EzLocalScoreQueries.GetTopByTotalScore(filteredScores, MaxEntryCount);
        }

        private void ensureGhostPlaceholders()
        {
            foreach (var scoreInfo in queryGhostScores())
            {
                if (entries.Any(e => !e.Tracked && e.ScoreInfo.ID == scoreInfo.ID))
                    continue;

                int trackedIndex = entries.FindIndex(e => e.Tracked);
                var ghostEntry = new EzScoreRaceEntry(scoreInfo);

                if (trackedIndex >= 0)
                    entries.Insert(trackedIndex, ghostEntry);
                else
                    entries.Add(ghostEntry);
            }
        }

        private void loadGhostTimelinesAsync(CancellationToken cancellationToken)
        {
            var topScores = queryGhostScores();
            var currentMods = gameplayState.Mods.ToArray();
            IBeatmap? sharedPlayableBeatmap = gameplayState.Beatmap;

            Task.Run(async () =>
            {
                try
                {
                    var buildTasks = topScores.Select(scoreInfo => Task.Run(() =>
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

                        if (timeline == null)
                            continue;

                        schedule(() =>
                        {
                            if (cancellationToken.IsCancellationRequested)
                                return;

                            var entry = entries.FirstOrDefault(e => !e.Tracked && e.ScoreInfo.ID == scoreInfo.ID);

                            if (entry == null || entry.Timeline != null)
                                return;

                            entry.Timeline = timeline;
                            notifyEntriesChanged();
                        });
                    }
                }
                catch (OperationCanceledException)
                {
                }
            }, cancellationToken);
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
        }
    }
}

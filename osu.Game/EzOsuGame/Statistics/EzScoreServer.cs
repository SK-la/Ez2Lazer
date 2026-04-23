// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Screens;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens;
using osu.Game.Screens.Play;
using osu.Game.Screens.Play.Leaderboards;
using osu.Game.Screens.Ranking;
using osu.Game.Users;

namespace osu.Game.EzOsuGame.Statistics
{
    /// <summary>
    /// 通过隐藏的真实 replay 判定链生成命中事件。
    /// </summary>
    public static partial class EzScoreServer
    {
        private const double default_analysis_playback_rate = 40;

        private static readonly ConcurrentDictionary<string, IReadOnlyList<HitEvent>> cached_hit_events = new ConcurrentDictionary<string, IReadOnlyList<HitEvent>>();

        private static readonly IAnalysisPlayerStrategy[] analysis_strategies =
        {
            new CatchAnalysisPlayerStrategy(),
            new DefaultAnalysisPlayerStrategy(),
        };

        private static bool tryGetCached(ScoreInfo scoreInfo, out IReadOnlyList<HitEvent> hitEvents)
        {
            hitEvents = Array.Empty<HitEvent>();

            string? key = getCacheKey(scoreInfo);
            if (string.IsNullOrEmpty(key))
                return false;

            return cached_hit_events.TryGetValue(key, out hitEvents!);
        }

        public static void Invalidate(ScoreInfo? scoreInfo)
        {
            if (scoreInfo == null)
                return;

            string? key = getCacheKey(scoreInfo);
            if (string.IsNullOrEmpty(key))
                return;

            cached_hit_events.TryRemove(key, out _);
        }

        private static void storeCached(ScoreInfo scoreInfo, IReadOnlyList<HitEvent> hitEvents)
        {
            if (hitEvents.Count == 0)
                return;

            string? key = getCacheKey(scoreInfo);
            if (string.IsNullOrEmpty(key))
                return;

            cached_hit_events[key] = cloneHitEvents(hitEvents);
        }

        private static string? getCacheKey(ScoreInfo scoreInfo)
        {
            if (!string.IsNullOrEmpty(scoreInfo.Hash))
                return $"hash:{scoreInfo.Hash}";

            if (scoreInfo.ID != Guid.Empty)
                return $"id:{scoreInfo.ID}";

            if (scoreInfo.OnlineID > 0)
                return $"online:{scoreInfo.OnlineID}";

            if (scoreInfo.LegacyOnlineID > 0)
                return $"legacy:{scoreInfo.LegacyOnlineID}";

            return null;
        }

        private static List<HitEvent> cloneHitEvents(IReadOnlyList<HitEvent> hitEvents) => new List<HitEvent>(hitEvents);

        public static int GetRecommendedAnalysisTimeoutMs(Score score, IBeatmap playableBeatmap)
        {
            ArgumentNullException.ThrowIfNull(score);
            ArgumentNullException.ThrowIfNull(playableBeatmap);

            return getStrategy(score.ScoreInfo.Ruleset).GetTimeoutMs(score, playableBeatmap);
        }

        private static IAnalysisPlayerStrategy getStrategy(RulesetInfo? rulesetInfo)
            => analysis_strategies.FirstOrDefault(strategy => strategy.AppliesTo(rulesetInfo)) ?? analysis_strategies[^1];

        /// <summary>
        /// 一个隐藏的分析宿主，用来在结果页里驱动真实 replay 判定。
        /// </summary>
        public partial class AnalysisHost : CompositeDrawable
        {
            private readonly BackgroundScreenStack backgroundScreenStack;
            private readonly ScreenStack screenStack;

            private AnalysisPlayer? currentPlayer;
            private TaskCompletionSource<List<HitEvent>?>? currentTask;
            private CancellationTokenRegistration cancellationRegistration;
            private long requestId;

            public override bool HandlePositionalInput => false;

            public override bool HandleNonPositionalInput => false;

            public override bool PropagatePositionalInputSubTree => false;

            public override bool PropagateNonPositionalInputSubTree => false;

            public AnalysisHost()
            {
                RelativeSizeAxes = Axes.Both;
                AlwaysPresent = true;
                Alpha = 0;

                InternalChildren = new Drawable[]
                {
                    backgroundScreenStack = new BackgroundScreenStack
                    {
                        RelativeSizeAxes = Axes.Both,
                        AlwaysPresent = true,
                        Alpha = 0,
                    },
                    screenStack = new ScreenStack
                    {
                        RelativeSizeAxes = Axes.Both,
                        AlwaysPresent = true,
                        Alpha = 0,
                    }
                };
            }

            protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
            {
                var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
                dependencies.CacheAs(backgroundScreenStack);
                return dependencies;
            }

            public Task<List<HitEvent>?> GenerateAsync(Score score, CancellationToken cancellationToken = default)
            {
                ArgumentNullException.ThrowIfNull(score);

                if (score.Replay == null || score.Replay.Frames.Count == 0)
                    return Task.FromResult<List<HitEvent>?>(null);

                if (tryGetCached(score.ScoreInfo, out var cached))
                    return Task.FromResult<List<HitEvent>?>(cloneHitEvents(cached));

                if (cancellationToken.IsCancellationRequested)
                    return Task.FromCanceled<List<HitEvent>?>(cancellationToken);

                var completionSource = new TaskCompletionSource<List<HitEvent>?>(TaskCreationOptions.RunContinuationsAsynchronously);
                long analysisId = Interlocked.Increment(ref requestId);

                Schedule(() => startAnalysis(analysisId, score.DeepClone(), completionSource));

                if (cancellationToken.CanBeCanceled)
                {
                    cancellationRegistration = cancellationToken.Register(() => Schedule(() => cancelAnalysis(analysisId, completionSource, cancellationToken)));
                }

                return completionSource.Task;
            }

            public void CancelPendingAnalysis()
            {
                long cancellationId = Interlocked.Increment(ref requestId);

                Schedule(() =>
                {
                    if (cancellationId != requestId)
                        return;

                    cancelActiveAnalysis(setTaskCancelled: true);
                });
            }

            private void startAnalysis(long analysisId, Score score, TaskCompletionSource<List<HitEvent>?> completionSource)
            {
                if (completionSource.Task.IsCompleted || analysisId != requestId)
                    return;

                if (LoadState < LoadState.Ready || screenStack.LoadState < LoadState.Ready)
                {
                    Schedule(() => startAnalysis(analysisId, score, completionSource));
                    return;
                }

                cancelActiveAnalysis(setTaskCancelled: false);

                currentTask = completionSource;
                currentPlayer = new AnalysisPlayer(score);
                currentPlayer.AnalysisCompleted += hitEvents => finishAnalysis(analysisId, completionSource, score.ScoreInfo, hitEvents);
                screenStack.Push(currentPlayer);
            }

            private void finishAnalysis(long analysisId, TaskCompletionSource<List<HitEvent>?> completionSource, ScoreInfo scoreInfo, List<HitEvent>? hitEvents)
            {
                if (analysisId != requestId || completionSource.Task.IsCompleted)
                    return;

                cancellationRegistration.Dispose();
                currentTask = null;
                currentPlayer = null;

                if (hitEvents != null && hitEvents.Count > 0)
                    storeCached(scoreInfo, hitEvents);

                completionSource.TrySetResult(hitEvents != null && hitEvents.Count > 0 ? cloneHitEvents(hitEvents) : null);
            }

            private void cancelAnalysis(long analysisId, TaskCompletionSource<List<HitEvent>?> completionSource, CancellationToken cancellationToken)
            {
                if (analysisId != requestId || completionSource.Task.IsCompleted)
                    return;

                cancelActiveAnalysis(setTaskCancelled: false);
                completionSource.TrySetCanceled(cancellationToken);
            }

            private void cancelActiveAnalysis(bool setTaskCancelled)
            {
                cancellationRegistration.Dispose();

                if (currentPlayer != null)
                {
                    currentPlayer.CancelAnalysis();
                    currentPlayer = null;
                }

                if (setTaskCancelled)
                    currentTask?.TrySetCanceled();

                currentTask = null;
            }

            protected override void Dispose(bool isDisposing)
            {
                if (isDisposing)
                    cancelActiveAnalysis(setTaskCancelled: true);

                base.Dispose(isDisposing);
            }
        }

        /// <summary>
        /// 负责按规则集选择分析运行策略。
        /// 这里只允许覆写“如何运行真实 replay 判定链”，不允许覆写判定逻辑本身。
        /// </summary>
        private interface IAnalysisPlayerStrategy
        {
            bool AppliesTo(RulesetInfo? rulesetInfo);

            double GetPlaybackRate(Score score);

            int GetTimeoutMs(Score score, IBeatmap playableBeatmap);

            bool AllowFailure(AnalysisPlayer player);

            double GetCompletionTime(AnalysisPlayer player);

            bool ShouldComplete(AnalysisPlayer player);
        }

        private class DefaultAnalysisPlayerStrategy : IAnalysisPlayerStrategy
        {
            private const double timeout_buffer_ms = 3000;
            private const double min_timeout_ms = 5000;
            private const double max_timeout_ms = 30000;
            private const double completion_safety_time = 100;

            public virtual bool AppliesTo(RulesetInfo? rulesetInfo) => true;

            public virtual double GetPlaybackRate(Score score) => default_analysis_playback_rate;

            public virtual int GetTimeoutMs(Score score, IBeatmap playableBeatmap)
            {
                double playbackRate = Math.Max(1, GetPlaybackRate(score));
                double estimatedRuntime = Math.Max(0, playableBeatmap.GetLastObjectTime()) / playbackRate;

                return (int)Math.Clamp(estimatedRuntime + timeout_buffer_ms, min_timeout_ms, max_timeout_ms);
            }

            public virtual bool AllowFailure(AnalysisPlayer player) => false;

            public virtual double GetCompletionTime(AnalysisPlayer player)
            {
                double lastObjectTime = player.GameplayState.Beatmap.GetLastObjectTime();
                double missWindow = player.FirstMissWindow;
                double replayTailTime = player.LastReplayFrameTime ?? lastObjectTime;

                return Math.Max(lastObjectTime + missWindow, replayTailTime) + completion_safety_time;
            }

            public virtual bool ShouldComplete(AnalysisPlayer player)
            {
                if (player.IsScoreProcessorCompleted)
                    return true;

                if (!player.HasReplayLoaded)
                    return false;

                if (player.IsWaitingOnReplayFrames)
                    return false;

                if (player.CurrentGameplayTime < player.AnalysisCompletionTime)
                    return false;

                return true;
            }
        }

        private sealed class CatchAnalysisPlayerStrategy : DefaultAnalysisPlayerStrategy
        {
            private const string catch_ruleset_short_name = "fruits";
            private const double safe_catch_analysis_playback_rate = 5;

            public override bool AppliesTo(RulesetInfo? rulesetInfo) => rulesetInfo?.ShortName == catch_ruleset_short_name;

            public override double GetPlaybackRate(Score score) => safe_catch_analysis_playback_rate;

            public override bool ShouldComplete(AnalysisPlayer player)
            {
                if (player.IsScoreProcessorCompleted)
                    return true;

                if (!player.HasReplayLoaded)
                    return false;

                return player.CurrentGameplayTime >= player.AnalysisCompletionTime;
            }
        }

        /// <summary>
        /// 一个最小化的隐藏 Player，只负责跑真实 replay 判定并导出 HitEvents。
        /// </summary>
        private sealed partial class AnalysisPlayer : Player
        {
            [Cached(typeof(IGameplayLeaderboardProvider))]
            private readonly EmptyGameplayLeaderboardProvider leaderboardProvider = new EmptyGameplayLeaderboardProvider();

            private readonly Score score;
            private readonly IAnalysisPlayerStrategy strategy;
            private readonly BindableDouble analysisPlaybackRate;
            private readonly BindableDouble analysisMutedVolume = new BindableDouble(0);
            private bool analysisFinished;

            public event Action<List<HitEvent>?>? AnalysisCompleted;

            protected override UserActivity? InitialActivity => null;

            public override bool HideOverlaysOnEnter => false;

            public override bool HideMenuCursorOnNonMouseInput => false;

            public double? LastReplayFrameTime { get; private set; }

            public double AnalysisCompletionTime { get; private set; } = double.PositiveInfinity;

            public double CurrentGameplayTime => GameplayClockContainer?.CurrentTime ?? double.NaN;

            public bool HasReplayLoaded => DrawableRuleset?.HasReplayLoaded.Value == true;

            public bool IsWaitingOnReplayFrames => DrawableRuleset != null && (DrawableRuleset.FrameStableClock.WaitingOnFrames.Value || DrawableRuleset.FrameStableClock.IsCatchingUp.Value);

            public bool IsScoreProcessorCompleted => ScoreProcessor.HasCompleted.Value;

            public double FirstMissWindow => DrawableRuleset?.FirstAvailableHitWindows?.WindowFor(HitResult.Miss) ?? 0;

            public AnalysisPlayer(Score score)
                : base(new PlayerConfiguration
                {
                    AllowPause = false,
                    ShowResults = false,
                    AllowRestart = false,
                    AllowUserInteraction = false,
                    AllowSkipping = false,
                    AutomaticallySkipIntro = false,
                    ShowLeaderboard = false,
                })
            {
                this.score = score;
                strategy = getStrategy(score.ScoreInfo.Ruleset);
                analysisPlaybackRate = new BindableDouble(strategy.GetPlaybackRate(score));
                AlwaysPresent = true;
                Alpha = 0;
            }

            public override void OnEntering(ScreenTransitionEvent e)
            {
                if (!LoadedBeatmapSuccessfully)
                    return;

                applyAnalysisAudioAdjustments();
                ValidForResume = false;

                base.OnEntering(e);
            }

            public override bool OnExiting(ScreenExitEvent e)
            {
                clearAnalysisAdjustments();

                return false;
            }

            protected override bool CheckModsAllowFailure() => strategy.AllowFailure(this);

            protected override Score CreateScore(IBeatmap beatmap) => score;

            protected override ResultsScreen CreateResults(ScoreInfo scoreInfo) => new SoloResultsScreen(scoreInfo);

            protected override void PrepareReplay()
            {
                score.ScoreInfo.HitEvents.Clear();
                DrawableRuleset?.SetReplayScore(score);
                LastReplayFrameTime = score.Replay?.Frames.LastOrDefault()?.Time;
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                if (!LoadedBeatmapSuccessfully)
                {
                    completeAnalysis(null);
                    return;
                }

                AnalysisCompletionTime = strategy.GetCompletionTime(this);

                ScoreProcessor.HasCompleted.BindValueChanged(completed =>
                {
                    if (completed.NewValue)
                        completeAnalysis();
                });
            }

            protected override void Update()
            {
                base.Update();

                if (analysisFinished || !LoadedBeatmapSuccessfully)
                    return;

                if (strategy.ShouldComplete(this))
                    completeAnalysis();
            }

            protected override void StartGameplay()
            {
                // 保留真实 beatmap track 作为 gameplay 时钟来源，只通过变速与静音调整驱动隐藏分析。
                GameplayClockContainer.AdjustmentsFromMods.AddAdjustment(AdjustableProperty.Frequency, analysisPlaybackRate);

                base.StartGameplay();
            }

            protected override void ConcludeFailedScore(Score score)
            {
                base.ConcludeFailedScore(score);
                completeAnalysis();
            }

            public void CancelAnalysis()
            {
                if (analysisFinished)
                    return;

                analysisFinished = true;
                clearAnalysisAdjustments();
                ValidForPush = false;
                ValidForResume = false;

                if (this.IsCurrentScreen())
                    this.Exit();
            }

            private void completeAnalysis(List<HitEvent>? hitEvents = null)
            {
                if (analysisFinished)
                    return;

                analysisFinished = true;
                clearAnalysisAdjustments();

                if (hitEvents == null && LoadedBeatmapSuccessfully && ScoreProcessor.HitEvents.Count > 0)
                    hitEvents = cloneHitEvents(ScoreProcessor.HitEvents);

                AnalysisCompleted?.Invoke(hitEvents);

                if (this.IsCurrentScreen())
                    this.Exit();
            }

            private void applyAnalysisAudioAdjustments()
            {
                GameplayClockContainer.AdjustmentsFromMods.AddAdjustment(AdjustableProperty.Volume, analysisMutedVolume);
                DrawableRuleset?.Audio.AddAdjustment(AdjustableProperty.Volume, analysisMutedVolume);
            }

            private void clearAnalysisAdjustments()
            {
                GameplayClockContainer?.AdjustmentsFromMods.RemoveAdjustment(AdjustableProperty.Frequency, analysisPlaybackRate);
                GameplayClockContainer?.AdjustmentsFromMods.RemoveAdjustment(AdjustableProperty.Volume, analysisMutedVolume);
                DrawableRuleset?.Audio.RemoveAdjustment(AdjustableProperty.Volume, analysisMutedVolume);
            }
        }
    }
}

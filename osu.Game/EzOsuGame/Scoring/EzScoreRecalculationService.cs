// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring.Bonus;
using osu.Game.Scoring;

namespace osu.Game.EzOsuGame.Scoring
{
    /// <summary>
    /// Mania 成绩 Session 重算并写回 Realm；非 Mania 或无 replay 时回退 vanilla <see cref="ScoreManager.Recalculate"/>。
    /// </summary>
    /// <remarks>
    /// <para>Gameplay 结束入库（Player.ImportScore）写入的是 ScoreProcessor 当场统计，<b>不</b>经 Session。</para>
    /// <para>本服务是产品层 Session → Realm 的<b>唯一</b>入口（选歌「重算成绩」）。结算 list / hover 读 Realm <c>StatisticsJson</c>，重算后才与拓展分析 Now 对齐。</para>
    /// </remarks>
    public static class EzScoreRecalculationService
    {
        public static async Task RecalculateAsync(
            ScoreManager scoreManager,
            BeatmapManager beatmapManager,
            IEzReplaySession replaySession,
            ScoreInfo scoreInfo,
            ReplayRunPurpose purpose,
            CancellationToken cancellationToken = default,
            EzAnalysisCache? analysisCache = null)
        {
            if (scoreInfo.Ruleset.OnlineID != 3)
            {
                scoreManager.Recalculate(scoreInfo);
                // var bonus = await CalculateBonusAsync(scoreManager, beatmapManager, replaySession, scoreInfo, cancellationToken, analysisCache).ConfigureAwait(false);
                //
                // if (bonus != null)
                //     EzScoreBonusProvider.Store(scoreInfo, bonus);

                return;
            }

            var databasedScore = scoreManager.GetScore(scoreInfo);

            if (databasedScore?.Replay == null || databasedScore.Replay.Frames.Count == 0)
            {
                scoreManager.Recalculate(scoreInfo);
                return;
            }

            var workingBeatmap = beatmapManager.GetWorkingBeatmap(scoreInfo.BeatmapInfo);

            if (workingBeatmap is DummyWorkingBeatmap)
            {
                scoreManager.Recalculate(scoreInfo);
                return;
            }

            var playableBeatmap = workingBeatmap.GetPlayableBeatmap(scoreInfo.Ruleset, scoreInfo.Mods);

            if (playableBeatmap.HitObjects.Count == 0)
            {
                scoreManager.Recalculate(scoreInfo);
                return;
            }

            var result = await replaySession.RunRequestAsync(
                new ReplayRunRequest(databasedScore.DeepClone(), playableBeatmap, purpose),
                cancellationToken).ConfigureAwait(false);

            if (result.WasCancelled || !result.IsValidReplay || result.Score.ScoreInfo == null)
            {
                scoreManager.Recalculate(scoreInfo);
                return;
            }

            if (EzScoreBonusTracker.IsEnabled)
            {
                IReadOnlyList<double>? cachedKps = null;

                if (analysisCache != null && scoreInfo.BeatmapInfo is BeatmapInfo beatmapInfo)
                {
                    var analysis = await analysisCache.GetAnalysisAsync(beatmapInfo, scoreInfo.Ruleset, scoreInfo.Mods, cancellationToken).ConfigureAwait(false);

                    if (analysis is EzAnalysisResult cached && cached.KpsList.Count > 0)
                        cachedKps = cached.KpsList;
                }

                EzScoreBonusCalculator.Apply(result.Score.ScoreInfo, playableBeatmap, cachedKps, convertedKeyCount(workingBeatmap, scoreInfo, cancellationToken));
                EzScoreBonusProvider.Store(scoreInfo, result.Score.ScoreInfo.EzBonus);
            }

            scoreManager.ApplyEzSessionRecalculation(scoreInfo, result.Score.ScoreInfo, purpose, result.ResolvedEnvironment!);
        }

        /// <summary>
        /// 用钉死的判定档重算一条 mania 成绩并写回 Realm。
        /// </summary>
        /// <remarks>
        /// <para>非标准入口，常规重算必须走 <see cref="RecalculateAsync"/>。</para>
        /// <para>不读任何用户设置，档位由调用方钉死，结果与机器、配置无关。</para>
        /// <para>
        /// 不复用 <see cref="RecalculateAsync"/> 的回退链：对非官方判定语义的成绩（例如迁移中的 38），
        /// 无 replay / 谱面缺失时回落到 <see cref="ScoreManager.Recalculate"/> 等于用官方算法重写 Ez 语义成绩。
        /// 这些情况下本方法直接返回 <see langword="false"/> 且不写 Realm，由调用方下次重试。
        /// </para>
        /// <para>
        /// 走 <see cref="ReplayRunPurpose.ForLive"/>，写回时会把 <paramref name="judgeMode"/> 落进成绩，
        /// 因此同一行重复调用幂等。
        /// </para>
        /// </remarks>
        /// <returns>已重算并写回 Realm 时为 <see langword="true"/>；无法重算时为 <see langword="false"/>。</returns>
        internal static async Task<bool> RecalculateWithPinnedJudgeModeAsync(
            ScoreManager scoreManager,
            BeatmapManager beatmapManager,
            IEzReplaySession replaySession,
            ScoreInfo scoreInfo,
            EzEnumJudgeMode judgeMode,
            CancellationToken cancellationToken = default)
        {
            if (scoreInfo.Ruleset.OnlineID != 3)
                return false;

            var databasedScore = scoreManager.GetScore(scoreInfo);

            if (databasedScore?.Replay == null || databasedScore.Replay.Frames.Count == 0)
                return false;

            var workingBeatmap = beatmapManager.GetWorkingBeatmap(scoreInfo.BeatmapInfo);

            if (workingBeatmap is DummyWorkingBeatmap)
                return false;

            var playableBeatmap = workingBeatmap.GetPlayableBeatmap(scoreInfo.Ruleset, scoreInfo.Mods);

            if (playableBeatmap.HitObjects.Count == 0)
                return false;

            var result = await replaySession.RunRequestAsync(
                new ReplayRunRequest(databasedScore.DeepClone(), playableBeatmap, ReplayRunPurpose.ForLive)
                {
                    EnvironmentOverride = GameplayEnvironment.ForMigration(judgeMode)
                },
                cancellationToken).ConfigureAwait(false);

            if (result.WasCancelled || !result.IsValidReplay || result.Score.ScoreInfo == null || result.ResolvedEnvironment == null)
                return false;

            scoreManager.ApplyEzSessionRecalculation(scoreInfo, result.Score.ScoreInfo, ReplayRunPurpose.ForLive, result.ResolvedEnvironment);
            return true;
        }

        /// <summary>
        /// 回放重跑（ForStored）只为得到附加分；不写 Realm，原分 / 判定统计不变。
        /// </summary>
        /// <returns><see langword="null"/>：无回放或谱面不可用。</returns>
        public static async Task<EzScoreBonusSet?> CalculateBonusAsync(
            ScoreManager scoreManager,
            BeatmapManager beatmapManager,
            IEzReplaySession replaySession,
            ScoreInfo scoreInfo,
            CancellationToken cancellationToken = default,
            EzAnalysisCache? analysisCache = null)
        {
            if (!EzScoreBonusTracker.IsEnabled)
                return null;

            var databasedScore = scoreManager.GetScore(scoreInfo);

            if (databasedScore?.Replay == null || databasedScore.Replay.Frames.Count == 0)
                return null;

            var workingBeatmap = beatmapManager.GetWorkingBeatmap(scoreInfo.BeatmapInfo);

            if (workingBeatmap is DummyWorkingBeatmap)
                return null;

            var playableBeatmap = workingBeatmap.GetPlayableBeatmap(scoreInfo.Ruleset, scoreInfo.Mods);

            if (playableBeatmap.HitObjects.Count == 0)
                return null;

            var result = await replaySession.RunRequestAsync(
                new ReplayRunRequest(databasedScore.DeepClone(), playableBeatmap, ReplayRunPurpose.ForStored),
                cancellationToken).ConfigureAwait(false);

            if (result.WasCancelled)
                throw new OperationCanceledException(cancellationToken);

            if (!result.IsValidReplay || result.Score.ScoreInfo == null)
                return null;

            IReadOnlyList<double>? cachedKps = null;

            if (analysisCache != null && scoreInfo.BeatmapInfo is BeatmapInfo beatmapInfo)
            {
                var analysis = await analysisCache.GetAnalysisAsync(beatmapInfo, scoreInfo.Ruleset, scoreInfo.Mods, cancellationToken).ConfigureAwait(false);

                if (analysis is EzAnalysisResult cached && cached.KpsList.Count > 0)
                    cachedKps = cached.KpsList;
            }

            var sessionInfo = result.Score.ScoreInfo;
            EzScoreBonusCalculator.Apply(sessionInfo, playableBeatmap, cachedKps, convertedKeyCount(workingBeatmap, scoreInfo, cancellationToken));
            return sessionInfo.EzBonus;
        }

        private static int convertedKeyCount(IWorkingBeatmap working, ScoreInfo scoreInfo, CancellationToken cancellationToken)
        {
            IBeatmap converted = EzPlayableBeatmapCache.GetShared(working, scoreInfo.Ruleset, scoreInfo.Mods, cancellationToken);
            return EzScoreBonusCalculator.KeyCountOf(converted);
        }
    }
}

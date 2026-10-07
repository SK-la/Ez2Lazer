// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Analysis;
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

            IReadOnlyList<double>? cachedKps = null;

            if (analysisCache != null && scoreInfo.BeatmapInfo is BeatmapInfo beatmapInfo)
            {
                var analysis = await analysisCache.GetAnalysisAsync(beatmapInfo, scoreInfo.Ruleset, scoreInfo.Mods, cancellationToken).ConfigureAwait(false);

                if (analysis is EzAnalysisResult cached && cached.KpsList.Count > 0)
                    cachedKps = cached.KpsList;
            }

            EzScoreBonusCalculator.Apply(result.Score.ScoreInfo, playableBeatmap, cachedKps);
            scoreManager.ApplyEzSessionRecalculation(scoreInfo, result.Score.ScoreInfo, purpose, result.ResolvedEnvironment!);
            EzScoreBonusProvider.Store(scoreInfo, result.Score.ScoreInfo.EzBonus);
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
            EzScoreBonusCalculator.Apply(sessionInfo, playableBeatmap, cachedKps);
            return sessionInfo.EzBonus;
        }
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets.Mania.EzMania.Diagnostics;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Mania.EzMania.ReplayJudge
{
    /// <summary>
    /// Mania 统一 replay 入口：负责 async、环境解析与共享 cache。
    /// Panel / Graph / Race 通过此接口获取 replay 判定结果，禁止自建解析或缓存。
    /// </summary>
    public sealed class ManiaReplaySessionService : EzReplaySession
    {
        private ManiaSimulationBeatmapProvider? beatmapProvider;

        /// <summary>
        /// 注入 beatmap 来源：之后每次仿真都在 <see cref="ManiaSimulationBeatmapProvider"/> 产出的独立副本上绑定 hitmode，
        /// 调用方传进来的实例（可能是本局 live 实例，或被多个 ghost 共享的 Race 实例）不再被改写。
        /// </summary>
        /// <remarks>未注入时（测试、无 BeatmapManager 的宿主）沿用调用方实例——那时调用方自己拥有该实例。</remarks>
        public override void AttachBeatmaps(IWorkingBeatmapCache beatmapCache)
            => beatmapProvider = new ManiaSimulationBeatmapProvider(beatmapCache);

        protected override (Score Score, EzScoreTimeline Timeline) RunWithTimeline(
            Score score, IBeatmap beatmap, IGameplayEnvironment environment, CancellationToken cancellationToken)
            => ManiaReplaySession.RunWithTimeline(score, resolveSimulationBeatmap(score, beatmap, environment, cancellationToken), environment, cancellationToken);

        private IBeatmap resolveSimulationBeatmap(Score score, IBeatmap beatmap, IGameplayEnvironment environment, CancellationToken cancellationToken)
        {
            if (beatmapProvider == null)
                return beatmap;

            try
            {
                return beatmapProvider.TryCreate(score, environment, cancellationToken) ?? beatmap;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                ManiaJudgeHotPathTrace.RecordSimulationBeatmapFallback();

                string message = $"[ManiaJudgeBinding] failed to derive an isolated beatmap for score {score.ScoreInfo?.ID}; "
                                 + $"falling back to the caller instance ({e.GetType().Name}: {e.Message})";

                Logger.Log(message, Ez2ConfigManager.LOGGER_NAME, LogLevel.Important);

                // 调用方刚刚转换出同一张谱面，这里再转失败只可能是实现问题：DEBUG 直接暴露。
#if DEBUG
                throw new InvalidOperationException(message, e);
#else
                return beatmap;
#endif
            }
        }
    }
}

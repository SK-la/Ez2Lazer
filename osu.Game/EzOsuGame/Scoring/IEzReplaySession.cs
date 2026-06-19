// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading;
using System.Threading.Tasks;
using osu.Game.Beatmaps;
using osu.Game.Scoring;

namespace osu.Game.EzOsuGame.Scoring
{
    /// <summary>
    /// 统一 Replay Session 入口：负责 async、环境解析与共享 cache。
    /// Panel / Graph / Race 通过此接口获取 replay 判定结果，禁止自建解析或缓存。
    /// </summary>
    public interface IEzReplaySession
    {
        /// <summary>
        /// 运行 replay 判定并回填 Score（HitEvents、Statistics 等）。
        /// </summary>
        Task<Score> RunAsync(Score score, IBeatmap beatmap, IGameplayEnvironment environment, CancellationToken cancellationToken = default);

        /// <summary>
        /// 运行 replay 判定并构建时间线（用于角逐 ghost）。
        /// </summary>
        Task<EzScoreTimeline> RunTimelineAsync(Score score, IBeatmap beatmap, IGameplayEnvironment environment, CancellationToken cancellationToken = default);

        /// <summary>
        /// 统一请求入口：支持更复杂的场景（如同时需要 Score + Timeline）。
        /// </summary>
        Task<ReplayRunResult> RunRequestAsync(ReplayRunRequest request, CancellationToken cancellationToken = default);
    }
}



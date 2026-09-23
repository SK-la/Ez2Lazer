// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.EzOsuGame.Scoring
{
    /// <summary>
    /// 统一 Replay Session 入口：负责 async、环境解析与共享 cache。
    /// 进程内由 <see cref="EzReplaySessionRouter"/> 按 <see cref="ScoreInfo.Ruleset"/> 分发；Panel / Graph / Race 注入本接口即可。
    /// <para>环境由 Session 按 <see cref="ReplayRunPurpose"/> 经 <see cref="Configuration.Ez2ConfigManager.ResolveEnvironment"/> 解析。</para>
    /// </summary>
    public interface IEzReplaySession
    {
        /// <summary>
        /// 运行 replay 判定并回填 Score（HitEvents、Statistics 等）。
        /// </summary>
        Task<Score> RunAsync(Score score, IBeatmap beatmap, ReplayRunPurpose purpose, CancellationToken cancellationToken = default);

        /// <summary>
        /// 运行 replay 判定并构建时间线（用于角逐 ghost）。
        /// </summary>
        Task<EzScoreTimeline> RunTimelineAsync(Score score, IBeatmap beatmap, ReplayRunPurpose purpose, CancellationToken cancellationToken = default);

        /// <summary>
        /// 构建时间线，不经 Service 层 TimelineCache（<see cref="EzScoreTimelineBuilder"/> 角逐热路径）。
        /// </summary>
        Task<EzScoreTimeline> RunTimelineDirectAsync(Score score, IBeatmap beatmap, ReplayRunPurpose purpose, CancellationToken cancellationToken = default);

        /// <summary>
        /// <see cref="RunTimelineDirectAsync"/> 的同步版本：直接在当前线程运行判定，不额外开 <c>Task.Run</c>。
        /// 供已在后台线程串行构建的角逐热路径调用，避免「嵌套 Task.Run + 同步阻塞」占用两个线程池线程。
        /// </summary>
        EzScoreTimeline RunTimelineDirect(Score score, IBeatmap beatmap, ReplayRunPurpose purpose, CancellationToken cancellationToken = default);

        /// <summary>
        /// 统一请求入口：支持更复杂的场景（如同时需要 Score + Timeline）。
        /// </summary>
        Task<ReplayRunResult> RunRequestAsync(ReplayRunRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// 从 replay 生成 HitEvents（与对应 Purpose 的 <see cref="RunAsync"/> 共 cache）。
        /// StatisticsPanel 补空 HitEvents 使用 <see cref="ReplayRunPurpose.ForStored"/>；Graph Now 使用 ForLive。
        /// 规则集没有 Session 时返回空列表。
        /// </summary>
        Task<List<HitEvent>> RunHitEventsAsync(Score score, IBeatmap beatmap, ReplayRunPurpose purpose = ReplayRunPurpose.ForLive, CancellationToken cancellationToken = default);

        /// <summary>
        /// 注入 beatmap 来源，供需要「仿真副本」的规则集在判定前取独立实例。
        /// 不隔离的规则集实现为空操作。
        /// </summary>
        void AttachBeatmaps(IWorkingBeatmapCache beatmapCache);
    }
}

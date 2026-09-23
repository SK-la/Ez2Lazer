// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.EzOsuGame.Scoring
{
    /// <summary>
    /// 进程内 <see cref="IEzReplaySession"/> 门面：按 <see cref="ScoreInfo.Ruleset"/> 分发到各 ruleset 的 Session 单例。
    /// 无 Session 的规则集在 <see cref="RunHitEventsAsync"/> 返回 null。
    /// </summary>
    public sealed class EzReplaySessionRouter : IEzReplaySession
    {
        private readonly Dictionary<int, IEzReplaySession> sessionsByOnlineId = new Dictionary<int, IEzReplaySession>();

        public EzReplaySessionRouter(IEnumerable<RulesetInfo> availableRulesets, IWorkingBeatmapCache? beatmapCache = null)
        {
            foreach (var rulesetInfo in availableRulesets)
            {
                var session = rulesetInfo.CreateInstance().CreateEzReplaySession();

                if (session != null)
                    sessionsByOnlineId[rulesetInfo.OnlineID] = session;
            }

            // 无 beatmap 来源（测试）时 session 沿用调用方提供的 beatmap。
            if (beatmapCache != null)
                AttachBeatmaps(beatmapCache);
        }

        public void AttachBeatmaps(IWorkingBeatmapCache beatmapCache)
        {
            foreach (var session in sessionsByOnlineId.Values)
                session.AttachBeatmaps(beatmapCache);
        }

        public Task<Score> RunAsync(Score score, IBeatmap beatmap, ReplayRunPurpose purpose, CancellationToken cancellationToken = default)
            => requireSession(score).RunAsync(score, beatmap, purpose, cancellationToken);

        public Task<EzScoreTimeline> RunTimelineAsync(Score score, IBeatmap beatmap, ReplayRunPurpose purpose, CancellationToken cancellationToken = default)
            => requireSession(score).RunTimelineAsync(score, beatmap, purpose, cancellationToken);

        public Task<EzScoreTimeline> RunTimelineDirectAsync(Score score, IBeatmap beatmap, ReplayRunPurpose purpose, CancellationToken cancellationToken = default)
            => requireSession(score).RunTimelineDirectAsync(score, beatmap, purpose, cancellationToken);

        public EzScoreTimeline RunTimelineDirect(Score score, IBeatmap beatmap, ReplayRunPurpose purpose, CancellationToken cancellationToken = default)
            => requireSession(score).RunTimelineDirect(score, beatmap, purpose, cancellationToken);

        public Task<ReplayRunResult> RunRequestAsync(ReplayRunRequest request, CancellationToken cancellationToken = default)
        {
            var session = tryResolve(request.Score);

            if (session == null)
                return Task.FromResult(ReplayRunResult.InvalidReplay(request.Score));

            return session.RunRequestAsync(request, cancellationToken);
        }

        public Task<List<HitEvent>> RunHitEventsAsync(Score score, IBeatmap beatmap, ReplayRunPurpose purpose = ReplayRunPurpose.ForLive, CancellationToken cancellationToken = default)
        {
            var session = tryResolve(score);

            if (session == null)
                return Task.FromResult(new List<HitEvent>());

            return session.RunHitEventsAsync(score, beatmap, purpose, cancellationToken);
        }

        private IEzReplaySession requireSession(Score score)
        {
            var session = tryResolve(score);

            if (session == null)
                throw new InvalidOperationException($"No replay session registered for ruleset '{score.ScoreInfo.Ruleset.ShortName}'.");

            return session;
        }

        private IEzReplaySession? tryResolve(Score score) => tryResolve(score.ScoreInfo.Ruleset);

        private IEzReplaySession? tryResolve(RulesetInfo ruleset)
        {
            if (ruleset == null)
                return null;

            sessionsByOnlineId.TryGetValue(ruleset.OnlineID, out var session);
            return session;
        }
    }
}

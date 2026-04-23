// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Statistics;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Utils;
using osuTK;

namespace osu.Game.Rulesets.Osu.EzOsu.Statistics
{
    /// <summary>
    /// Osu 规则集的分数命中事件生成器实现。
    /// 通过分析回放帧和谱面对象计算命中判定。
    /// </summary>
    public sealed class OsuScoreHitEventGenerator : IScoreHitEventGenerator
    {
        private readonly struct ReplayPressEvent
        {
            public readonly double Time;
            public readonly Vector2 Position;

            public ReplayPressEvent(double time, Vector2 position)
            {
                Time = time;
                Position = position;
            }
        }

        /// <summary>
        /// 生成器的单例实例。
        /// </summary>
        public static OsuScoreHitEventGenerator Instance { get; } = new OsuScoreHitEventGenerator();

        /// <summary>
        /// 静态构造函数在类首次使用时自动执行，注册此生成器。
        /// </summary>
        static OsuScoreHitEventGenerator()
        {
            EzScoreReloadBridge.RegisterImplementation("osu", Instance);
            EzScoreReloadBridge.RegisterImplementation("0", Instance);
        }

        /// <summary>
        /// 验证该回放是否对此规则集有效。
        /// 这个方法会在 Generate() 之前被调用。
        /// 用于快速检查回放格式、帧数据完整性等。
        /// </summary>
        /// <param name="score">要验证的分数</param>
        /// <returns>如果回放有效则返回 true，否则返回 false</returns>
        public bool Validate(Score score)
        {
            if (score.ScoreInfo.Ruleset.OnlineID != 0)
                return false;

            var replay = score.Replay;

            if (replay == null || replay.Frames.Count == 0)
                return false;

            // 只需要至少有一些 OsuReplayFrame，不需要全部都是
            int osuFrameCount = replay.Frames.OfType<OsuReplayFrame>().Count();
            return osuFrameCount > 0;
        }

        /// <summary>
        /// 为分数生成命中事件列表。
        /// 通过分析回放帧和谱面对象计算命中判定。
        /// </summary>
        /// <param name="score">要处理的分数</param>
        /// <param name="playableBeatmap">与分数关联的可玩谱面</param>
        /// <param name="cancellationToken">用于停止生成的取消令牌</param>
        /// <returns>生成的命中事件列表，若无法生成则返回 null</returns>
        public List<HitEvent>? Generate(Score score, IBeatmap playableBeatmap, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var osuFrames = score.Replay.Frames.OfType<OsuReplayFrame>().OrderBy(f => f.Time).ToList();

            if (osuFrames.Count == 0)
                return null;

            var targets = collectJudgementTargets(playableBeatmap, cancellationToken);

            if (targets.Count == 0)
                return null;

            var replayPresses = collectPressEvents(osuFrames);
            bool[] pressConsumed = new bool[replayPresses.Count];

            var hitEvents = new List<HitEvent>(targets.Count);
            HitObject? lastHitObject = null;
            double gameplayRate = ModUtils.CalculateRateWithMods(score.ScoreInfo.Mods);
            int firstRelevantPressIndex = 0;

            foreach (var target in targets)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (target is not OsuHitObject osuTarget || target.HitWindows == null || ReferenceEquals(target.HitWindows, HitWindows.Empty))
                    continue;

                double targetTime = target.StartTime;
                double missWindow = target.HitWindows.WindowFor(HitResult.Miss);

                while (firstRelevantPressIndex < replayPresses.Count && replayPresses[firstRelevantPressIndex].Time < targetTime - missWindow)
                    firstRelevantPressIndex++;

                int matchedPressIndex = -1;

                for (int i = firstRelevantPressIndex; i < replayPresses.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (pressConsumed[i])
                        continue;

                    ReplayPressEvent press = replayPresses[i];

                    if (press.Time > targetTime + missWindow)
                        break;

                    if (Vector2.Distance(press.Position, osuTarget.StackedPosition) > osuTarget.Radius)
                        continue;

                    matchedPressIndex = i;
                    break;
                }

                double timeOffsetForJudgement = 0;
                HitResult result = HitResult.Miss;
                Vector2? hitPosition = null;

                if (matchedPressIndex >= 0)
                {
                    pressConsumed[matchedPressIndex] = true;

                    ReplayPressEvent press = replayPresses[matchedPressIndex];
                    timeOffsetForJudgement = press.Time - targetTime;
                    result = target.HitWindows.ResultFor(timeOffsetForJudgement);

                    if (result == HitResult.None)
                        result = HitResult.Miss;

                    hitPosition = press.Position;
                }

                hitEvents.Add(new HitEvent(timeOffsetForJudgement, gameplayRate, result, target, lastHitObject, hitPosition));
                lastHitObject = target;
            }

            return hitEvents.Count > 0 ? hitEvents : null;
        }

        /// <summary>
        /// 收集需要判定的所有目标对象（Circle、Slider、Spinner）。
        /// </summary>
        private static List<HitObject> collectJudgementTargets(IBeatmap beatmap, CancellationToken cancellationToken)
        {
            var targets = new List<HitObject>();

            foreach (var hitObject in beatmap.HitObjects)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // 只添加有判定窗口的对象
                if (hitObject.HitWindows == null || ReferenceEquals(hitObject.HitWindows, HitWindows.Empty))
                    continue;

                if (hitObject.Judgement.MaxResult == HitResult.IgnoreHit)
                    continue;

                // 添加主对象
                targets.Add(hitObject);

                // 递归添加嵌套对象（如 Slider Ticks）
                collectNestedJudgementTargets(hitObject, targets, cancellationToken);
            }

            return targets.OrderBy(h => h.StartTime).ToList();
        }

        /// <summary>
        /// 递归收集嵌套的判定对象。
        /// </summary>
        private static void collectNestedJudgementTargets(HitObject hitObject, List<HitObject> targets, CancellationToken cancellationToken)
        {
            foreach (var nested in hitObject.NestedHitObjects)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (nested.HitWindows != null && !ReferenceEquals(nested.HitWindows, HitWindows.Empty) && nested.Judgement.MaxResult != HitResult.IgnoreHit)
                {
                    targets.Add(nested);
                }

                collectNestedJudgementTargets(nested, targets, cancellationToken);
            }
        }

        private static List<ReplayPressEvent> collectPressEvents(List<OsuReplayFrame> frames)
        {
            var presses = new List<ReplayPressEvent>();
            var previousActions = new HashSet<OsuAction>();

            foreach (var frame in frames)
            {
                var currentActions = new HashSet<OsuAction>(frame.Actions.Where(isPressAction));

                foreach (var action in currentActions)
                {
                    if (previousActions.Contains(action))
                        continue;

                    presses.Add(new ReplayPressEvent(frame.Time, frame.Position));
                }

                previousActions = currentActions;
            }

            return presses;
        }

        private static bool isPressAction(OsuAction action) => action == OsuAction.LeftButton || action == OsuAction.RightButton;
    }
}

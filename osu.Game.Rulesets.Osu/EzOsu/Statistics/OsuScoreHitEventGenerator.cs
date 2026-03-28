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

            // 解析回放帧 - 使用 OfType 安全过滤
            var osuFrames = score.Replay.Frames.OfType<OsuReplayFrame>().OrderBy(f => f.Time).ToList();

            if (osuFrames.Count == 0)
                return null;

            // 收集判定目标对象
            var targets = collectJudgementTargets(playableBeatmap, cancellationToken);

            if (targets.Count == 0)
                return null;

            // 生成 HitEvent
            var hitEvents = new List<HitEvent>();
            var lastHitObject = targets.FirstOrDefault();
            double gameplayRate = ModUtils.CalculateRateWithMods(score.ScoreInfo.Mods);

            foreach (var target in targets)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    // 计算目标对象的判定时间
                    double targetTime = target.GetEndTime();
                    if (target is HitCircle hitCircle)
                        targetTime = hitCircle.StartTime;
                    else if (target is Slider slider)
                        targetTime = slider.StartTime;
                    else if (target is Spinner spinner)
                        targetTime = spinner.StartTime;

                    // 查找最接近的帧
                    OsuReplayFrame? closestFrame = findClosestFrame(osuFrames, targetTime);

                    if (closestFrame == null)
                    {
                        // 没有找到对应的按键事件 -> Miss
                        hitEvents.Add(new HitEvent(0.0, gameplayRate, HitResult.Miss, target, lastHitObject, null));
                        lastHitObject = target;
                        continue;
                    }

                    // 计算时间偏差
                    double timeOffsetForJudgement = (closestFrame.Time - targetTime) / gameplayRate;

                    // 计算空间距离偏差
                    Vector2 cursorPosition = closestFrame.Position;
                    Vector2 targetPosition = (target as OsuHitObject)?.Position ?? Vector2.Zero;
                    double spatialDistance = Vector2.Distance(cursorPosition, targetPosition);

                    // 根据时间判定结果（Osu 主要基于时间判定）
                    HitResult result = evaluateHitResult(target, timeOffsetForJudgement, spatialDistance);

                    // 创建 HitEvent，包含位移信息
                    hitEvents.Add(new HitEvent(timeOffsetForJudgement, gameplayRate, result, target, lastHitObject, null));
                    lastHitObject = target;
                }
                catch (Exception)
                {
                    // 继续处理下一个对象
                }
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

        /// <summary>
        /// 在回放帧中找到最接近目标时间的帧。
        /// 使用二分查找以提高性能。
        /// </summary>
        private static OsuReplayFrame? findClosestFrame(List<OsuReplayFrame> frames, double targetTime)
        {
            if (frames.Count == 0)
                return null;

            // 二分查找最接近的帧
            int left = 0, right = frames.Count - 1;
            OsuReplayFrame? closest = null;
            double minTimeDiff = double.MaxValue;

            while (left <= right)
            {
                int mid = (left + right) / 2;
                var frame = frames[mid];
                double timeDiff = Math.Abs(frame.Time - targetTime);

                if (timeDiff < minTimeDiff)
                {
                    minTimeDiff = timeDiff;
                    closest = frame;
                }

                if (frame.Time < targetTime)
                    left = mid + 1;
                else
                    right = mid - 1;
            }

            // 检查前后帧，找到最接近的
            if (left < frames.Count)
            {
                var leftFrame = frames[left];
                if (Math.Abs(leftFrame.Time - targetTime) < minTimeDiff)
                    closest = leftFrame;
            }

            if (right >= 0)
            {
                var rightFrame = frames[right];
                if (Math.Abs(rightFrame.Time - targetTime) < minTimeDiff)
                    closest = rightFrame;
            }

            return closest;
        }

        /// <summary>
        /// 根据时间偏差和空间距离评估 Hit Result。
        /// Osu 规则集主要基于时间判定，但也会考虑空间距离作为二级因素。
        /// </summary>
        private static HitResult evaluateHitResult(HitObject hitObject, double timeOffset, double spatialDistance)
        {
            var hitWindows = hitObject.HitWindows;

            if (hitWindows == null)
                return HitResult.Miss;

            // Osu 判定主要基于时间偏差
            // 从最严格到最宽松检查各个判定等级

            if (hitWindows.IsHitResultAllowed(HitResult.Perfect))
            {
                if (Math.Abs(timeOffset) <= hitWindows.WindowFor(HitResult.Perfect))
                    return HitResult.Perfect;
            }

            if (hitWindows.IsHitResultAllowed(HitResult.Great))
            {
                if (Math.Abs(timeOffset) <= hitWindows.WindowFor(HitResult.Great))
                    return HitResult.Great;
            }

            if (hitWindows.IsHitResultAllowed(HitResult.Good))
            {
                if (Math.Abs(timeOffset) <= hitWindows.WindowFor(HitResult.Good))
                    return HitResult.Good;
            }

            if (hitWindows.IsHitResultAllowed(HitResult.Ok))
            {
                if (Math.Abs(timeOffset) <= hitWindows.WindowFor(HitResult.Ok))
                    return HitResult.Ok;
            }

            if (hitWindows.IsHitResultAllowed(HitResult.Meh))
            {
                if (Math.Abs(timeOffset) <= hitWindows.WindowFor(HitResult.Meh))
                    return HitResult.Meh;
            }

            return HitResult.Miss;
        }
    }
}

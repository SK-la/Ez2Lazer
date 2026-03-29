// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Utils;
using osu.Game.Replays.Legacy;
using osu.Game.Rulesets.Objects.Types;
using osuTK;

namespace osu.Game.EzOsuGame.Statistics
{
    /// <summary>
    /// 简单的命中事件生成器：仅使用谱面顶层 HitObject（不包含嵌套的 ticks / tail / hold 等）
    /// 来匹配回放帧并生成 <see cref="HitEvent"/> 列表。
    /// 目的：不对 slider ticks、spinner、hold 等嵌套对象做任何特殊处理或判定逻辑。
    /// </summary>
    public static partial class EzScoreServer
    {
        /// <summary>
        /// 从给定分数与可玩谱面中，基于顶层 HitObject 与回放帧匹配，生成命中事件列表。
        /// 不会访问或包含任何 <see cref="HitObject.NestedHitObjects"/> 中的对象。
        /// </summary>
        public static List<HitEvent>? TryGenerate(Score score, IBeatmap playableBeatmap, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(score);
            ArgumentNullException.ThrowIfNull(playableBeatmap);

            var replay = score.Replay;
            if (replay == null || replay.Frames.Count == 0)
                return null;

            // 使用按时间排序的帧列表（不做类型假定，也不读取位置）
            var frames = replay.Frames.OrderBy(f => f.Time).ToList();
            if (frames.Count == 0)
                return null;

            // 尝试使用分数中的 ruleset ID 反射并创建对应规则集实例，使用其 BeatmapConverter 对谱面进行转换。
            // 转换成功时优先使用转换后对象的 HitWindows 来进行判定（以保证与真实模式判定一致）。
            // 若转换失败或不可用，则回退到直接对顶层对象调用 ApplyDefaults 的行为。
            List<HitObject>? convertedObjects = null;

            try
            {
                var rulesetInfo = score.ScoreInfo?.Ruleset;

                if (rulesetInfo != null && rulesetInfo.Available)
                {
                    try
                    {
                        var rulesetInstance = rulesetInfo.CreateInstance();
                        var converter = rulesetInstance.CreateBeatmapConverter(playableBeatmap);

                        if (converter.CanConvert())
                        {
                            var convertedBeatmap = converter.Convert(cancellationToken);
                            convertedObjects = convertedBeatmap.HitObjects.OrderBy(h => h.StartTime).ToList();
                        }
                    }
                    catch
                    {
                        // 若无法加载/实例化规则集或转换失败，默默回退到原生行为（不抛出）
                    }
                }
            }
            catch
            {
                // 守住所有反射/创建异常，确保不会影响主流程
            }

            // 仅收集顶层判定对象（不包含 nestedHitObjects）
            // 不主动依赖 HitObject.HitWindows 属性，判定时优先尝试使用规则集转换后对象的 HitWindows
            var targets = playableBeatmap.HitObjects
                                         .Where(h => h.Judgement.MaxResult != HitResult.IgnoreHit)
                                         .OrderBy(h => h.StartTime)
                                         .ToList();

            if (targets.Count == 0)
                return null;

            var hitEvents = new List<HitEvent>(targets.Count);
            HitObject? lastHitObject = targets.FirstOrDefault();
            double gameplayRate = ModUtils.CalculateRateWithMods(score.ScoreInfo.Mods);

            foreach (var target in targets)
            {
                cancellationToken.ThrowIfCancellationRequested();

                double targetTime = target.GetEndTime();

                var closest = findClosestFrame(frames, targetTime);

                if (closest == null)
                {
                    // 没有匹配到任何帧 -> 视为 Miss
                    hitEvents.Add(new HitEvent(0.0, gameplayRate, HitResult.Miss, target, lastHitObject, null));
                    lastHitObject = target;
                    continue;
                }

                // 优先尝试在回放帧中查找实际的按键按下帧（录制器会在按键事件触发时写入），
                // 以避免仅依赖固定帧率采样（例如 60fps）带来的时间量化（≈±8ms）问题。
                var pressFrame = findClosestPressFrame(frames, targetTime, 500);
                if (pressFrame != null)
                    closest = pressFrame;

                // 使用所选帧的时间差作为 TimeOffset（单位：毫秒）
                double timeOffset = closest.Time - targetTime;

                // 优先尝试从规则集转换后的对象中获取 HitWindows（以保证判定与模式一致）
                HitWindows? hitWindows = null;

                if (convertedObjects != null && convertedObjects.Count > 0)
                {
                    const double time_epsilon = 0.001; // ms

                    // 以 StartTime 为主键进行匹配（转换器通常保留事件时间）。
                    var candidates = convertedObjects.Where(c => Math.Abs(c.StartTime - target.StartTime) < time_epsilon).ToList();

                    // 若未找到严格匹配，尝试按最接近时间的单个对象作为候选（阈值较宽松，避免极端错误匹配）。
                    if (candidates.Count == 0)
                    {
                        var nearest = convertedObjects.OrderBy(c => Math.Abs(c.StartTime - target.StartTime)).FirstOrDefault();
                        if (nearest != null && Math.Abs(nearest.StartTime - target.StartTime) <= 5) // 5ms 容忍度
                            candidates.Add(nearest);
                    }

                    foreach (var c in candidates)
                    {
                        c.ApplyDefaults(playableBeatmap.ControlPointInfo, playableBeatmap.Difficulty, cancellationToken);

                        if (c.HitWindows != null)
                        {
                            hitWindows = c.HitWindows;
                            break;
                        }
                    }
                }

                // 未能通过转换对象获取到 HitWindows，则回退到对原对象调用 ApplyDefaults
                if (hitWindows == null)
                {
                    target.ApplyDefaults(playableBeatmap.ControlPointInfo, playableBeatmap.Difficulty, cancellationToken);
                    hitWindows = target.HitWindows;
                }

                var result = hitWindows.ResultFor(timeOffset);

                // 优先使用 HitObject 的 IHas 接口获取坐标（避免反射）
                Vector2? position = null;

                if (target is IHasPosition hp)
                    position = hp.Position;
                else if (target is IHasXPosition hx)
                {
                    float x = hx.X;
                    float y = target is IHasYPosition hy ? hy.Y : 0f;
                    position = new Vector2(x, y);
                }

                // 若 HitObject 无坐标，再尝试从已知回放帧类型读取（显式类型判断，避免反射）
                if (position == null)
                {
                    if (closest is LegacyReplayFrame legacy)
                        position = legacy.Position;
                }

                hitEvents.Add(new HitEvent(timeOffset, gameplayRate, result, target, lastHitObject, position));
                lastHitObject = target;
            }

            return hitEvents.Count > 0 ? hitEvents : null;
        }

        private static ReplayFrame? findClosestFrame(IList<ReplayFrame> frames, double targetTime)
        {
            if (frames.Count == 0)
                return null;

            int left = 0, right = frames.Count - 1;
            ReplayFrame? closest = null;
            double minDiff = double.MaxValue;

            while (left <= right)
            {
                int mid = (left + right) / 2;
                var frame = frames[mid];
                double diff = Math.Abs(frame.Time - targetTime);

                if (diff < minDiff)
                {
                    minDiff = diff;
                    closest = frame;
                }

                if (frame.Time < targetTime)
                    left = mid + 1;
                else
                    right = mid - 1;
            }

            if (left < frames.Count)
            {
                var lf = frames[left];
                if (Math.Abs(lf.Time - targetTime) < minDiff)
                    closest = lf;
            }

            if (right >= 0)
            {
                var rf = frames[right];
                if (Math.Abs(rf.Time - targetTime) < minDiff)
                    closest = rf;
            }

            return closest;
        }

        private static ReplayFrame? findClosestPressFrame(IList<ReplayFrame> frames, double targetTime, double window = 500)
        {
            if (frames.Count == 0)
                return null;

            ReplayFrame? best = null;
            double bestDiff = double.MaxValue;

            for (int i = 0; i < frames.Count; i++)
            {
                if (frames[i] is not LegacyReplayFrame curr)
                    continue;

                bool isPress = false;

                if (i == 0)
                {
                    // 若首帧即为按下状态，则视为一次按下事件（保守处理）
                    if (curr.MouseLeft)
                        isPress = true;
                }
                else if (frames[i - 1] is LegacyReplayFrame prev)
                {
                    // 检测从未按下到按下的上升沿
                    if (curr.MouseLeft && !prev.MouseLeft)
                        isPress = true;
                }

                if (!isPress)
                    continue;

                double diff = Math.Abs(curr.Time - targetTime);
                if (diff <= window && diff < bestDiff)
                {
                    bestDiff = diff;
                    best = curr;
                }
            }

            return best;
        }
    }
}

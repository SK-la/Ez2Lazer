// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.EzOsu.ReplayJudge.Judgement;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Scoring;
using osuTK;

namespace osu.Game.Rulesets.Osu.EzOsu.ReplayJudge.Session
{
    /// <summary>
    /// 待判定目标队列：顶层 HitCircle；Slider/Spinner 由 Shadow 状态机处理。
    /// </summary>
    internal sealed class OsuSessionCircleScheduler
    {
        internal sealed class PendingTarget
        {
            public required HitObject HitObject { get; init; }
            public required OsuHitObject OsuTarget { get; init; }
            public required double MissWindow { get; init; }
            public bool Judged { get; set; }
        }

        private readonly List<PendingTarget> targets;

        private OsuSessionCircleScheduler(List<PendingTarget> targets)
        {
            this.targets = targets;
        }

        public static OsuSessionCircleScheduler Create(IBeatmap beatmap, CancellationToken cancellationToken)
        {
            var list = new List<PendingTarget>();

            foreach (var hitObject in beatmap.HitObjects)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (hitObject is HitCircle && hitObject is not SliderHeadCircle)
                    tryAddTarget(hitObject, beatmap, list, cancellationToken);
            }

            return new OsuSessionCircleScheduler(list.OrderBy(t => t.HitObject.StartTime).ToList());
        }

        public IReadOnlyList<double> CollectMissDeadlines()
        {
            return targets.Select(t => t.HitObject.StartTime + t.MissWindow).Distinct().OrderBy(t => t).ToList();
        }

        public bool ProcessPress(double time, Vector2 position, Action<PendingTarget, HitResult, double, Vector2?> apply)
        {
            PendingTarget? target = FindAcceptableTarget(time, position);

            if (target == null)
                return false;

            double startOffset = time - target.HitObject.StartTime;
            HitResult result = OsuCircleJudgement.ResultForPress(target.HitObject.HitWindows!, startOffset);
            apply(target, result, time, position);
            target.Judged = true;
            return true;
        }

        /// <summary>最早的可判圆；无则 null。仅查询，不落判定。用于按键仲裁。</summary>
        public PendingTarget? FindAcceptableTarget(double time, Vector2 position)
        {
            foreach (var target in targets)
            {
                if (target.Judged)
                    continue;

                if (time < target.HitObject.StartTime - target.MissWindow)
                    continue;

                if (time > target.HitObject.StartTime + target.MissWindow)
                    break;

                if (!OsuCircleJudgement.IsInHitRadius(position, target.OsuTarget.StackedPosition, target.OsuTarget.Radius))
                    continue;

                if (OsuCircleJudgement.ResultForPress(target.HitObject.HitWindows!, time - target.HitObject.StartTime) == HitResult.None)
                    continue;

                return target;
            }

            return null;
        }

        public void ProcessExpiredMisses(double time, Action<PendingTarget, HitResult, double, Vector2?> apply)
        {
            foreach (var target in targets)
            {
                if (target.Judged)
                    continue;

                if (time < target.HitObject.StartTime + target.MissWindow)
                    continue;

                apply(target, HitResult.Miss, time, null);
                target.Judged = true;
            }
        }

        /// <summary>
        /// note lock 抢拍：把尚未判定的目标立刻结算为 Miss（对应 Drawable 的 <c>MissForcefully</c>，结果与过期窗一致，只有时刻提前）。
        /// </summary>
        public bool ForceMissTarget(HitObject hitObject, double judgementTime, Action<PendingTarget, HitResult, double, Vector2?> apply)
        {
            foreach (var target in targets)
            {
                if (target.Judged || !ReferenceEquals(target.HitObject, hitObject))
                    continue;

                apply(target, HitResult.Miss, judgementTime, null);
                target.Judged = true;
                return true;
            }

            return false;
        }

        public void FinalizeRemainingMisses(Action<PendingTarget, HitResult, double, Vector2?> apply)
        {
            foreach (var target in targets)
            {
                if (target.Judged)
                    continue;

                double judgementTime = target.HitObject.StartTime + target.MissWindow;
                apply(target, HitResult.Miss, judgementTime, null);
                target.Judged = true;
            }
        }

        private static void tryAddTarget(HitObject hitObject, IBeatmap beatmap, List<PendingTarget> list, CancellationToken cancellationToken)
        {
            ensureHitWindows(beatmap, hitObject);

            if (hitObject.HitWindows == null || ReferenceEquals(hitObject.HitWindows, HitWindows.Empty))
                return;

            if (hitObject.Judgement.MaxResult == HitResult.IgnoreHit)
                return;

            if (hitObject is OsuHitObject osuTarget)
            {
                list.Add(new PendingTarget
                {
                    HitObject = hitObject,
                    OsuTarget = osuTarget,
                    // 对齐 DrawableHitCircle：!CanBeHit 即自动 Miss（Meh 窗），不是固定 400ms Miss 窗。
                    MissWindow = OsuCircleJudgement.AutoMissWindow(hitObject.HitWindows),
                });
            }
        }

        private static void ensureHitWindows(IBeatmap beatmap, HitObject hitObject)
        {
            if (hitObject.HitWindows != null && !ReferenceEquals(hitObject.HitWindows, HitWindows.Empty))
                return;

            if (hitObject.NestedHitObjects.Count > 0)
                return;

            hitObject.ApplyDefaults(beatmap.ControlPointInfo, beatmap.BeatmapInfo.Difficulty);
        }
    }
}

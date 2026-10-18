// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.EzOsu.ReplayJudge.Judgement;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Scoring;
using osuTK;

namespace osu.Game.Rulesets.Osu.EzOsu.Judgement
{
    /// <summary>顶层圆队列：命中、过期、抢拍结算。滑条头由滑条状态自己持有。</summary>
    internal sealed class OsuKernelCircleQueue : IOsuKernelDeadlineOwner
    {
        internal sealed class Target
        {
            public HitObject HitObject = null!;
            public OsuHitObject OsuTarget = null!;
            public double MissWindow;
            public bool Judged;
            public double JudgedTime;
        }

        private readonly List<Target> targets = new List<Target>();
        private readonly Dictionary<HitObject, Target> byObject = new Dictionary<HitObject, Target>();
        private readonly IOsuJudgementSampleSource samples;
        private readonly Action<OsuJudgementEvent> emit;
        private int cursor;

        public OsuKernelCircleQueue(IReadOnlyList<Target> targets, int deadlineOrder, IOsuJudgementSampleSource samples, Action<OsuJudgementEvent> emit)
        {
            this.targets.AddRange(targets);
            this.samples = samples;
            this.emit = emit;
            DeadlineOrder = deadlineOrder;

            foreach (var target in this.targets)
                byObject[target.HitObject] = target;
        }

        public int DeadlineOrder { get; }

        public double? NextDeadline
        {
            get
            {
                advanceCursor();
                return cursor < targets.Count ? targets[cursor].HitObject.StartTime + targets[cursor].MissWindow : null;
            }
        }

        public void SettleAt(double deadline)
        {
            advanceCursor();

            while (cursor < targets.Count)
            {
                var target = targets[cursor];
                double missTime = target.HitObject.StartTime + target.MissWindow;

                if (missTime > deadline)
                    break;

                target.Judged = true;
                target.JudgedTime = missTime;
                emit(OsuJudgementEvent.Hit(target.HitObject, HitResult.Miss, missTime));
                cursor++;
            }
        }

        public Target? FindPressCandidate(double time, Vector2 position)
        {
            advanceCursor();

            for (int i = cursor; i < targets.Count; i++)
            {
                var target = targets[i];

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

        /// <summary>该次按下在目标上的窗口结果；<see cref="HitResult.None"/> 表示尚不可判（过早/过晚）。</summary>
        public HitResult ResultAt(Target target, double time)
            => OsuCircleJudgement.ResultForPress(target.HitObject.HitWindows!, time - target.HitObject.StartTime);

        /// <summary>实机按下：按目标物件定位队列项；已判或无此目标时返回 <see langword="null"/>。</summary>
        public Target? Find(HitObject hitObject)
        {
            if (!byObject.TryGetValue(hitObject, out var target) || target.Judged)
                return null;

            return target;
        }

        public bool ProcessPress(Target target, double time, Vector2 position)
        {
            if (target.Judged)
                return false;

            HitResult result = ResultAt(target, time);

            if (result == HitResult.None)
                return false;

            target.Judged = true;
            target.JudgedTime = time;
            emit(OsuJudgementEvent.Hit(target.HitObject, result, time, position));
            return true;
        }

        public bool ForceMiss(HitObject hitObject, double judgementTime)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];

                if (target.Judged || !ReferenceEquals(target.HitObject, hitObject))
                    continue;

                target.Judged = true;
                target.JudgedTime = judgementTime;
                emit(OsuJudgementEvent.Hit(hitObject, HitResult.Miss, judgementTime));
                return true;
            }

            return false;
        }

        public void FinalizeRemaining()
        {
            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];

                if (target.Judged)
                    continue;

                target.Judged = true;
                target.JudgedTime = target.HitObject.StartTime + target.MissWindow;
                emit(OsuJudgementEvent.Hit(target.HitObject, HitResult.Miss, target.JudgedTime));
            }
        }

        /// <summary>倒带：撤销判定时刻晚于 <paramref name="time"/> 的落判。</summary>
        public void RewindTo(double time)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];

                if (target.Judged && target.JudgedTime > time)
                {
                    target.Judged = false;
                    target.JudgedTime = 0;
                    cursor = Math.Min(cursor, i);
                }
            }
        }

        private void advanceCursor()
        {
            while (cursor < targets.Count && targets[cursor].Judged)
                cursor++;
        }

        public static Target? CreateTarget(HitObject hitObject, IBeatmap beatmap, CancellationToken cancellationToken = default)
        {
            if (hitObject is not HitCircle || hitObject is SliderHeadCircle)
                return null;

            ensureHitWindows(beatmap, hitObject);

            if (hitObject.HitWindows == null || ReferenceEquals(hitObject.HitWindows, HitWindows.Empty))
                return null;

            if (hitObject.Judgement.MaxResult == HitResult.IgnoreHit)
                return null;

            if (hitObject is not OsuHitObject osuTarget)
                return null;

            return new Target
            {
                HitObject = hitObject,
                OsuTarget = osuTarget,
                MissWindow = OsuCircleJudgement.AutoMissWindow(hitObject.HitWindows),
            };
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

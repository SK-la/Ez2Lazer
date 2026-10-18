// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.EzOsu.ReplayJudge.Judgement;
using osu.Game.Rulesets.Osu.EzOsu.StableClassic;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Osu.EzOsu.Judgement
{
    /// <summary>note lock 要求立刻结算的目标。</summary>
    internal readonly struct OsuKernelMissTarget
    {
        public OsuKernelMissTarget(HitObject target, HitObject containerObject)
        {
            Target = target;
            ContainerObject = containerObject;
        }

        public HitObject Target { get; }

        public HitObject ContainerObject { get; }
    }

    /// <summary>
    /// 无头 note lock：把谱面按 Drawable 容器语义整理成 <see cref="OsuNoteLockEntry"/>，
    /// 交给 <see cref="OsuNoteLockSelector"/> 或 <see cref="StableClassicNoteLock"/> 求解。
    /// </summary>
    internal sealed class OsuKernelNoteLock
    {
        private sealed class ContainerEntry
        {
            public HitObject Object = null!;
            public HitObject ContainerObject = null!;
            public HitObject[] Required = null!;
            public double StartTime;
            public double EndTime;
            public int StackHeight;
            public bool CanBlock;
        }

        private readonly List<ContainerEntry> topLevel = new List<ContainerEntry>();
        private readonly Dictionary<HitObject, int> topLevelIndex = new Dictionary<HitObject, int>();
        private List<ContainerEntry> blockers = new List<ContainerEntry>();
        private readonly Dictionary<HitObject, double> judged = new Dictionary<HitObject, double>();
        private readonly List<HitObject> rewindScratch = new List<HitObject>();
        private readonly List<OsuNoteLockEntry> buffer = new List<OsuNoteLockEntry>();

        private readonly bool legacy;
        private readonly double hittableRange;

        public OsuKernelNoteLock(IBeatmap beatmap, bool legacy, double hittableRange, CancellationToken cancellationToken)
        {
            this.legacy = legacy;
            this.hittableRange = hittableRange;
            build(beatmap, cancellationToken);
        }

        public void MarkJudged(HitObject hitObject, double judgementTime) => judged[hitObject] = judgementTime;

        /// <summary>倒带：撤销判定时刻晚于 <paramref name="time"/> 的落判标记。</summary>
        public void RewindTo(double time)
        {
            rewindScratch.Clear();

            foreach (var entry in judged)
            {
                if (entry.Value > time)
                    rewindScratch.Add(entry.Key);
            }

            for (int i = 0; i < rewindScratch.Count; i++)
                judged.Remove(rewindScratch[i]);

            rewindScratch.Clear();
        }

        public ClickAction Check(HitObject containerObject, double targetStartTime, double time, bool targetIsNested)
        {
            if (!legacy)
                return checkTimeOrdered(targetStartTime, time);

            return checkLegacy(containerObject, targetStartTime, time, targetIsNested);
        }

        public void CollectForceMissTargets(double targetStartTime, List<OsuKernelMissTarget> result)
        {
            if (legacy)
                return;

            for (int i = 0; i < blockers.Count; i++)
            {
                var blocker = blockers[i];

                if (blocker.StartTime >= targetStartTime)
                    break;

                if (!judged.ContainsKey(blocker.Object))
                    result.Add(new OsuKernelMissTarget(blocker.Object, blocker.ContainerObject));
            }
        }

        private ClickAction checkTimeOrdered(double targetStartTime, double time)
        {
            buffer.Clear();

            for (int i = 0; i < blockers.Count; i++)
            {
                if (blockers[i].StartTime >= targetStartTime)
                    break;

                buffer.Add(toEntry(blockers[i]));
            }

            return OsuNoteLockSelector.CheckTimeOrdered(buffer, targetStartTime, time);
        }

        private ClickAction checkLegacy(HitObject containerObject, double targetStartTime, double time, bool targetIsNested)
        {
            if (!topLevelIndex.TryGetValue(containerObject, out int index))
                return ClickAction.Hit;

            ClickAction previousAction = StableClassicNoteLock.CheckPrevious(!targetIsNested && index > 0 ? toEntry(topLevel[index - 1]) : null);

            if (previousAction != ClickAction.Hit)
                return previousAction;

            buffer.Clear();

            for (int i = 0; i < index; i++)
                buffer.Add(toEntry(topLevel[i]));

            return StableClassicNoteLock.CheckOrder(buffer, targetStartTime, time, hittableRange);
        }

        private void build(IBeatmap beatmap, CancellationToken cancellationToken)
        {
            var ordered = beatmap.HitObjects.OrderBy(o => o.StartTime).ToList();

            foreach (var hitObject in ordered)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var entry = new ContainerEntry
                {
                    Object = hitObject,
                    ContainerObject = hitObject,
                    Required = collectRequired(hitObject),
                    StartTime = hitObject.StartTime,
                    EndTime = hitObject.GetEndTime(),
                    StackHeight = stackHeightOf(hitObject),
                    CanBlock = hitObject is HitCircle,
                };

                topLevelIndex[hitObject] = topLevel.Count;
                topLevel.Add(entry);

                if (entry.CanBlock)
                    blockers.Add(entry);

                foreach (var nested in hitObject.NestedHitObjects)
                {
                    if (nested is not SliderHeadCircle)
                        continue;

                    var nestedEntry = new ContainerEntry
                    {
                        Object = nested,
                        ContainerObject = hitObject,
                        Required = collectRequired(nested),
                        StartTime = nested.StartTime,
                        EndTime = nested.GetEndTime(),
                        StackHeight = stackHeightOf(nested),
                        CanBlock = true,
                    };

                    blockers.Add(nestedEntry);
                }
            }

            blockers = blockers.OrderBy(b => b.StartTime).ToList();
        }

        private static HitObject[] collectRequired(HitObject hitObject)
        {
            var list = new List<HitObject> { hitObject };

            foreach (var nested in hitObject.NestedHitObjects)
            {
                if (nested.Judgement.MaxResult != HitResult.IgnoreHit)
                    list.Add(nested);
            }

            return list.ToArray();
        }

        private static int stackHeightOf(HitObject hitObject)
            => hitObject is OsuHitObject osuHitObject ? osuHitObject.StackHeight : 0;

        private OsuNoteLockEntry toEntry(ContainerEntry entry)
            => new OsuNoteLockEntry(entry.StartTime, entry.EndTime, judged.ContainsKey(entry.Object), isAllJudged(entry), entry.StackHeight, entry.CanBlock);

        private bool isAllJudged(ContainerEntry entry)
        {
            for (int i = 0; i < entry.Required.Length; i++)
            {
                if (!judged.ContainsKey(entry.Required[i]))
                    return false;
            }

            return true;
        }
    }
}

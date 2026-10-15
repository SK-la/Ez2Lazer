// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.EzOsu.ReplayJudge.Judgement;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Scoring;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Osu.EzOsu.ReplayJudge.Session
{
    /// <summary>
    /// note lock 要求立刻结算的目标：抢拍时被跳过的旧物件（对应 Drawable <c>StartTimeOrderedHitPolicy.HandleHit</c> 里的 <c>MissForcefully</c>）。
    /// </summary>
    internal readonly struct OsuNoteLockMissTarget
    {
        public OsuNoteLockMissTarget(HitObject target, HitObject containerObject)
        {
            Target = target;
            ContainerObject = containerObject;
        }

        /// <summary>需要立刻判 Miss 的物件（圆或其嵌套滑条头）。</summary>
        public HitObject Target { get; }

        /// <summary>目标所属的顶层物件（圆为其自身，滑条头为其父滑条），用于把滑条头交给对应滑条状态结算。</summary>
        public HitObject ContainerObject { get; }
    }

    /// <summary>
    /// 无头 note lock 状态：把谱面按 Drawable 容器语义（顶层物件 + 滑条头这类可阻挡的嵌套圆）整理成
    /// <see cref="OsuNoteLockEntry"/>，交由 <see cref="OsuNoteLockSelector"/> 求解。
    /// </summary>
    /// <remarks>
    /// 判定轨为 <see cref="EzEnumJudgeMode.StableClassic"/>（或带 CL 且未关 classic note lock）时走
    /// <c>LegacyHitPolicy</c> 语义，否则走 lazer 默认 <c>StartTimeOrderedHitPolicy</c>。
    /// 「本次结果本身不可命中」由各影子状态先行判掉，这里只回答顺序问题。
    /// </remarks>
    internal sealed class OsuSessionNoteLock
    {
        private sealed class ContainerEntry
        {
            public HitObject Object = null!;

            /// <summary>本物件所属的顶层物件；顶层物件即其自身。</summary>
            public HitObject ContainerObject = null!;

            /// <summary>本物件自身 + 参与判定的嵌套物件；全部判定即 <c>AllJudged</c>。</summary>
            public HitObject[] Required = null!;

            public double StartTime;
            public double EndTime;
            public int StackHeight;
            public bool CanBlock;
        }

        private readonly List<ContainerEntry> topLevel = new List<ContainerEntry>();
        private readonly Dictionary<HitObject, int> topLevelIndex = new Dictionary<HitObject, int>();
        private List<ContainerEntry> blockers = new List<ContainerEntry>();
        private readonly HashSet<HitObject> judged = new HashSet<HitObject>();
        private readonly List<OsuNoteLockEntry> buffer = new List<OsuNoteLockEntry>();

        private readonly bool legacy;
        private readonly double hittableRange;

        private OsuSessionNoteLock(bool legacy, double hittableRange)
        {
            this.legacy = legacy;
            this.hittableRange = hittableRange;
        }

        public static OsuSessionNoteLock Create(IBeatmap beatmap, Score score, IGameplayEnvironment environment, CancellationToken cancellationToken)
        {
            bool useLegacy = environment.JudgeMode == EzEnumJudgeMode.StableClassic;

            if (!useLegacy && score.ScoreInfo.Mods.OfType<OsuModClassic>().Any(m => m.ClassicNoteLock.Value))
                useLegacy = true;

            double range = OsuHitWindows.MISS_WINDOW - (score.ScoreInfo.Mods.OfType<OsuModAutopilot>().Any() ? 200 : 0);

            var noteLock = new OsuSessionNoteLock(useLegacy, range);
            noteLock.build(beatmap, cancellationToken);
            return noteLock;
        }

        public void MarkJudged(HitObject hitObject) => judged.Add(hitObject);

        /// <summary>
        /// 本次输入在 <paramref name="time"/> 能否命中目标。
        /// </summary>
        /// <param name="containerObject">目标所在的顶层物件（圆为其自身，滑条头为其父滑条）。</param>
        /// <param name="targetStartTime">目标的起始时刻（滑条头用头的起始时刻）。</param>
        /// <param name="time">本次输入时刻。</param>
        /// <param name="targetIsNested">目标是否为嵌套物件（滑条头）；嵌套目标不做紧邻叠堆检查。</param>
        public ClickAction Check(HitObject containerObject, double targetStartTime, double time, bool targetIsNested)
        {
            if (!legacy)
                return checkTimeOrdered(targetStartTime, time);

            return checkLegacy(containerObject, targetStartTime, time, targetIsNested);
        }

        /// <summary>
        /// lazer <c>StartTimeOrderedHitPolicy.HandleHit</c> 等价：命中某一物件后，该物件起拍之前仍未判定、
        /// 且可阻挡后续输入的目标需要立刻判 Miss，而不是各自等过期窗。
        /// CL 策略改为直接挡下输入本身，没有这一步。
        /// </summary>
        /// <param name="targetStartTime">本次命中的目标起始时刻。</param>
        /// <param name="result">需要立刻结算的目标；调用方按 <see cref="OsuNoteLockMissTarget.Target"/> 类型派发。</param>
        public void CollectForceMissTargets(double targetStartTime, List<OsuNoteLockMissTarget> result)
        {
            if (legacy)
                return;

            for (int i = 0; i < blockers.Count; i++)
            {
                var blocker = blockers[i];

                if (blocker.StartTime >= targetStartTime)
                    break;

                if (!judged.Contains(blocker.Object))
                    result.Add(new OsuNoteLockMissTarget(blocker.Object, blocker.ContainerObject));
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

            // 与 LegacyHitPolicy 同序：先叠堆忽略，再顺序/可达性。
            ClickAction previousAction = OsuNoteLockSelector.CheckLegacyPrevious(!targetIsNested && index > 0 ? toEntry(topLevel[index - 1]) : null);

            if (previousAction != ClickAction.Hit)
                return previousAction;

            buffer.Clear();

            for (int i = 0; i < index; i++)
                buffer.Add(toEntry(topLevel[i]));

            return OsuNoteLockSelector.CheckLegacyOrder(buffer, targetStartTime, time, hittableRange);
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

            // 稳定排序：同刻物件保持容器序，与 AliveObjects 的 OrderBy(StartTime) 一致。
            blockers = blockers.OrderBy(b => b.StartTime).ToList();
        }

        /// <summary>
        /// 影子判定实际会落的物件集合：自身 + 嵌套中非 <c>IgnoreHit</c> 者（与 <see cref="OsuSessionSliderState"/> 口径一致）。
        /// </summary>
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

        /// <summary>叠堆高度只存在于 <see cref="OsuHitObject"/>；其它规则集物件恒为 0。</summary>
        private static int stackHeightOf(HitObject hitObject)
            => hitObject is OsuHitObject osuHitObject ? osuHitObject.StackHeight : 0;

        private OsuNoteLockEntry toEntry(ContainerEntry entry)            => new OsuNoteLockEntry(entry.StartTime, entry.EndTime, judged.Contains(entry.Object), isAllJudged(entry), entry.StackHeight, entry.CanBlock);

        private bool isAllJudged(ContainerEntry entry)
        {
            for (int i = 0; i < entry.Required.Length; i++)
            {
                if (!judged.Contains(entry.Required[i]))
                    return false;
            }

            return true;
        }
    }
}

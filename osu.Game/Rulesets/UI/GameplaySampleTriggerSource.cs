// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Containers;
using osu.Game.Audio;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.UI
{
    /// <summary>
    /// A component which can trigger the most appropriate hit sound for a given point in time, based on the state of a <see cref="HitObjectContainer"/>
    /// </summary>
    public partial class GameplaySampleTriggerSource : CompositeDrawable
    {
        /// <summary>
        /// The number of concurrent samples allowed to be played concurrently so that it feels better when spam-pressing a key.
        /// </summary>
        private const int max_concurrent_hitsounds = OsuGameBase.SAMPLE_CONCURRENCY;

        private readonly HitObjectContainer hitObjectContainer;

        private int nextHitSoundIndex;

        private readonly Container<SkinnableSound> hitSounds;

        private HitObjectLifetimeEntry? mostValidObject;

        [Resolved]
        private IGameplayClock? gameplayClock { get; set; }

        [Resolved]
        private GameplayState? gameplayState { get; set; }

        protected readonly AudioContainer AudioContainer;

        public GameplaySampleTriggerSource(HitObjectContainer hitObjectContainer)
        {
            this.hitObjectContainer = hitObjectContainer;

            InternalChild = AudioContainer = new AudioContainer
            {
                Child = hitSounds = new Container<SkinnableSound>
                {
                    Name = "concurrent sample pool",
                    ChildrenEnumerable = Enumerable.Range(0, max_concurrent_hitsounds).Select(_ => new PausableSkinnableSound
                    {
                        MinimumSampleVolume = DrawableHitObject.MINIMUM_SAMPLE_VOLUME
                    })
                }
            };
        }

        /// <summary>
        /// Play the most appropriate hit sound for the current point in time.
        /// </summary>
        public virtual void Play()
        {
            HitObject? nextObject = GetMostValidObject();

            if (nextObject == null)
                return;

            // [Ez] 手动填充而不是 Cast().ToArray()：同为一次数组分配，但不产生 Cast 迭代器与 builder 缓冲。
            // 数组仍每次新建，因为 GameplayState.ApplySamples 会保留引用并靠引用不等触发绑定变更。
            var source = nextObject.Samples;
            var samples = new ISampleInfo[source.Count];

            for (int i = 0; i < samples.Length; i++)
                samples[i] = source[i];

            PlaySamples(samples);
        }

        protected virtual void PlaySamples(ISampleInfo[] samples) => Schedule(() =>
        {
            var hitSound = GetNextSample();
            ApplySampleInfo(hitSound, samples);
            hitSound.Play();
            gameplayState?.ApplySamples(samples);
        });

        protected virtual void ApplySampleInfo(SkinnableSound hitSound, ISampleInfo[] samples)
        {
            hitSound.Samples = samples;
        }

        public void StopAllPlayback() => Schedule(() =>
        {
            foreach (var sound in hitSounds)
                sound.Stop();
        });

        protected override void Update()
        {
            base.Update();

            if (gameplayClock?.IsRewinding == true)
                mostValidObject = null;
        }

        /// <summary>
        /// 取「下一个最该发声的物件」。
        /// </summary>
        /// <remarks>
        /// [Ez] 与上游语义等价的零分配实现：上游用 LINQ（<c>Where</c>/<c>MinBy</c>/<c>OrderBy</c>/<c>SkipWhile</c>）
        /// 与递归迭代器，每次按键都会产生多次迭代器 / 排序缓冲分配，且对 LN 会枚举并按结束时间排序全部 tick。
        /// 这里改为单遍最小扫描 + 显式栈的先序深度优先，取舍规则（并列时取先枚举到的）保持不变。
        /// </remarks>
        protected HitObject? GetMostValidObject()
        {
            if (mostValidObject == null || isAlreadyHit(mostValidObject))
            {
                // We need to use lifetime entries to find the next object (we can't just use `hitObjectContainer.Objects` due to pooling - it may even be empty).
                // If required, we can make this lookup more efficient by adding support to get next-future-entry in LifetimeEntryManager.
                var candidate =
                    // Use alive entries first as an optimisation.
                    findEarliestNonJudged(hitObjectContainer.AliveEntries.Keys)
                    ?? findEarliestNonJudged(hitObjectContainer.Entries);

                // In the case there are no non-judged objects, the last hit object should be used instead.
                if (candidate == null)
                {
                    mostValidObject = findLastEntry();
                }
                else
                {
                    if (isCloseEnoughToCurrentTime(candidate.HitObject))
                    {
                        mostValidObject = candidate;
                    }
                    else
                    {
                        mostValidObject ??= findFirstEntry();
                    }
                }
            }

            if (mostValidObject == null)
                return null;

            // If the fallback has been judged then we want the sample from the object itself.
            if (isAlreadyHit(mostValidObject))
                return mostValidObject.HitObject;

            // Else we want the earliest valid nested.
            // In cases of nested objects, they will always have earlier sample data than their parent object.
            return getEarliestNestedAfter(mostValidObject.HitObject) ?? mostValidObject.HitObject;
        }

        private bool isAlreadyHit(HitObjectLifetimeEntry h) => h.AllJudged;
        private bool isCloseEnoughToCurrentTime(HitObject h) => getReferenceTime() >= h.StartTime - h.HitWindows.WindowFor(HitResult.Miss) * 2;

        private double getReferenceTime() => gameplayClock?.CurrentTime ?? Clock.CurrentTime;

        /// <summary>
        /// 取 <paramref name="entries"/> 中尚未判定、且开始时间最早的一项。
        /// </summary>
        private HitObjectLifetimeEntry? findEarliestNonJudged(IEnumerable<HitObjectLifetimeEntry> entries)
        {
            HitObjectLifetimeEntry? earliest = null;
            double earliestStartTime = double.MaxValue;

            foreach (var entry in entries)
            {
                if (isAlreadyHit(entry))
                    continue;

                double startTime = entry.HitObject.StartTime;

                if (startTime < earliestStartTime)
                {
                    earliestStartTime = startTime;
                    earliest = entry;
                }
            }

            return earliest;
        }

        private HitObjectLifetimeEntry? findFirstEntry()
        {
            foreach (var entry in hitObjectContainer.Entries)
                return entry;

            return null;
        }

        private HitObjectLifetimeEntry? findLastEntry()
        {
            HitObjectLifetimeEntry? last = null;

            foreach (var entry in hitObjectContainer.Entries)
                last = entry;

            return last;
        }

        private readonly Stack<HitObject> nestedSearchStack = new Stack<HitObject>();

        /// <summary>
        /// 取 <paramref name="hitObject"/> 的后代中，结束时间晚于参考时间且最早的一个。
        /// </summary>
        private HitObject? getEarliestNestedAfter(HitObject hitObject)
        {
            double referenceTime = getReferenceTime();
            HitObject? earliest = null;
            double earliestEndTime = double.MaxValue;

            var stack = nestedSearchStack;
            stack.Clear();
            stack.Push(hitObject);

            while (stack.Count > 0)
            {
                var current = stack.Pop();

                if (!ReferenceEquals(current, hitObject))
                {
                    double endTime = current.GetEndTime();

                    if (endTime > referenceTime && endTime < earliestEndTime)
                    {
                        earliestEndTime = endTime;
                        earliest = current;
                    }
                }

                var nested = current.NestedHitObjects;

                // 逆序入栈以保持与递归枚举一致的先序顺序（OrderBy 的稳定性依赖该顺序）。
                for (int i = nested.Count - 1; i >= 0; i--)
                    stack.Push(nested[i]);
            }

            return earliest;
        }

        protected SkinnableSound GetNextSample()
        {
            SkinnableSound hitSound = hitSounds[nextHitSoundIndex];

            // round robin over available samples to allow for concurrent playback.
            nextHitSoundIndex = (nextHitSoundIndex + 1) % max_concurrent_hitsounds;

            return hitSound;
        }
    }
}

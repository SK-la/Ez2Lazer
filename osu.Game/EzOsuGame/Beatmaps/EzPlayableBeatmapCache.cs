// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;

namespace osu.Game.EzOsuGame.Beatmaps
{
    /// <summary>
    /// Process-wide memo for converted ("playable") beatmaps.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The key is the working beatmap <em>instance</em> (held weakly) plus its content hash, the ruleset, an exact
    /// snapshot of the mods, and a binding scope. Both halves of that key earn their place:
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// The instance is what the table is weak on, so an entry dies with the working beatmap it was built from.
    /// <see cref="WorkingBeatmapCache"/> holds those weakly too, which is what keeps this bounded: a bulk scan over
    /// a whole database neither pins working beatmaps nor accumulates conversions for them.
    /// </item>
    /// <item>
    /// The content hash is in the key as well, because an invalidated working beatmap that a caller still holds a
    /// reference to keeps its instance while the file behind it changes. Without the hash such an entry stays
    /// reachable and would answer for content it was not built from; with it, the new hash simply misses.
    /// </item>
    /// </list>
    /// <para>
    /// Callers arrive on whatever thread they already use (song-select background tasks, the analysis worker, a
    /// replay session thread); there is no scheduler here. A first lookup for one key may therefore be computed
    /// twice concurrently — both results are conversions of the same input, and whichever reaches the bucket first
    /// serves everybody afterwards. Nothing is cached when the conversion throws, so cancellation and load failures
    /// are never remembered.
    /// </para>
    /// <para>
    /// Only call sites that were checked to be read-only are wired to this cache; <see cref="WorkingBeatmap"/>'s own
    /// conversion path is untouched, so live gameplay keeps converting its own instance. An instance handed out by
    /// <see cref="GetShared"/> must not be written to — see <see cref="IsSharedInstance"/>.
    /// </para>
    /// </remarks>
    public static class EzPlayableBeatmapCache
    {
        /// <summary>
        /// Binding scope of an instance that every consumer may share, and that no consumer may write to.
        /// </summary>
        public const int SHARED_SCOPE = 0;

        /// <summary>How many distinct mod sets a single working beatmap may keep converted at once.</summary>
        public const int MAX_ENTRIES_PER_WORKING_BEATMAP = 4;

        /// <summary>
        /// Secondary bound for unusually large charts: the entry just converted is always kept, older ones are
        /// dropped to get back under the budget.
        /// </summary>
        public const long MAX_HIT_OBJECTS_PER_WORKING_BEATMAP = 40_000;

        private static readonly ConditionalWeakTable<IWorkingBeatmap, Bucket> buckets = new ConditionalWeakTable<IWorkingBeatmap, Bucket>();

        /// <summary>
        /// Instances handed out by <see cref="GetShared"/>. Kept so that an in-place binding can be reported rather
        /// than silently leaking one hitmode's judgements into every other consumer.
        /// </summary>
        private static readonly ConditionalWeakTable<IBeatmap, SharedMarker> sharedInstances = new ConditionalWeakTable<IBeatmap, SharedMarker>();

        private static long last_gauge_publish;

        /// <summary>
        /// Returns the shared converted beatmap for <paramref name="working"/> under <paramref name="ruleset"/> and
        /// <paramref name="mods"/>.
        /// </summary>
        /// <param name="token">
        /// Passed straight to the conversion. <c>null</c> keeps <c>WorkingBeatmap.GetPlayableBeatmap</c>'s
        /// own bounded wait, so a caller without a token of its own does not silently lose that bound;
        /// <see cref="CancellationToken.None"/> asks for no bound at all.
        /// </param>
        /// <remarks>
        /// The result is shared with every other caller of the same key and is therefore read-only: hold it, measure
        /// it, enumerate its hit objects — but do not bind judgements or hit windows onto it, and do not rewrite its
        /// objects. A caller that needs to write asks for <see cref="GetBound"/> instead.
        /// </remarks>
        public static IBeatmap GetShared(IWorkingBeatmap working, IRulesetInfo ruleset, IReadOnlyList<Mod>? mods = null, CancellationToken? token = null)
            => get(working, ruleset, mods, SHARED_SCOPE, null, token);

        /// <summary>
        /// Returns a converted beatmap owned by the caller, converted and <paramref name="bind"/>-ed exactly once and
        /// returned to nobody else carrying a different <paramref name="bindingScope"/>.
        /// </summary>
        /// <param name="bindingScope">
        /// Identifies the environment the instance is bound for (for mania, the hitmode). Must not be
        /// <see cref="SHARED_SCOPE"/>: two different environments must never meet on one instance, because the
        /// binding is in-place and last-wins.
        /// </param>
        /// <param name="bind">
        /// Applied to the converted beatmap before it is stored or returned, i.e. before any other caller can observe
        /// it, and only on the conversion that produced it.
        /// </param>
        /// <param name="token">As for <see cref="GetShared"/>.</param>
        public static IBeatmap GetBound(IWorkingBeatmap working, IRulesetInfo ruleset, IReadOnlyList<Mod>? mods, int bindingScope, Action<IBeatmap> bind,
                                       CancellationToken? token = null)
        {
            ArgumentNullException.ThrowIfNull(bind);
            ArgumentOutOfRangeException.ThrowIfLessThan(bindingScope, 1);

            return get(working, ruleset, mods, bindingScope, bind, token);
        }

        /// <summary>
        /// Whether <paramref name="beatmap"/> was handed out by <see cref="GetShared"/> and is therefore shared and
        /// read-only.
        /// </summary>
        public static bool IsSharedInstance(IBeatmap beatmap)
            => beatmap != null && sharedInstances.TryGetValue(beatmap, out _);

        /// <summary>
        /// Drops every entry. Only for tests and explicit recomputation; correctness does not depend on it, since the
        /// key already carries the beatmap content hash.
        /// </summary>
        public static void Reset()
        {
            buckets.Clear();
            sharedInstances.Clear();
        }

        private static IBeatmap get(IWorkingBeatmap working, IRulesetInfo ruleset, IReadOnlyList<Mod>? mods, int scope, Action<IBeatmap>? bind, CancellationToken? token)
        {
            ArgumentNullException.ThrowIfNull(working);
            ArgumentNullException.ThrowIfNull(ruleset);
            ArgumentOutOfRangeException.ThrowIfNegative(scope);

            token?.ThrowIfCancellationRequested();

            // 快照同时是键的身份与转换输入：键必须等于真正参与转换的那组设置，所以 seed 在这里一次性解析并带进副本。
            // 也因此命中路径不需要再对活实例做比较——两次相同设置的快照逐项相等，即使 seed 还没写回活 bindable。
            Mod[] snapshot = EzModSignature.SnapshotForConversion(mods);
            var key = new BucketKey(working.BeatmapInfo.Hash, ruleset.ShortName, EzModSignature.Compute(snapshot), scope, snapshot);
            Bucket bucket = buckets.GetValue(working, static _ => new Bucket());

            if (bucket.TryGet(key, out IBeatmap? cached))
            {
                markIfShared(cached!, scope);
                publishGauges();
                return cached!;
            }

            // 同一 working beatmap 的所有键（不同 mods / scope）共享同一批源 HitObject：转换是就地写
            // （ApplyDefaults 重建 NestedHitObjects、后转换 mod 也会改对象）。两个线程同时转换同一张图，写入
            // 会落在同一批对象上，与后台难度计算等读者撞车。按 working beatmap 串行，避免同一批对象被并发改写。
            lock (bucket.ConversionGate)
            {
                // 等锁期间别的线程可能已经算完并入库。
                if (bucket.TryGet(key, out cached))
                {
                    markIfShared(cached!, scope);
                    publishGauges();
                    return cached!;
                }

                EzModConversionPerf.RecordCacheMiss();

                IBeatmap converted = convert(working, ruleset, snapshot, bind, token);

                markIfShared(converted, scope);
                bucket.Store(key, converted);
                publishGauges();

                return converted;
            }
        }

        private static IBeatmap convert(IWorkingBeatmap working, IRulesetInfo ruleset, Mod[] snapshot, Action<IBeatmap>? bind, CancellationToken? token)
        {
            long start = EzModConversionPerf.Enabled ? Stopwatch.GetTimestamp() : 0;
            long allocatedBefore = EzModConversionPerf.Enabled ? EzModConversionPerf.CurrentThreadAllocatedBytes : 0;

            // 没有 token 时走无 token 的重载：10s 有界等待只有上游那一份实现，这里不复制那个常量。
            IBeatmap converted = token is CancellationToken cancellation
                ? working.GetPlayableBeatmap(ruleset, snapshot, cancellation)
                : working.GetPlayableBeatmap(ruleset, snapshot);

            // 绑定必须先于产物被任何其他人看到；缓存只存已经绑好的那一份。
            bind?.Invoke(converted);

            if (EzModConversionPerf.Enabled)
            {
                EzModConversionPerf.RecordConversion(
                    Stopwatch.GetTimestamp() - start,
                    EzModConversionPerf.CurrentThreadAllocatedBytes - allocatedBefore,
                    converted.HitObjects.Count);
            }

            return converted;
        }

        private static void markIfShared(IBeatmap beatmap, int scope)
        {
            if (scope == SHARED_SCOPE)
                sharedInstances.TryAdd(beatmap, SharedMarker.Instance);
        }

        /// <summary>
        /// Publishes occupancy and the accumulated counters at most once per second.
        /// </summary>
        private static void publishGauges()
        {
            if (!EzModConversionPerf.Enabled)
                return;

            long now = Stopwatch.GetTimestamp();
            long last = Volatile.Read(ref last_gauge_publish);

            if (now - last < Stopwatch.Frequency)
                return;

            if (Interlocked.CompareExchange(ref last_gauge_publish, now, last) != last)
                return;

            int shared = 0;
            int bound = 0;
            long hitObjects = 0;

            foreach (var pair in buckets)
            {
                Bucket bucket = pair.Value;
                shared += bucket.SharedCount;
                bound += bucket.BoundCount;
                hitObjects += bucket.HitObjectCount;
            }

            EzModConversionPerf.UpdateCacheGauges(shared, bound, (int)Math.Min(hitObjects, int.MaxValue));
            EzModConversionPerf.MaybeLog();
        }

        private readonly struct BucketKey : IEquatable<BucketKey>
        {
            private readonly string? beatmapHash;
            private readonly string? rulesetShortName;
            private readonly int scope;
            private readonly Mod[] orderedMods;

            /// <remarks>
            /// Equality cannot be read off this: it is a 32-bit hash, and two different settings can collide on it
            /// (see <see cref="EzModSignature.SequenceEqual"/>). Getting that wrong here would serve one mod set's
            /// converted chart for another's, so the hash is only what <see cref="GetHashCode"/> works from.
            /// </remarks>
            private readonly int modsSignature;

            public BucketKey(string? beatmapHash, string? rulesetShortName, int modsSignature, int scope, Mod[] orderedMods)
            {
                this.beatmapHash = beatmapHash;
                this.rulesetShortName = rulesetShortName;
                this.modsSignature = modsSignature;
                this.scope = scope;
                this.orderedMods = orderedMods;
            }

            public int Scope => scope;

            public bool Equals(BucketKey other)
                => scope == other.scope
                   && modsSignature == other.modsSignature
                   && string.Equals(beatmapHash, other.beatmapHash, StringComparison.Ordinal)
                   && string.Equals(rulesetShortName, other.rulesetShortName, StringComparison.Ordinal)
                   && EzModSignature.SequenceEqual(orderedMods, other.orderedMods);

            public override bool Equals(object? obj) => obj is BucketKey other && Equals(other);

            public override int GetHashCode()
            {
                var hashCode = new HashCode();

                hashCode.Add(beatmapHash);
                hashCode.Add(rulesetShortName);
                hashCode.Add(modsSignature);
                hashCode.Add(scope);

                return hashCode.ToHashCode();
            }
        }

        private sealed class Entry
        {
            public readonly IBeatmap Beatmap;
            public long LastAccess;

            public Entry(IBeatmap beatmap, long lastAccess)
            {
                Beatmap = beatmap;
                LastAccess = lastAccess;
            }
        }

        private sealed class Bucket
        {
            /// <summary>
            /// Serialises conversions for this working beatmap. Every key of one working beatmap converts the same
            /// source hit objects, so two concurrent conversions would rebuild the same nested objects in place.
            /// </summary>
            public readonly object ConversionGate = new object();

            private readonly Dictionary<BucketKey, Entry> entries = new Dictionary<BucketKey, Entry>();
            private readonly object gate = new object();

            private long stamp;
            private int sharedCount;
            private int boundCount;
            private long hitObjectCount;

            public int SharedCount
            {
                get
                {
                    lock (gate) return sharedCount;
                }
            }

            public int BoundCount
            {
                get
                {
                    lock (gate) return boundCount;
                }
            }

            public long HitObjectCount
            {
                get
                {
                    lock (gate) return hitObjectCount;
                }
            }

            public bool TryGet(BucketKey key, out IBeatmap? beatmap)
            {
                lock (gate)
                {
                    if (entries.TryGetValue(key, out var entry))
                    {
                        entry.LastAccess = ++stamp;
                        beatmap = entry.Beatmap;
                        return true;
                    }
                }

                beatmap = null;
                return false;
            }

            public void Store(BucketKey key, IBeatmap beatmap)
            {
                lock (gate)
                {
                    // 并发首查可能各自转换了一次：先落地的那个是此后所有人的结果，后来者不再覆盖。
                    if (entries.ContainsKey(key))
                        return;

                    entries.Add(key, new Entry(beatmap, ++stamp));
                    addCounters(key, beatmap, 1);

                    while (entries.Count > MAX_ENTRIES_PER_WORKING_BEATMAP
                           || (entries.Count > 1 && hitObjectCount > MAX_HIT_OBJECTS_PER_WORKING_BEATMAP))
                    {
                        evictOldestLocked();
                    }
                }
            }

            /// <remarks>
            /// Never drops the entry just stored: a single chart larger than the whole budget still has to be
            /// cacheable, so the budget only ever turns over the older entries around it.
            /// </remarks>
            private void evictOldestLocked()
            {
                BucketKey oldestKey = default;
                Entry? oldestEntry = null;

                foreach (var pair in entries)
                {
                    if (oldestEntry == null || pair.Value.LastAccess < oldestEntry.LastAccess)
                    {
                        oldestKey = pair.Key;
                        oldestEntry = pair.Value;
                    }
                }

                if (oldestEntry == null)
                    return;

                entries.Remove(oldestKey);
                addCounters(oldestKey, oldestEntry.Beatmap, -1);
                EzModConversionPerf.RecordEviction();
            }

            private void addCounters(BucketKey key, IBeatmap beatmap, int sign)
            {
                if (key.Scope == SHARED_SCOPE)
                    sharedCount += sign;
                else
                    boundCount += sign;

                hitObjectCount += (long)beatmap.HitObjects.Count * sign;
            }
        }

        private sealed class SharedMarker
        {
            public static readonly SharedMarker Instance = new SharedMarker();

            private SharedMarker()
            {
            }
        }
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using osu.Game.EzOsuGame.Analysis;
using osu.Game.EzOsuGame.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Beatmaps
{
    public abstract partial class WorkingBeatmap
    {
        /// <summary>How many distinct mod sets a single working beatmap may keep converted at once.</summary>
        public const int MAX_SHARED_PLAYABLE_ENTRIES = 4;

        /// <summary>
        /// Secondary bound for unusually large charts: the entry just converted is always kept, older ones are
        /// dropped to get back under the budget.
        /// </summary>
        public const long MAX_SHARED_PLAYABLE_HIT_OBJECTS = 40_000;

        private readonly object playableConversionGate = new object();
        private readonly object playableCacheGate = new object();
        private readonly Dictionary<SharedPlayableKey, SharedPlayableEntry> sharedPlayableEntries = new Dictionary<SharedPlayableKey, SharedPlayableEntry>();
        private long sharedPlayableAccessStamp;

        private static readonly ConditionalWeakTable<IBeatmap, SharedPlayableMarker> cachedReadOnlyMarkers = new ConditionalWeakTable<IBeatmap, SharedPlayableMarker>();

        private static long last_gauge_publish;

        /// <summary>
        /// Whether <paramref name="beatmap"/> was returned from the shared playable cache on this working beatmap and is therefore read-only.
        /// </summary>
        public static bool IsCachedReadOnlyPlayableInstance(IBeatmap beatmap)
            => beatmap != null && cachedReadOnlyMarkers.TryGetValue(beatmap, out _);

        internal static void ClearCachedReadOnlyMarkersForTesting()
        {
            // Markers are weakly held; tests that need a clean slate should call InvalidatePlayableCache on the working instance.
        }

        /// <summary>
        /// Drops every shared playable entry on this working beatmap.
        /// </summary>
        public void InvalidatePlayableCache()
        {
            lock (playableCacheGate)
                sharedPlayableEntries.Clear();
        }

        public IBeatmap GetMutablePlayableBeatmap(IRulesetInfo ruleset, IReadOnlyList<Mod> mods = null, Action<IBeatmap>? postConvert = null)
        {
            try
            {
                using (var cancellationTokenSource = new CancellationTokenSource(10_000))
                {
                    return GetMutablePlayableBeatmap(ruleset, mods ?? Array.Empty<Mod>(),
                        Debugger.IsAttached ? CancellationToken.None : cancellationTokenSource.Token, postConvert);
                }
            }
            catch (OperationCanceledException)
            {
                throw new BeatmapLoadTimeoutException(BeatmapInfo);
            }
        }

        public virtual IBeatmap GetMutablePlayableBeatmap(IRulesetInfo ruleset, IReadOnlyList<Mod> mods, CancellationToken token, Action<IBeatmap>? postConvert = null)
        {
            Mod[] snapshot = EzModSignature.SnapshotForConversion(mods);
            token.ThrowIfCancellationRequested();

            lock (playableConversionGate)
            {
                IBeatmap converted = convertPlayableBeatmapWithPerf(ruleset, snapshot, token);
                postConvert?.Invoke(converted);
                return converted;
            }
        }

        private IBeatmap getSharedPlayableBeatmap(IRulesetInfo ruleset, IReadOnlyList<Mod> mods, CancellationToken token)
        {
            Mod[] snapshot = EzModSignature.SnapshotForConversion(mods);
            token.ThrowIfCancellationRequested();

            var key = new SharedPlayableKey(BeatmapInfo.Hash, ruleset.ShortName, EzModSignature.Compute(snapshot), snapshot);

            lock (playableCacheGate)
            {
                if (tryGetSharedEntry(key, out IBeatmap? cached))
                {
                    markAsCachedReadOnly(cached!);
                    publishGauges();
                    return cached!;
                }
            }

            lock (playableConversionGate)
            {
                lock (playableCacheGate)
                {
                    if (tryGetSharedEntry(key, out cached))
                    {
                        markAsCachedReadOnly(cached!);
                        publishGauges();
                        return cached!;
                    }
                }

                EzModConversionPerf.RecordCacheMiss();
                IBeatmap converted = convertPlayableBeatmapWithPerf(ruleset, snapshot, token);
                markAsCachedReadOnly(converted);
                storeSharedEntry(key, converted);
                publishGauges();
                return converted;
            }
        }

        private IBeatmap convertPlayableBeatmapWithPerf(IRulesetInfo ruleset, Mod[] snapshot, CancellationToken token)
        {
            long start = EzModConversionPerf.Enabled ? Stopwatch.GetTimestamp() : 0;
            long allocatedBefore = EzModConversionPerf.Enabled ? EzModConversionPerf.CurrentThreadAllocatedBytes : 0;

            IBeatmap converted = convertPlayableBeatmapCore(ruleset, snapshot, token);

            if (EzModConversionPerf.Enabled)
            {
                EzModConversionPerf.RecordConversion(
                    Stopwatch.GetTimestamp() - start,
                    EzModConversionPerf.CurrentThreadAllocatedBytes - allocatedBefore,
                    converted.HitObjects.Count);
            }

            return converted;
        }

        private bool tryGetSharedEntry(SharedPlayableKey key, out IBeatmap? beatmap)
        {
            if (sharedPlayableEntries.TryGetValue(key, out var entry))
            {
                entry.LastAccess = ++sharedPlayableAccessStamp;
                beatmap = entry.Beatmap;
                return true;
            }

            beatmap = null;
            return false;
        }

        private void storeSharedEntry(SharedPlayableKey key, IBeatmap beatmap)
        {
            if (sharedPlayableEntries.ContainsKey(key))
                return;

            sharedPlayableEntries.Add(key, new SharedPlayableEntry(beatmap, ++sharedPlayableAccessStamp));

            long hitObjectCount = 0;
            foreach (var pair in sharedPlayableEntries)
                hitObjectCount += pair.Value.Beatmap.HitObjects.Count;

            while (sharedPlayableEntries.Count > MAX_SHARED_PLAYABLE_ENTRIES
                   || (sharedPlayableEntries.Count > 1 && hitObjectCount > MAX_SHARED_PLAYABLE_HIT_OBJECTS))
            {
                evictOldestSharedEntry();
                hitObjectCount = 0;
                foreach (var pair in sharedPlayableEntries)
                    hitObjectCount += pair.Value.Beatmap.HitObjects.Count;
            }
        }

        private void evictOldestSharedEntry()
        {
            SharedPlayableKey oldestKey = default;
            SharedPlayableEntry? oldestEntry = null;

            foreach (var pair in sharedPlayableEntries)
            {
                if (oldestEntry == null || pair.Value.LastAccess < oldestEntry.LastAccess)
                {
                    oldestKey = pair.Key;
                    oldestEntry = pair.Value;
                }
            }

            if (oldestEntry == null)
                return;

            sharedPlayableEntries.Remove(oldestKey);
            EzModConversionPerf.RecordEviction();
        }

        private static void markAsCachedReadOnly(IBeatmap beatmap)
            => cachedReadOnlyMarkers.TryAdd(beatmap, SharedPlayableMarker.Instance);

        private void publishGauges()
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
            long hitObjects = 0;

            lock (playableCacheGate)
            {
                shared = sharedPlayableEntries.Count;
                foreach (var entry in sharedPlayableEntries.Values)
                    hitObjects += entry.Beatmap.HitObjects.Count;
            }

            EzModConversionPerf.UpdateCacheGauges(shared, 0, (int)Math.Min(hitObjects, int.MaxValue));
            EzModConversionPerf.MaybeLog();
        }

        private readonly struct SharedPlayableKey : IEquatable<SharedPlayableKey>
        {
            private readonly string? beatmapHash;
            private readonly string? rulesetShortName;
            private readonly int modsSignature;
            private readonly Mod[] orderedMods;

            public SharedPlayableKey(string? beatmapHash, string? rulesetShortName, int modsSignature, Mod[] orderedMods)
            {
                this.beatmapHash = beatmapHash;
                this.rulesetShortName = rulesetShortName;
                this.modsSignature = modsSignature;
                this.orderedMods = orderedMods;
            }

            public bool Equals(SharedPlayableKey other)
                => modsSignature == other.modsSignature
                   && string.Equals(beatmapHash, other.beatmapHash, StringComparison.Ordinal)
                   && string.Equals(rulesetShortName, other.rulesetShortName, StringComparison.Ordinal)
                   && EzModSignature.SequenceEqual(orderedMods, other.orderedMods);

            public override bool Equals(object? obj) => obj is SharedPlayableKey other && Equals(other);

            public override int GetHashCode()
            {
                var hashCode = new HashCode();
                hashCode.Add(beatmapHash);
                hashCode.Add(rulesetShortName);
                hashCode.Add(modsSignature);
                return hashCode.ToHashCode();
            }
        }

        private sealed class SharedPlayableEntry
        {
            public readonly IBeatmap Beatmap;
            public long LastAccess;

            public SharedPlayableEntry(IBeatmap beatmap, long lastAccess)
            {
                Beatmap = beatmap;
                LastAccess = lastAccess;
            }
        }

        private sealed class SharedPlayableMarker
        {
            public static readonly SharedPlayableMarker Instance = new SharedPlayableMarker();

            private SharedPlayableMarker()
            {
            }
        }
    }
}
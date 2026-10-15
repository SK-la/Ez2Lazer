// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.Audio;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Beatmaps
{
    /// <summary>
    /// Records the hit objects a beatmap carries before mods run, then hands the samples of the ones left without a
    /// live hit object to <see cref="IDetachedSampleHost.DetachedSamples"/> so their audio still sounds at its
    /// original time.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Mods that remove or rewrite hit objects lose the samples those objects carried. Requiring every mod to hand
    /// them over by hand is both easy to forget and impossible for mods that merely leave objects out of a rebuilt
    /// list, so the ledger diffs the object set around the mods instead.
    /// </para>
    /// <para>
    /// Samples are matched by <see cref="HitSampleInfo"/> equality, i.e. by audio identity, and are read back after
    /// the mods ran, so a mod which retargets the samples of an object it drops still gets the retargeted audio, and
    /// a sample a mod hands to a replacement object (whether the original instance or a clone) is not sounded twice.
    /// Matching is done including the time first, which is what distinguishes a hold note's tail from its
    /// identically-named head, and then ignoring the time, which is what lets a sample follow an object a mod moved.
    /// </para>
    /// </remarks>
    public class DetachedSampleLedger
    {
        private readonly List<Entry> entries = new List<Entry>();

        private DetachedSampleLedger()
        {
        }

        /// <summary>
        /// Takes a snapshot of every sample-bearing hit object and the time and position it occupies.
        /// </summary>
        /// <remarks>
        /// Must run after <see cref="HitObject.ApplyDefaults"/>, which is what materialises nested objects such as a
        /// hold note's head and tail (each carrying its own samples at its own time).
        /// </remarks>
        public static DetachedSampleLedger Capture(IBeatmap beatmap)
        {
            var ledger = new DetachedSampleLedger();
            var host = beatmap as IDetachedSampleHost;

            foreach (var hitObject in beatmap.HitObjects)
            {
                float position = host?.SamplePositionResolver(hitObject) ?? DetachedNoteSample.CENTRE;
                ledger.capture(hitObject, position);
            }

            return ledger;
        }

        /// <summary>
        /// Registers every sample of a captured object that no longer belongs to a live hit object.
        /// </summary>
        public void Settle(IBeatmap beatmap)
        {
            if (entries.Count == 0 || beatmap is not IDetachedSampleHost host)
                return;

            var byIdentity = new Dictionary<HitSampleInfo, int>();
            var byIdentityAndTime = new Dictionary<(HitSampleInfo Sample, double Time), int>();
            var collected = new List<HitSampleInfo>();

            countLive(beatmap.HitObjects, collected, byIdentity, byIdentityAndTime);

            // Pass one pairs each sample with a live occurrence of the same audio at the same time. That is what keeps
            // a hold note's tail from being matched against its head, and what hands a rebuilt object's samples over
            // to it rather than also sounding them as detached audio.
            var unmatched = new List<PendingSample>();

            foreach (var entry in entries)
            {
                collected.Clear();
                collectSamples(entry.Object, collected);

                foreach (var sample in collected)
                {
                    var key = (sample, entry.Time);

                    if (isAvailable(byIdentityAndTime, key))
                    {
                        consume(byIdentityAndTime, key);
                        consume(byIdentity, sample);
                        continue;
                    }

                    unmatched.Add(new PendingSample(sample, entry.Time, entry.Position));
                }
            }

            // Pass two ignores the time, which is what lets a sample follow an object a mod moved in time.
            foreach (var sample in unmatched)
            {
                if (isAvailable(byIdentity, sample.Sample))
                {
                    consume(byIdentity, sample.Sample);
                    continue;
                }

                host.DetachedSamples.Add(new DetachedNoteSample(sample.Time, sample.Sample, sample.Position));
            }
        }

        private void capture(HitObject hitObject, float position)
        {
            entries.Add(new Entry(hitObject, hitObject.StartTime, position));

            // Nested objects (a hold note's head and tail) sit in the same column as their parent.
            foreach (var nested in hitObject.NestedHitObjects)
                capture(nested, position);
        }

        private static void countLive(IEnumerable<HitObject> hitObjects, List<HitSampleInfo> collected,
                                      Dictionary<HitSampleInfo, int> byIdentity,
                                      Dictionary<(HitSampleInfo Sample, double Time), int> byIdentityAndTime)
        {
            foreach (var hitObject in hitObjects)
            {
                collected.Clear();
                collectSamples(hitObject, collected);

                foreach (var sample in collected)
                {
                    increment(byIdentity, sample);
                    increment(byIdentityAndTime, (sample, hitObject.StartTime));
                }

                countLive(hitObject.NestedHitObjects, collected, byIdentity, byIdentityAndTime);
            }
        }

        /// <summary>
        /// Appends the samples a hit object plays: its own <see cref="HitObject.Samples"/>, plus the auxiliary samples
        /// it does not already list.
        /// </summary>
        /// <remarks>
        /// Auxiliary samples are documented as preload-only, but keysound rulesets play them as well, and those are
        /// precisely the samples a mod must not be allowed to drop.
        /// </remarks>
        private static void collectSamples(HitObject hitObject, List<HitSampleInfo> target)
        {
            foreach (var sample in hitObject.Samples)
                target.Add(sample);

            var auxiliary = hitObject.AuxiliarySamples;

            for (int i = 0; i < auxiliary.Count; i++)
            {
                var sample = auxiliary[i];

                if (!hitObject.Samples.Contains(sample))
                    target.Add(sample);
            }
        }

        private static void increment<TKey>(Dictionary<TKey, int> counts, TKey key)
            where TKey : notnull
        {
            counts.TryGetValue(key, out int count);
            counts[key] = count + 1;
        }

        private static bool isAvailable<TKey>(Dictionary<TKey, int> counts, TKey key)
            where TKey : notnull
            => counts.TryGetValue(key, out int count) && count > 0;

        private static void consume<TKey>(Dictionary<TKey, int> counts, TKey key)
            where TKey : notnull
            => counts[key]--;

        private readonly struct Entry
        {
            public readonly HitObject Object;
            public readonly double Time;
            public readonly float Position;

            public Entry(HitObject hitObject, double time, float position)
            {
                Object = hitObject;
                Time = time;
                Position = position;
            }
        }

        private readonly struct PendingSample
        {
            public readonly HitSampleInfo Sample;
            public readonly double Time;
            public readonly float Position;

            public PendingSample(HitSampleInfo sample, double time, float position)
            {
                Sample = sample;
                Time = time;
                Position = position;
            }
        }
    }
}

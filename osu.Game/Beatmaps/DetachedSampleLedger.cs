// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.Audio;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Beatmaps
{
    /// <summary>
    /// Records the hit objects a beatmap carries before the mods that rewrite hit objects run, then registers the
    /// samples of the ones left without a live carrier in <see cref="IDetachedSampleHost.DetachedSamples"/> so their
    /// audio still sounds at its original time.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Mods that remove or rewrite hit objects lose the samples those objects carried. Requiring every mod to hand
    /// them over by hand is both easy to forget and impossible for mods that merely leave objects out of a rebuilt
    /// list, so the ledger diffs the sample set around the mods instead.
    /// </para>
    /// <para>
    /// Samples are matched by <see cref="HitSampleInfo"/> equality, i.e. by audio identity, and are read from the
    /// objects after the mods ran, so a mod which retargets the samples of an object it drops still gets the
    /// retargeted audio, and a sample a mod hands to a replacement object (the original instance or a clone alike) is
    /// not sounded twice. Matching is done including the time first, which is what distinguishes a hold note's tail
    /// from its identically-named head, and then ignoring the time, which is what lets a sample follow an object a mod
    /// moved.
    /// </para>
    /// </remarks>
    public class DetachedSampleLedger
    {
        private readonly List<Entry> entries = new List<Entry>();

        private DetachedSampleLedger()
        {
        }

        /// <summary>
        /// Takes a snapshot of every sample-bearing hit object and the position it occupies.
        /// </summary>
        /// <remarks>
        /// Must run after conversion, and before any mod that can remove or rewrite hit objects. It must not run
        /// before <see cref="HitObject.ApplyDefaults"/> for the sake of nested objects: the timeline is read off the
        /// objects themselves instead, precisely so that this can run earlier.
        /// </remarks>
        public static DetachedSampleLedger Capture(IBeatmap beatmap)
        {
            var ledger = new DetachedSampleLedger();
            var host = beatmap as IDetachedSampleHost;

            foreach (var hitObject in beatmap.HitObjects)
                ledger.entries.Add(new Entry(hitObject, host?.SamplePositionResolver(hitObject) ?? DetachedNoteSample.CENTRE));

            return ledger;
        }

        /// <summary>
        /// Registers every captured sample that no longer belongs to a live hit object.
        /// </summary>
        public void Settle(IBeatmap beatmap)
        {
            if (entries.Count == 0 || beatmap is not IDetachedSampleHost host)
                return;

            var timeline = host.SampleTimelineResolver;

            var byIdentity = new Dictionary<HitSampleInfo, int>();
            var byIdentityAndTime = new Dictionary<(HitSampleInfo Sample, double Time), int>();

            foreach (var hitObject in beatmap.HitObjects)
            {
                foreach (var (time, sample) in timeline(hitObject))
                {
                    increment(byIdentity, sample);
                    increment(byIdentityAndTime, (sample, time));
                }
            }

            // Pass one pairs each sample with a live occurrence of the same audio at the same time. That is what keeps
            // a hold note's tail from being matched against its head, and what hands a rebuilt object's samples over to
            // it rather than also sounding them as detached audio.
            var unmatched = new List<PendingSample>();

            foreach (var entry in entries)
            {
                foreach (var (time, sample) in timeline(entry.Object))
                {
                    var key = (sample, time);

                    if (isAvailable(byIdentityAndTime, key))
                    {
                        consume(byIdentityAndTime, key);
                        consume(byIdentity, sample);
                        continue;
                    }

                    unmatched.Add(new PendingSample(sample, time, entry.Position));
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

        /// <summary>
        /// The timeline for objects whose samples all sound at <see cref="HitObject.StartTime"/>.
        /// </summary>
        /// <remarks>
        /// Auxiliary samples are documented as preload-only, but keysound rulesets play them as well, and those are
        /// precisely the samples a mod must not be allowed to drop. Ones already listed in
        /// <see cref="HitObject.Samples"/> are skipped so the same audio is never counted twice for one object.
        /// </remarks>
        public static IEnumerable<(double Time, HitSampleInfo Sample)> GetDefaultTimeline(HitObject hitObject)
        {
            foreach (var sample in hitObject.Samples)
                yield return (hitObject.StartTime, sample);

            var auxiliary = hitObject.AuxiliarySamples;

            for (int i = 0; i < auxiliary.Count; i++)
            {
                var sample = auxiliary[i];

                if (!hitObject.Samples.Contains(sample))
                    yield return (hitObject.StartTime, sample);
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
            public readonly float Position;

            public Entry(HitObject hitObject, float position)
            {
                Object = hitObject;
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

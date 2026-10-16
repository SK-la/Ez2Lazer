// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Audio;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Beatmaps
{
    /// <summary>
    /// A beatmap which can host samples that hit objects removed by a mod would have played.
    /// </summary>
    /// <remarks>
    /// Implemented by <see cref="Beatmap{T}"/> rather than added to <see cref="IBeatmap"/> so that rulesets
    /// outside this repository need not implement anything.
    /// </remarks>
    public interface IDetachedSampleHost
    {
        /// <summary>
        /// Samples that hit objects removed or rewritten by a mod carried, kept so their audio still sounds at its
        /// original time.
        /// </summary>
        List<DetachedNoteSample> DetachedSamples { get; }

        /// <summary>
        /// Maps a hit object to the 0..1 playfield position its samples should be panned to.
        /// </summary>
        Func<HitObject, float> SamplePositionResolver { get; set; }

        /// <summary>
        /// Enumerates the samples a hit object plays, paired with the time each plays at.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is what lets the ledger see samples without depending on nested objects, which only exist once
        /// <see cref="HitObject.ApplyDefaults"/> has run, and are never created on an object a mod removed before that.
        /// Rulesets whose samples are spread over time (a hold note's head and tail) override the default timeline.
        /// </para>
        /// <para>
        /// Implementations must be pure: the same object must produce the same pairs before and after the mods run,
        /// since times are read on both sides to pair samples up.
        /// </para>
        /// </remarks>
        Func<HitObject, IEnumerable<(double Time, HitSampleInfo Sample)>> SampleTimelineResolver { get; set; }

        /// <summary>
        /// Whether one of <see cref="DetachedSamples"/> counts as a hitsound rather than plain background audio.
        /// </summary>
        /// <remarks>
        /// This is what decides both how the sample is looked up and which volume chain it plays under, so a ruleset
        /// whose note audio is a keysound can keep it sounding like the note it came from.
        /// </remarks>
        Func<ISampleInfo, bool> DetachedSampleIsHitsound { get; set; }
    }
}

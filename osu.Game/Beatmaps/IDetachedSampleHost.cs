// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
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
        Func<HitObject, float> SamplePositionResolver { get; }
    }
}

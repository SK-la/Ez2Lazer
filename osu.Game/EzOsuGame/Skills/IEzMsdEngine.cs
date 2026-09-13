// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Mania difficulty rating engine. MSD is the chart-side baseline; SSR is the
    /// score-relative rating at a given Wife/accuracy goal.
    /// </summary>
    /// <remarks>
    /// Implementations are <b>not thread-safe</b>: keep one engine per thread / call site,
    /// or serialize access externally. See <see cref="EzNKeyMsdEngine"/> for the
    /// default n-key MinaCalc implementation backing 4-18K.
    /// </remarks>
    public interface IEzMsdEngine : IDisposable
    {
        /// <summary>Lowest keymode the engine rates (inclusive).</summary>
        int MinKeyCount { get; }

        /// <summary>Highest keymode the engine rates (inclusive).</summary>
        int MaxKeyCount { get; }

        /// <summary>Human-readable engine identifier (version string) for diagnostics.</summary>
        string EngineVersion { get; }

        /// <summary>True when <paramref name="keyCount"/> is inside the engine's supported keymode range.</summary>
        bool SupportsKeyCount(int keyCount);

        /// <summary>
        /// Chart difficulty (MSD) for the given rows. <paramref name="rate"/> applies a music-rate mod.
        /// Returns the zero vector when the keymode is unsupported or there are too few rows to rate.
        /// </summary>
        EzSkillsetVector CalculateMsd(ReadOnlySpan<EzCalcNote> notes, int keyCount, float rate = 1f);

        /// <summary>
        /// Score-relative skill rating (SSR) for the given rows at a Wife/accuracy goal.
        /// <paramref name="goal"/> is clamped to the engine's usable range.
        /// Returns the zero vector when the keymode is unsupported or there are too few rows to rate.
        /// </summary>
        EzSkillsetVector CalculateSsr(ReadOnlySpan<EzCalcNote> notes, int keyCount, float rate, float goal);
    }
}

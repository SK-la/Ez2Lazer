// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.EzOsuGame.Skills.Mina
{
    /// <summary>
    /// Mania MinaCalc engine. <see cref="CalculateMinaSkillAxis"/> is chart baseline (Mina);
    /// <see cref="CalculatePlayerMinaSkillAxis"/> is per-play skill rating at a Wife/accuracy goal (not chart <see cref="EzOsuGame.Analysis.EzAnalysisSystem.SSR"/>).
    /// </summary>
    /// <remarks>
    /// Implementations are <b>not thread-safe</b>: keep one engine per thread / call site,
    /// or serialize access externally. See <see cref="EzMinaEngine"/> for the
    /// default n-key MinaCalc implementation backing 4-18K.
    /// </remarks>
    public interface IEzMinaEngine : IDisposable
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
        /// Chart difficulty (Mina) for the given rows. <paramref name="rate"/> applies a music-rate mod.
        /// Returns the zero vector when the keymode is unsupported or there are too few rows to rate.
        /// </summary>
        EzMinaSkillAxisVector CalculateMinaSkillAxis(ReadOnlySpan<EzCalcNote> notes, int keyCount, float rate = 1f);

        /// <summary>
        /// Per-play Mina skill rating vector for the given rows at a Wife/accuracy goal.
        /// <paramref name="goal"/> is clamped to <see cref="EzMinaEngine.MIN_RATING_GOAL"/> … <see cref="EzMinaEngine.MAX_RATING_GOAL"/>.
        /// Returns the zero vector when the keymode is unsupported or there are too few rows to rate.
        /// </summary>
        EzMinaSkillAxisVector CalculatePlayerMinaSkillAxis(ReadOnlySpan<EzCalcNote> notes, int keyCount, float rate, float goal);
    }
}

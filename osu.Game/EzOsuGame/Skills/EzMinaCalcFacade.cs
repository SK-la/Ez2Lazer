// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using MinaCalc;
using osu.Game.Beatmaps;

namespace osu.Game.EzOsuGame.Skills
{
    /// <summary>
    /// Thread-affine wrapper around <see cref="Calculator"/>.
    /// MinaCalc 0.4.2: last bool is MSD when false, SSR (goal-sensitive) when true.
    /// </summary>
    public sealed class EzMinaCalcFacade : IDisposable
    {
        private readonly Calculator calculator = new Calculator();
        private bool disposed;

        public static int EngineVersion => Calculator.Version;

        /// <summary>Chart difficulty (MSD). Goal is ignored by the engine in this mode.</summary>
        public EzSkillsetVector CalculateMsd(MinaCalcNote[] notes, float rate = 1f)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (notes.Length == 0)
                return default;

            // MinaCalc 0.4.2: final bool false = MSD (goal ignored), true = SSR (goal-sensitive).
            return EzSkillsetVector.FromMina(calculator.CalculateAtRate(notes, rate, 0.93f, false));
        }

        /// <summary>Score-relative skill rating (SSR) at the given Wife/accuracy goal.</summary>
        public EzSkillsetVector CalculateSsr(MinaCalcNote[] notes, float rate, float goal)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (notes.Length == 0)
                return default;

            goal = Math.Clamp(goal, 0.8f, 0.9975f);
            return EzSkillsetVector.FromMina(calculator.CalculateAtRate(notes, rate, goal, true));
        }

        public EzSkillsetVector CalculateMsd(IBeatmap beatmap, float rate = 1f)
            => CalculateMsd(EzMinaNoteConverter.Convert(beatmap), rate);

        public EzSkillsetVector CalculateSsr(IBeatmap beatmap, float rate, float goal)
            => CalculateSsr(EzMinaNoteConverter.Convert(beatmap), rate, goal);

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            calculator.Dispose();
        }
    }
}

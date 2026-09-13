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
    /// <remarks>
    /// The note-array overloads only rate 4K correctly in 0.4.2 (columns above bit3
    /// yield an all-zero "junk" vector). 6K/7K must go through
    /// <see cref="CalculateMsdFromOsuText"/> / <see cref="CalculateSsrFromOsuText"/>,
    /// which let the native parser read CircleSize. 5K and 8K+ return engine error -3
    /// on this package (newer wasm hubs support 4–18K).
    /// </remarks>
    public sealed class EzMinaCalcFacade : IDisposable
    {
        private readonly Calculator calculator = new Calculator();
        private bool disposed;

        public static int EngineVersion => Calculator.Version;

        /// <summary>Keymodes the note-array API rates non-zero on MinaCalc 0.4.2.</summary>
        public static bool SupportsNoteArrayKeyCount(int keyCount) => keyCount == 4;

        /// <summary>Keymodes <c>CalculateAtRateFromString</c> accepts on MinaCalc 0.4.2.</summary>
        public static bool SupportsOsuTextKeyCount(int keyCount) => keyCount is 4 or 6 or 7;

        /// <summary>
        /// Builds a stable pseudo filename for MinaCalc's .osu text parser.
        /// Prefer the beatmap hash over the on-disk path so parsing is not affected by directory layout.
        /// </summary>
        public static string BuildOsuFileHint(string? beatmapHash, int keyCount)
            => !string.IsNullOrWhiteSpace(beatmapHash) ? $"{beatmapHash}.osu" : $"{keyCount}k.osu";

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

        /// <summary>
        /// MSD from raw <c>.osu</c> text. Prefers this for 6K/7K (and is fine for 4K).
        /// </summary>
        public EzSkillsetVector CalculateMsdFromOsuText(string osuText, string fileHint = "chart.osu", float rate = 1f)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            ArgumentException.ThrowIfNullOrWhiteSpace(osuText);

            return EzSkillsetVector.FromMina(calculator.CalculateAtRateFromString(osuText, fileHint, rate, 0.93f, false));
        }

        /// <summary>
        /// SSR from raw <c>.osu</c> text. Prefers this for 6K/7K (and is fine for 4K).
        /// </summary>
        public EzSkillsetVector CalculateSsrFromOsuText(string osuText, string fileHint, float rate, float goal)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            ArgumentException.ThrowIfNullOrWhiteSpace(osuText);

            goal = Math.Clamp(goal, 0.8f, 0.9975f);
            return EzSkillsetVector.FromMina(calculator.CalculateAtRateFromString(osuText, fileHint, rate, goal, true));
        }

        /// <summary>
        /// MSD from a playable beatmap via the note-array API (4K only on 0.4.2).
        /// Prefer <see cref="CalculateMsdFromOsuText"/> when the chart is 6K/7K.
        /// </summary>
        public EzSkillsetVector CalculateMsd(IBeatmap beatmap, float rate = 1f)
        {
            int keys = EzMinaNoteConverter.ResolveKeyCount(beatmap);
            if (!SupportsNoteArrayKeyCount(keys))
                return default;

            return CalculateMsd(EzMinaNoteConverter.Convert(beatmap), rate);
        }

        /// <summary>
        /// SSR from a playable beatmap via the note-array API (4K only on 0.4.2).
        /// Prefer <see cref="CalculateSsrFromOsuText"/> when the chart is 6K/7K.
        /// </summary>
        public EzSkillsetVector CalculateSsr(IBeatmap beatmap, float rate, float goal)
        {
            int keys = EzMinaNoteConverter.ResolveKeyCount(beatmap);
            if (!SupportsNoteArrayKeyCount(keys))
                return default;

            return CalculateSsr(EzMinaNoteConverter.Convert(beatmap), rate, goal);
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            calculator.Dispose();
        }
    }
}

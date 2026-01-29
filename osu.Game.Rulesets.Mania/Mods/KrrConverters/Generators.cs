// Licensed under the MIT Licence.
using System;

namespace osu.Game.Rulesets.Mania.Mods.KrrConverters
{
    // Simple oscillator generator useful for introducing periodic variation to lengths/values.
    // Deterministic when provided a seed.
    public sealed class OscillatorGenerator
    {
        private readonly double frequency;
        private readonly double phase;
        private readonly double step;
        private long counter;

        public OscillatorGenerator(int seed, double frequency = 1.0, double phase = 0.0, double step = 1.0)
        {
            // frequency: cycles per step unit
            // phase: initial phase in radians
            // step: increment per Next() call (allow sub-sampling)
            this.frequency = frequency;
            this.phase = phase;
            this.step = step;
            counter = seed;
        }

        // Returns value in range [-1, 1]
        public double NextSigned()
        {
            double t = counter * step;
            counter++;
            return Math.Sin(2.0 * Math.PI * frequency * t + phase);
        }

        // Returns value in range [0, 1]
        public double Next()
        {
            return (NextSigned() + 1.0) * 0.5;
        }

        // Reset internal counter (for deterministic reuse)
        public void Reset(long start = 0)
        {
            counter = start;
        }
    }
}

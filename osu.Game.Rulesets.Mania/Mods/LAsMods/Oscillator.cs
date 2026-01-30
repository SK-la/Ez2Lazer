// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Rulesets.Mania.Mods.LAsMods
{
    // 简单振荡器生成器，支持多种波形。
    // 默认正弦波，确定性输出。
    public sealed class Oscillator
    {
        public enum Waveform
        {
            Sine, // 正弦波
            Square, // 方波
            Triangle, // 三角波
            Sawtooth // 锯齿波
        }

        private readonly double frequency;
        private readonly double phase;
        private readonly double step;
        private readonly Waveform waveform;
        private long counter;

        public Oscillator(int seed, double frequency = 1.0, double phase = 0.0, double step = 1.0, Waveform waveform = Waveform.Sine)
        {
            // frequency: cycles per step unit
            // phase: initial phase in radians
            // step: increment per Next() call (allow sub-sampling)
            this.frequency = frequency;
            this.phase = phase;
            this.step = step;
            this.waveform = waveform;
            counter = seed;
        }

        // 返回值范围 [-1, 1]
        public double NextSigned()
        {
            double t = counter * step;
            counter++;

            double sine = Math.Sin(2.0 * Math.PI * frequency * t + phase);

            switch (waveform)
            {
                case Waveform.Sine:
                    return sine;

                case Waveform.Square:
                    return sine >= 0 ? 1.0 : -1.0;

                case Waveform.Triangle:
                    return 2.0 / Math.PI * Math.Asin(sine);

                case Waveform.Sawtooth:
                    double frac = (t * frequency + phase / (2.0 * Math.PI)) % 1.0;
                    return 2.0 * frac - 1.0;

                default:
                    return sine;
            }
        }

        // 返回值范围 [0, 1]
        public double Next()
        {
            return (NextSigned() + 1.0) * 0.5;
        }

        // 重置内部计数器（保证可复现）
        public void Reset(long start = 0)
        {
            counter = start;
        }
    }
}

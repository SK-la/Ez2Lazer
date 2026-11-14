// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osu.Game.Graphics;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning
{
    public enum ManiaNoteRecolourAlgorithm
    {
        Direct = 0,
        Tint = 1,
        SetLum = 2,
        HslKeepL = 3,
        HspKeepP = 4,
        LumaNormMul = 5,
    }

    public enum ManiaNoteRecolourRange
    {
        All = 0,
        Gray = 1,
        KeepWhite = 2,
    }

    /// <summary>
    /// CPU 像素着色原型：保明度算法与范围蒙版。量产 shader 只收这里锁死的公式。
    /// SetLum 在 luma 为 0 或 1 时无法带色相（只有黑/白能达到这两端）。
    /// </summary>
    public static class ManiaNoteRecolour
    {
        public const float LUMA_R = 0.299f;
        public const float LUMA_G = 0.587f;
        public const float LUMA_B = 0.114f;

        /// <summary>
        /// 8-bit 通道误差小于 1。
        /// </summary>
        public const float DEFAULT_CHROMA_EPSILON = 1f / 255f;

        public const float DEFAULT_WHITE_LUMA = 0.92f;

        public const float DEFAULT_WHITE_CHROMA = 0.04f;

        public static float Luma(Colour4 colour) => luma(colour.R, colour.G, colour.B);

        public static float Chroma(Colour4 colour)
            => Math.Max(Math.Abs(colour.R - colour.G), Math.Max(Math.Abs(colour.G - colour.B), Math.Abs(colour.R - colour.B)));

        public static float PerceivedBrightness(Colour4 colour)
            => MathF.Sqrt(colour.R * colour.R * LUMA_R + colour.G * colour.G * LUMA_G + colour.B * colour.B * LUMA_B);

        public static Colour4 Apply(
            Colour4 source,
            Colour4 tint,
            ManiaNoteRecolourAlgorithm algorithm,
            ManiaNoteRecolourRange range = ManiaNoteRecolourRange.All,
            float chromaEpsilon = DEFAULT_CHROMA_EPSILON,
            float whiteLuma = DEFAULT_WHITE_LUMA,
            float whiteChroma = DEFAULT_WHITE_CHROMA)
        {
            if (source.A <= 0f)
                return source;

            Colour4 straight = unpremultiply(source);

            if (!passesRange(straight, range, chromaEpsilon, whiteLuma, whiteChroma))
                return source;

            Colour4 recoloured = applyAlgorithm(straight, tint, algorithm);
            return premultiply(recoloured);
        }

        private static bool passesRange(Colour4 straight, ManiaNoteRecolourRange range, float chromaEpsilon, float whiteLuma, float whiteChroma)
        {
            float chroma = Chroma(straight);

            switch (range)
            {
                case ManiaNoteRecolourRange.Gray:
                    return chroma <= chromaEpsilon;

                case ManiaNoteRecolourRange.KeepWhite:
                    return Luma(straight) < whiteLuma || chroma > whiteChroma;

                default:
                    return true;
            }
        }

        private static Colour4 applyAlgorithm(Colour4 straight, Colour4 tint, ManiaNoteRecolourAlgorithm algorithm)
        {
            switch (algorithm)
            {
                case ManiaNoteRecolourAlgorithm.Tint:
                    tint = ManiaNoteDisplayTint.ApplyTo(tint, ManiaNoteDisplayTint.DEFAULT_TARGET_GRAYSCALE, ManiaNoteDisplayTint.DEFAULT_COLOUR_ALPHA);
                    return multiply(straight, tint);

                case ManiaNoteRecolourAlgorithm.SetLum:
                    return setLum(tint, Luma(straight), straight.A);

                case ManiaNoteRecolourAlgorithm.HslKeepL:
                    return hslKeepL(straight, tint);

                case ManiaNoteRecolourAlgorithm.HspKeepP:
                    return hspKeepP(straight, tint);

                case ManiaNoteRecolourAlgorithm.LumaNormMul:
                    return lumaNormMul(straight, tint);

                default:
                    return multiply(straight, tint);
            }
        }

        private static Colour4 multiply(Colour4 source, Colour4 tint)
            => new Colour4(source.R * tint.R, source.G * tint.G, source.B * tint.B, source.A);

        private static Colour4 lumaNormMul(Colour4 source, Colour4 tint)
        {
            float tintLuma = Math.Max(Luma(tint), 1e-6f);
            float scale = Luma(source) / tintLuma;
            return clampRgb(tint.R * scale, tint.G * scale, tint.B * scale, source.A);
        }

        private static Colour4 hslKeepL(Colour4 source, Colour4 tint)
        {
            var sourceHsl = source.ToHSL();
            var tintHsl = tint.ToHSL();
            return Colour4.FromHSL(tintHsl.X, tintHsl.Y, sourceHsl.Z, source.A);
        }

        private static Colour4 hspKeepP(Colour4 source, Colour4 tint)
        {
            var sourceHsp = new HSPAColour((Color4)source);
            var tintHsp = new HSPAColour((Color4)tint);
            var rgb = new HSPAColour(tintHsp.H, tintHsp.S, sourceHsp.P, source.A).ToColor4();
            return clampRgb(rgb.R, rgb.G, rgb.B, source.A);
        }

        private static Colour4 setLum(Colour4 tint, float targetLuma, float alpha)
        {
            float d = targetLuma - Luma(tint);
            return clipColor(tint.R + d, tint.G + d, tint.B + d, alpha);
        }

        private static Colour4 clipColor(float r, float g, float b, float alpha)
        {
            const float epsilon = 1e-6f;

            float l = luma(r, g, b);
            float n = Math.Min(r, Math.Min(g, b));
            float x = Math.Max(r, Math.Max(g, b));

            if (n < 0f)
            {
                float t = l / Math.Max(l - n, epsilon);
                r = l + (r - l) * t;
                g = l + (g - l) * t;
                b = l + (b - l) * t;
                l = luma(r, g, b);
                x = Math.Max(r, Math.Max(g, b));
            }

            if (x > 1f)
            {
                float t = (1f - l) / Math.Max(x - l, epsilon);
                r = l + (r - l) * t;
                g = l + (g - l) * t;
                b = l + (b - l) * t;
            }

            return clampRgb(r, g, b, alpha);
        }

        private static Colour4 unpremultiply(Colour4 colour)
        {
            if (colour.A <= 0f || colour.A >= 1f)
                return colour;

            return new Colour4(colour.R / colour.A, colour.G / colour.A, colour.B / colour.A, colour.A);
        }

        private static Colour4 premultiply(Colour4 straight)
        {
            if (straight.A >= 1f)
                return straight;

            return new Colour4(straight.R * straight.A, straight.G * straight.A, straight.B * straight.A, straight.A);
        }

        private static Colour4 clampRgb(float r, float g, float b, float alpha)
            => new Colour4(Math.Clamp(r, 0f, 1f), Math.Clamp(g, 0f, 1f), Math.Clamp(b, 0f, 1f), alpha);

        private static float luma(float r, float g, float b) => r * LUMA_R + g * LUMA_G + b * LUMA_B;
    }
}

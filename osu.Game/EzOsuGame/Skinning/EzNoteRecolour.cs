// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Graphics;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Skinning
{
    public enum EzNoteRecolourAlgorithm
    {
        Direct = 0,
        Tint = 1,
        SetLum = 2,
        HslKeepL = 3,
        HspKeepP = 4,
        LumaNormMul = 5,
    }

    /// <summary>
    /// CPU 像素着色：保明度算法与范围蒙版。GPU <see cref="ColorizeSprite"/> 与 PNG 导出走同一套公式。
    /// SetLum 在 luma 为 0 或 1 时无法带色相（只有黑/白能达到这两端）。
    /// </summary>
    public static class EzNoteRecolour
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

        public static EzNoteRecolourAlgorithm ToAlgorithm(EzColouringMode mode) => mode switch
        {
            EzColouringMode.Tint => EzNoteRecolourAlgorithm.Tint,
            EzColouringMode.True => EzNoteRecolourAlgorithm.SetLum,
            _ => EzNoteRecolourAlgorithm.Direct,
        };

        public static ColorizeAlgorithm ToColorizeAlgorithm(EzColouringMode mode)
            => mode == EzColouringMode.True ? ColorizeAlgorithm.SetLum : ColorizeAlgorithm.Multiply;

        public static ColorizeRange ToColorizeRange(EzColouringRange range) => range switch
        {
            EzColouringRange.Gray => ColorizeRange.Gray,
            EzColouringRange.KeepWhite => ColorizeRange.KeepWhite,
            _ => ColorizeRange.All,
        };

        /// <summary>
        /// 给 <see cref="ColorizeSprite"/> 写 Tint/算法/范围；没有着色 sprite 时退回父级乘色。
        /// 父级 <see cref="Drawable.Colour"/> 保持白色，淡出仍走父级。
        /// </summary>
        public static void ApplyToDrawable(Drawable target, Colour4 tint, EzColouringMode mode, EzColouringRange range)
        {
            ColorizeAlgorithm algorithm = isIdentityTint(tint)
                ? ColorizeAlgorithm.Multiply
                : ToColorizeAlgorithm(mode);
            ColorizeRange colorizeRange = ToColorizeRange(range);

            bool found = false;

            foreach (var sprite in target.ChildrenOfType<ColorizeSprite>())
            {
                sprite.Tint = tint;
                sprite.Algorithm = algorithm;
                sprite.Range = colorizeRange;
                found = true;
            }

            target.Colour = found ? Colour4.White : tint;
        }

        public static Colour4 Apply(
            Colour4 source,
            Colour4 tint,
            EzNoteRecolourAlgorithm algorithm,
            EzColouringRange range = EzColouringRange.All,
            float chromaEpsilon = DEFAULT_CHROMA_EPSILON,
            float whiteLuma = DEFAULT_WHITE_LUMA,
            float whiteChroma = DEFAULT_WHITE_CHROMA)
        {
            if (source.A <= 0f || isIdentityTint(tint))
                return source;

            Colour4 straight = unpremultiply(source);

            if (!passesRange(straight, range, chromaEpsilon, whiteLuma, whiteChroma))
                return source;

            Colour4 recoloured = applyAlgorithm(straight, tint, algorithm);
            return premultiply(recoloured);
        }

        /// <summary>
        /// PNG 直通色（未预乘）。导出与 GPU 反预乘路径同一套算法。
        /// </summary>
        public static Colour4 ApplyStraight(
            Colour4 source,
            Colour4 tint,
            EzNoteRecolourAlgorithm algorithm,
            EzColouringRange range = EzColouringRange.All,
            float chromaEpsilon = DEFAULT_CHROMA_EPSILON,
            float whiteLuma = DEFAULT_WHITE_LUMA,
            float whiteChroma = DEFAULT_WHITE_CHROMA)
        {
            if (source.A <= 0f || isIdentityTint(tint))
                return source;

            if (!passesRange(source, range, chromaEpsilon, whiteLuma, whiteChroma))
                return source;

            return applyAlgorithm(source, tint, algorithm);
        }

        private static bool isIdentityTint(Colour4 tint)
            => tint.R >= 1f && tint.G >= 1f && tint.B >= 1f;

        private static bool passesRange(Colour4 straight, EzColouringRange range, float chromaEpsilon, float whiteLuma, float whiteChroma)
        {
            float chroma = Chroma(straight);

            switch (range)
            {
                case EzColouringRange.Gray:
                    return chroma <= chromaEpsilon;

                case EzColouringRange.KeepWhite:
                    return Luma(straight) < whiteLuma || chroma > whiteChroma;

                default:
                    return true;
            }
        }

        private static Colour4 applyAlgorithm(Colour4 straight, Colour4 tint, EzNoteRecolourAlgorithm algorithm)
        {
            switch (algorithm)
            {
                case EzNoteRecolourAlgorithm.Tint:
                    // 与 ManiaNoteDisplayTint.ApplyTo 同一套灰度目标混合（1.5 @ 0.8）。
                    tint = fadeTint(tint);
                    return multiply(straight, tint);

                case EzNoteRecolourAlgorithm.SetLum:
                    return setLum(tint, Luma(straight), straight.A);

                case EzNoteRecolourAlgorithm.HslKeepL:
                    return hslKeepL(straight, tint);

                case EzNoteRecolourAlgorithm.HspKeepP:
                    return hspKeepP(straight, tint);

                case EzNoteRecolourAlgorithm.LumaNormMul:
                    return lumaNormMul(straight, tint);

                default:
                    return multiply(straight, tint);
            }
        }

        private static Colour4 fadeTint(Colour4 tint)
        {
            const float target = 1.5f;
            const float alpha = 0.8f;

            return new Colour4(
                target + (tint.R - target) * alpha,
                target + (tint.G - target) * alpha,
                target + (tint.B - target) * alpha,
                tint.A);
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

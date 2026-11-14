// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Game.Rulesets.Mania.Skinning;

namespace osu.Game.Rulesets.Mania.Tests.Skinning
{
    [TestFixture]
    public class ManiaNoteRecolourTest
    {
        private static readonly Colour4 dark_grey = new Colour4(0.15f, 0.15f, 0.15f, 1f);
        private static readonly Colour4 mid_grey = new Colour4(0.5f, 0.5f, 0.5f, 1f);
        private static readonly Colour4 white = Colour4.White;
        private static readonly Colour4 near_white = new Colour4(0.98f, 0.98f, 0.99f, 1f);
        private static readonly Colour4 saturated_blue = new Colour4(0.1f, 0.2f, 0.9f, 1f);
        private static readonly Colour4 red = new Colour4(1f, 0f, 0f, 1f);
        private static readonly Colour4 premul_white_glow = new Colour4(0.4f, 0.4f, 0.4f, 0.4f);

        private static readonly ManiaNoteRecolourAlgorithm[] preserve_algorithms =
        {
            ManiaNoteRecolourAlgorithm.SetLum,
            ManiaNoteRecolourAlgorithm.HslKeepL,
            ManiaNoteRecolourAlgorithm.HspKeepP,
            ManiaNoteRecolourAlgorithm.LumaNormMul,
        };

        [Test]
        public void DirectDarkensDarkGrey()
        {
            float sourceLuma = ManiaNoteRecolour.Luma(dark_grey);
            float directLuma = ManiaNoteRecolour.Luma(ManiaNoteRecolour.Apply(dark_grey, red, ManiaNoteRecolourAlgorithm.Direct));

            Assert.That(directLuma, Is.LessThan(sourceLuma - 0.05f));
        }

        [Test]
        public void SetLumKeepsDarkGreyLumaCloserThanDirect()
        {
            float sourceLuma = ManiaNoteRecolour.Luma(dark_grey);
            float directError = Math.Abs(ManiaNoteRecolour.Luma(ManiaNoteRecolour.Apply(dark_grey, red, ManiaNoteRecolourAlgorithm.Direct)) - sourceLuma);
            float setLumError = Math.Abs(ManiaNoteRecolour.Luma(ManiaNoteRecolour.Apply(dark_grey, red, ManiaNoteRecolourAlgorithm.SetLum)) - sourceLuma);

            Assert.That(setLumError, Is.LessThan(0.02f));
            Assert.That(setLumError, Is.LessThan(directError));
        }

        [Test]
        public void SetLumDoesNotCrushWhiteHighlightUnlikeLumaNormMul()
        {
            float setLumLuma = ManiaNoteRecolour.Luma(ManiaNoteRecolour.Apply(white, red, ManiaNoteRecolourAlgorithm.SetLum));
            float lumaMulLuma = ManiaNoteRecolour.Luma(ManiaNoteRecolour.Apply(white, red, ManiaNoteRecolourAlgorithm.LumaNormMul));

            Assert.That(setLumLuma, Is.GreaterThan(0.85f));
            Assert.That(lumaMulLuma, Is.LessThan(0.4f));
        }

        [Test]
        public void HspKeepPCrushesWhiteHighlightAfterGamutClamp()
        {
            float hspLuma = ManiaNoteRecolour.Luma(ManiaNoteRecolour.Apply(white, red, ManiaNoteRecolourAlgorithm.HspKeepP));

            Assert.That(hspLuma, Is.LessThan(0.4f));
        }

        [Test]
        public void SetLumIsPreferredPreserveAlgorithmOnGreyRamp()
        {
            Colour4[] greys = { dark_grey, mid_grey, white };

            float setLumMaxError = maxLumaError(greys, ManiaNoteRecolourAlgorithm.SetLum);
            float hslMaxError = maxLumaError(greys, ManiaNoteRecolourAlgorithm.HslKeepL);
            float hspMaxError = maxLumaError(greys, ManiaNoteRecolourAlgorithm.HspKeepP);
            float lumaMulMaxError = maxLumaError(greys, ManiaNoteRecolourAlgorithm.LumaNormMul);
            float directMaxError = maxLumaError(greys, ManiaNoteRecolourAlgorithm.Direct);

            Assert.That(setLumMaxError, Is.LessThan(directMaxError));
            Assert.That(setLumMaxError, Is.LessThan(hspMaxError));
            Assert.That(setLumMaxError, Is.LessThan(lumaMulMaxError));
            Assert.That(setLumMaxError, Is.LessThanOrEqualTo(hslMaxError + 0.05f));
        }

        [Test]
        public void GrayRangeLeavesSaturatedBlueUnchanged()
        {
            foreach (var algorithm in preserve_algorithms)
            {
                Colour4 result = ManiaNoteRecolour.Apply(saturated_blue, red, algorithm, ManiaNoteRecolourRange.Gray);
                assertColourEqual(result, saturated_blue);
            }
        }

        [Test]
        public void GrayRangeRecoloursMidGreyWithSetLum()
        {
            Colour4 result = ManiaNoteRecolour.Apply(mid_grey, red, ManiaNoteRecolourAlgorithm.SetLum, ManiaNoteRecolourRange.Gray);

            Assert.That(result.R, Is.GreaterThan(result.G));
            Assert.That(Math.Abs(ManiaNoteRecolour.Luma(result) - ManiaNoteRecolour.Luma(mid_grey)), Is.LessThan(0.02f));
        }

        [Test]
        public void GrayRangeLetsDirectColourPureWhite()
        {
            Colour4 result = ManiaNoteRecolour.Apply(white, red, ManiaNoteRecolourAlgorithm.Direct, ManiaNoteRecolourRange.Gray);

            Assert.That(result.R, Is.EqualTo(1f).Within(0.001f));
            Assert.That(result.G, Is.EqualTo(0f).Within(0.001f));
            Assert.That(result.B, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void SetLumCannotHueShiftPureWhite()
        {
            Colour4 result = ManiaNoteRecolour.Apply(white, red, ManiaNoteRecolourAlgorithm.SetLum, ManiaNoteRecolourRange.Gray);

            assertColourEqual(result, white);
        }

        [Test]
        public void KeepWhiteLeavesPureWhiteAndNearWhite()
        {
            foreach (var source in new[] { white, near_white })
            {
                Colour4 result = ManiaNoteRecolour.Apply(source, red, ManiaNoteRecolourAlgorithm.SetLum, ManiaNoteRecolourRange.KeepWhite);
                assertColourEqual(result, source);
            }
        }

        [Test]
        public void KeepWhiteRecoloursSaturatedBlue()
        {
            Colour4 result = ManiaNoteRecolour.Apply(saturated_blue, red, ManiaNoteRecolourAlgorithm.SetLum, ManiaNoteRecolourRange.KeepWhite);

            Assert.That(result.R, Is.GreaterThan(result.B));
            Assert.That(Math.Abs(ManiaNoteRecolour.Luma(result) - ManiaNoteRecolour.Luma(saturated_blue)), Is.LessThan(0.03f));
        }

        [Test]
        public void SetLumPremultipliedWhiteGlowKeepsStraightLuma()
        {
            Colour4 result = ManiaNoteRecolour.Apply(premul_white_glow, red, ManiaNoteRecolourAlgorithm.SetLum);
            Colour4 straight = unpremultiply(result);

            Assert.That(result.A, Is.EqualTo(premul_white_glow.A).Within(0.0001f));
            Assert.That(ManiaNoteRecolour.Luma(straight), Is.GreaterThan(0.85f));
        }

        [Test]
        public void DirectPremultipliedWhiteGlowLosesStraightLuma()
        {
            Colour4 result = ManiaNoteRecolour.Apply(premul_white_glow, red, ManiaNoteRecolourAlgorithm.Direct);
            Colour4 straight = unpremultiply(result);

            Assert.That(ManiaNoteRecolour.Luma(straight), Is.LessThan(0.4f));
        }

        [Test]
        public void AllRangeRecoloursMidGreyForPreserveAlgorithms()
        {
            foreach (var algorithm in preserve_algorithms)
            {
                Colour4 result = ManiaNoteRecolour.Apply(mid_grey, red, algorithm);
                Assert.That(result.R, Is.GreaterThan(result.G));
                Assert.That(result.R, Is.GreaterThan(result.B));
            }
        }

        private static float maxLumaError(Colour4[] sources, ManiaNoteRecolourAlgorithm algorithm)
        {
            float max = 0f;

            foreach (var source in sources)
            {
                float error = Math.Abs(ManiaNoteRecolour.Luma(ManiaNoteRecolour.Apply(source, red, algorithm)) - ManiaNoteRecolour.Luma(source));
                max = Math.Max(max, error);
            }

            return max;
        }

        private static Colour4 unpremultiply(Colour4 colour)
        {
            if (colour.A <= 0f || colour.A >= 1f)
                return colour;

            return new Colour4(colour.R / colour.A, colour.G / colour.A, colour.B / colour.A, colour.A);
        }

        private static void assertColourEqual(Colour4 actual, Colour4 expected)
        {
            Assert.That(actual.R, Is.EqualTo(expected.R).Within(0.001f));
            Assert.That(actual.G, Is.EqualTo(expected.G).Within(0.001f));
            Assert.That(actual.B, Is.EqualTo(expected.B).Within(0.001f));
            Assert.That(actual.A, Is.EqualTo(expected.A).Within(0.001f));
        }
    }
}

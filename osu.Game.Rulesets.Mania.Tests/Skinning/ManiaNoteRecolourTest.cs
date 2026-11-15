// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Skinning;

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

        private static readonly EzNoteRecolourAlgorithm[] preserve_algorithms =
        {
            EzNoteRecolourAlgorithm.SetLum,
            EzNoteRecolourAlgorithm.HslKeepL,
            EzNoteRecolourAlgorithm.HspKeepP,
            EzNoteRecolourAlgorithm.LumaNormMul,
        };

        [Test]
        public void DirectDarkensDarkGrey()
        {
            float sourceLuma = EzNoteRecolour.Luma(dark_grey);
            float directLuma = EzNoteRecolour.Luma(EzNoteRecolour.Apply(dark_grey, red, EzNoteRecolourAlgorithm.Direct));

            Assert.That(directLuma, Is.LessThan(sourceLuma - 0.05f));
        }

        [Test]
        public void SetLumKeepsDarkGreyLumaCloserThanDirect()
        {
            float sourceLuma = EzNoteRecolour.Luma(dark_grey);
            float directError = Math.Abs(EzNoteRecolour.Luma(EzNoteRecolour.Apply(dark_grey, red, EzNoteRecolourAlgorithm.Direct)) - sourceLuma);
            float setLumError = Math.Abs(EzNoteRecolour.Luma(EzNoteRecolour.Apply(dark_grey, red, EzNoteRecolourAlgorithm.SetLum)) - sourceLuma);

            Assert.That(setLumError, Is.LessThan(0.02f));
            Assert.That(setLumError, Is.LessThan(directError));
        }

        [Test]
        public void SetLumDoesNotCrushWhiteHighlightUnlikeLumaNormMul()
        {
            float setLumLuma = EzNoteRecolour.Luma(EzNoteRecolour.Apply(white, red, EzNoteRecolourAlgorithm.SetLum));
            float lumaMulLuma = EzNoteRecolour.Luma(EzNoteRecolour.Apply(white, red, EzNoteRecolourAlgorithm.LumaNormMul));

            Assert.That(setLumLuma, Is.GreaterThan(0.85f));
            Assert.That(lumaMulLuma, Is.LessThan(0.4f));
        }

        [Test]
        public void HspKeepPCrushesWhiteHighlightAfterGamutClamp()
        {
            float hspLuma = EzNoteRecolour.Luma(EzNoteRecolour.Apply(white, red, EzNoteRecolourAlgorithm.HspKeepP));

            Assert.That(hspLuma, Is.LessThan(0.4f));
        }

        [Test]
        public void SetLumIsPreferredPreserveAlgorithmOnGreyRamp()
        {
            Colour4[] greys = { dark_grey, mid_grey, white };

            float setLumMaxError = maxLumaError(greys, EzNoteRecolourAlgorithm.SetLum);
            float hslMaxError = maxLumaError(greys, EzNoteRecolourAlgorithm.HslKeepL);
            float hspMaxError = maxLumaError(greys, EzNoteRecolourAlgorithm.HspKeepP);
            float lumaMulMaxError = maxLumaError(greys, EzNoteRecolourAlgorithm.LumaNormMul);
            float directMaxError = maxLumaError(greys, EzNoteRecolourAlgorithm.Direct);

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
                Colour4 result = EzNoteRecolour.Apply(saturated_blue, red, algorithm, EzColouringRange.Gray);
                assertColourEqual(result, saturated_blue);
            }
        }

        [Test]
        public void GrayRangeRecoloursMidGreyWithSetLum()
        {
            Colour4 result = EzNoteRecolour.Apply(mid_grey, red, EzNoteRecolourAlgorithm.SetLum, EzColouringRange.Gray);

            Assert.That(result.R, Is.GreaterThan(result.G));
            Assert.That(Math.Abs(EzNoteRecolour.Luma(result) - EzNoteRecolour.Luma(mid_grey)), Is.LessThan(0.02f));
        }

        [Test]
        public void GrayRangeLetsDirectColourPureWhite()
        {
            Colour4 result = EzNoteRecolour.Apply(white, red, EzNoteRecolourAlgorithm.Direct, EzColouringRange.Gray);

            Assert.That(result.R, Is.EqualTo(1f).Within(0.001f));
            Assert.That(result.G, Is.EqualTo(0f).Within(0.001f));
            Assert.That(result.B, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void SetLumCannotHueShiftPureWhite()
        {
            Colour4 result = EzNoteRecolour.Apply(white, red, EzNoteRecolourAlgorithm.SetLum, EzColouringRange.Gray);

            assertColourEqual(result, white);
        }

        [Test]
        public void KeepWhiteLeavesPureWhiteAndNearWhite()
        {
            foreach (var source in new[] { white, near_white })
            {
                Colour4 result = EzNoteRecolour.Apply(source, red, EzNoteRecolourAlgorithm.SetLum, EzColouringRange.KeepWhite);
                assertColourEqual(result, source);
            }
        }

        [Test]
        public void KeepWhiteRecoloursSaturatedBlue()
        {
            Colour4 result = EzNoteRecolour.Apply(saturated_blue, red, EzNoteRecolourAlgorithm.SetLum, EzColouringRange.KeepWhite);

            Assert.That(result.R, Is.GreaterThan(result.B));
            Assert.That(Math.Abs(EzNoteRecolour.Luma(result) - EzNoteRecolour.Luma(saturated_blue)), Is.LessThan(0.03f));
        }

        [Test]
        public void SetLumPremultipliedWhiteGlowKeepsStraightLuma()
        {
            Colour4 result = EzNoteRecolour.Apply(premul_white_glow, red, EzNoteRecolourAlgorithm.SetLum);
            Colour4 straight = unpremultiply(result);

            Assert.That(result.A, Is.EqualTo(premul_white_glow.A).Within(0.0001f));
            Assert.That(EzNoteRecolour.Luma(straight), Is.GreaterThan(0.85f));
        }

        [Test]
        public void DirectPremultipliedWhiteGlowLosesStraightLuma()
        {
            Colour4 result = EzNoteRecolour.Apply(premul_white_glow, red, EzNoteRecolourAlgorithm.Direct);
            Colour4 straight = unpremultiply(result);

            Assert.That(EzNoteRecolour.Luma(straight), Is.LessThan(0.4f));
        }

        [Test]
        public void AllRangeRecoloursMidGreyForPreserveAlgorithms()
        {
            foreach (var algorithm in preserve_algorithms)
            {
                Colour4 result = EzNoteRecolour.Apply(mid_grey, red, algorithm);
                Assert.That(result.R, Is.GreaterThan(result.G));
                Assert.That(result.R, Is.GreaterThan(result.B));
            }
        }

        private static float maxLumaError(Colour4[] sources, EzNoteRecolourAlgorithm algorithm)
        {
            float max = 0f;

            foreach (var source in sources)
            {
                float error = Math.Abs(EzNoteRecolour.Luma(EzNoteRecolour.Apply(source, red, algorithm)) - EzNoteRecolour.Luma(source));
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

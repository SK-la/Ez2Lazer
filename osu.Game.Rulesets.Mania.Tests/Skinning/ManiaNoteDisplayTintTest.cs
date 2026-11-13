// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.Skinning;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Tests.Skinning
{
    [TestFixture]
    public class ManiaNoteDisplayTintTest
    {
        [Test]
        public void TestApplyToNeutralGreyBlendsTowardTarget()
        {
            var neutral = new Color4(0.5f, 0.5f, 0.5f, 1f);
            var result = ManiaNoteDisplayTint.ApplyTo(neutral, ManiaNoteDisplayTint.DEFAULT_TARGET_GRAYSCALE, ManiaNoteDisplayTint.DEFAULT_COLOUR_ALPHA);

            float target = (float)ManiaNoteDisplayTint.DEFAULT_TARGET_GRAYSCALE;
            float alpha = (float)ManiaNoteDisplayTint.DEFAULT_COLOUR_ALPHA;
            float expected = target + (0.5f - target) * alpha;

            Assert.That(result.R, Is.EqualTo(expected).Within(0.001));
            Assert.That(result.G, Is.EqualTo(expected).Within(0.001));
            Assert.That(result.B, Is.EqualTo(expected).Within(0.001));
        }

        [Test]
        public void TestApplyToPullsSaturatedChannelTowardTarget()
        {
            var red = new Color4(1f, 0f, 0f, 1f);
            var result = ManiaNoteDisplayTint.ApplyTo(red, ManiaNoteDisplayTint.DEFAULT_TARGET_GRAYSCALE, ManiaNoteDisplayTint.DEFAULT_COLOUR_ALPHA);

            float target = (float)ManiaNoteDisplayTint.DEFAULT_TARGET_GRAYSCALE;
            float alpha = (float)ManiaNoteDisplayTint.DEFAULT_COLOUR_ALPHA;
            float expectedR = target + (1f - target) * alpha;

            Assert.That(result.R, Is.EqualTo(expectedR).Within(0.001));
            Assert.That(result.G, Is.EqualTo(target + (0f - target) * alpha).Within(0.001));
            Assert.That(result.B, Is.EqualTo(target + (0f - target) * alpha).Within(0.001));
        }

        [Test]
        public void TestForColumnHubUsesApplyTo()
        {
            var columnColour = new osu.Framework.Graphics.Colour4(1f, 0f, 0f, 1f);
            var hub = ManiaNoteDisplayColour.ForColumnNoteDisplayTint(columnColour, EzColouringMode.Tint);
            var direct = ManiaNoteDisplayTint.ApplyTo(columnColour, ManiaNoteDisplayTint.DEFAULT_TARGET_GRAYSCALE, ManiaNoteDisplayTint.DEFAULT_COLOUR_ALPHA);

            Assert.That(hub.R, Is.EqualTo(direct.R).Within(0.001));
            Assert.That(hub.G, Is.EqualTo(direct.G).Within(0.001));
            Assert.That(hub.B, Is.EqualTo(direct.B).Within(0.001));
        }

        [Test]
        public void TestForColumnHubDirectKeepsSourceColour()
        {
            var columnColour = new osu.Framework.Graphics.Colour4(1f, 0f, 0f, 1f);
            var hub = ManiaNoteDisplayColour.ForColumnNoteDisplayTint(columnColour, EzColouringMode.Direct);

            Assert.That(hub, Is.EqualTo(columnColour));
        }

        [Test]
        public void TestDivisorEnabledRespectsEditorCompose()
        {
            Assert.That(ManiaDivisorNoteColouring.IsEnabled(true, false, true), Is.True);
            Assert.That(ManiaDivisorNoteColouring.IsEnabled(true, true, false), Is.False);
            Assert.That(ManiaDivisorNoteColouring.IsEnabled(false, true, true), Is.True);
            Assert.That(ManiaDivisorNoteColouring.IsEnabled(false, false, true), Is.False);
        }
    }
}

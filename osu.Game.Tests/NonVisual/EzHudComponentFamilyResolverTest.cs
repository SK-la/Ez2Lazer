// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.EzOsuGame.Edit;
using osu.Game.EzOsuGame.HUD;
using osu.Game.Screens.Play.HUD;
using osu.Game.Skinning;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class EzHudComponentFamilyResolverTest
    {
        [Test]
        public void TestRepresentativeTypes()
        {
            Assert.That(EzHudComponentFamilyResolver.Resolve(typeof(EzHUDScoreCounter)), Is.EqualTo(EzHudComponentFamily.Ez));
            Assert.That(EzHudComponentFamilyResolver.Resolve(typeof(ArgonScoreCounter)), Is.EqualTo(EzHudComponentFamily.Argon));
            Assert.That(EzHudComponentFamilyResolver.Resolve(typeof(LegacyAccuracyCounter)), Is.EqualTo(EzHudComponentFamily.Legacy));
            Assert.That(EzHudComponentFamilyResolver.Resolve(typeof(DefaultScoreCounter)), Is.EqualTo(EzHudComponentFamily.Other));
        }
    }
}
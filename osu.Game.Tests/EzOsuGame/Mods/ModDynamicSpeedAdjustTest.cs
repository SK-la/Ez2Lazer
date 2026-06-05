// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.EzOsuGame.Mods.CommunityMod;
using osu.Game.EzOsuGame.Mods.LAsMods;

namespace osu.Game.Tests.EzOsuGame.Mods
{
    [TestFixture]
    public class ModDynamicSpeedAdjustTest
    {
        [Test]
        public void TestShowSpeedLineDefaultsTrueOnDynamicSpeedMods()
        {
            Assert.That(new ModNiceBPM().ShowSpeedLine.Value, Is.True);
            Assert.That(new ModAccuracyAdaptive().ShowSpeedLine.Value, Is.True);
            Assert.That(new ModHealthAdaptive().ShowSpeedLine.Value, Is.True);
        }

        [Test]
        public void TestLinkSpeedHudDefaultsTrueOnDynamicSpeedMods()
        {
            Assert.That(new ModNiceBPM().LinkSpeedHUD.Value, Is.True);
            Assert.That(new ModAccuracyAdaptive().LinkSpeedHUD.Value, Is.True);
            Assert.That(new ModHealthAdaptive().LinkSpeedHUD.Value, Is.True);
        }
    }
}

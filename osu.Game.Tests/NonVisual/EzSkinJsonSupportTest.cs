// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.EzOsuGame.Edit;
using osu.Game.Skinning;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class EzSkinJsonSupportTest
    {
        [Test]
        public void TestProtectedSkinNotSupported()
        {
            var skin = new SkinInfo { Protected = true };
            Assert.That(EzSkinJsonSupport.IsSupported(skin), Is.False);
        }

        [Test]
        public void TestUnmanagedSkinNotSupported()
        {
            var skin = new SkinInfo { Protected = false };
            Assert.That(skin.IsManaged, Is.False);
            Assert.That(EzSkinJsonSupport.IsSupported(skin), Is.False);
        }
    }
}

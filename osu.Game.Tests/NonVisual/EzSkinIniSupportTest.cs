// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.EzOsuGame.Edit;
using osu.Game.Extensions;
using osu.Game.Skinning;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class EzSkinIniSupportTest
    {
        [Test]
        public void TestBuiltInSkinNotSupported()
        {
            var skin = new SkinInfo
            {
                Protected = true,
                InstantiationInfo = typeof(Ez2Skin).GetInvariantInstantiationInfo(),
            };

            Assert.That(EzSkinIniSupport.IsSupported(skin), Is.False);
        }

        [Test]
        public void TestLegacyImportedSkinSupported()
        {
            var skin = new SkinInfo
            {
                Protected = false,
                InstantiationInfo = typeof(LegacySkin).GetInvariantInstantiationInfo(),
            };

            Assert.That(EzSkinIniSupport.IsSupported(skin), Is.True);
        }

        [Test]
        public void TestModifiedBuiltInCopyNotSupported()
        {
            var skin = new SkinInfo
            {
                Protected = false,
                InstantiationInfo = typeof(TrianglesSkin).GetInvariantInstantiationInfo(),
            };

            Assert.That(EzSkinIniSupport.IsSupported(skin), Is.False);
        }
    }
}

// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.EzOsuGame.Edit;
using osu.Game.Skinning;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class EzSkinEditorSkinKindResolverTest
    {
        [Test]
        public void TestResolveFromSkinInfoInstantiation()
        {
            Assert.That(EzSkinEditorSkinKindResolver.Resolve(EzStyleProSkin.CreateInfo()), Is.EqualTo(EzSkinFamily.EzStylePro));
            Assert.That(EzSkinEditorSkinKindResolver.Resolve(Ez2Skin.CreateInfo()), Is.EqualTo(EzSkinFamily.Ez2));
            Assert.That(EzSkinEditorSkinKindResolver.Resolve(SbISkin.CreateInfo()), Is.EqualTo(EzSkinFamily.SbI));
            Assert.That(EzSkinEditorSkinKindResolver.Resolve(ArgonSkin.CreateInfo()), Is.EqualTo(EzSkinFamily.None));
            Assert.That(EzSkinEditorSkinKindResolver.Resolve((SkinInfo?)null), Is.EqualTo(EzSkinFamily.None));
        }

        [Test]
        public void TestRegistryHasOneProviderPerKind()
        {
            Assert.That(EzSkinEditorSkinScopedGroupRegistry.For(EzSkinFamily.EzStylePro), Has.Exactly(1).Items);
            Assert.That(EzSkinEditorSkinScopedGroupRegistry.For(EzSkinFamily.Ez2), Has.Exactly(1).Items);
            Assert.That(EzSkinEditorSkinScopedGroupRegistry.For(EzSkinFamily.SbI), Has.Exactly(1).Items);
            Assert.That(EzSkinEditorSkinScopedGroupRegistry.For(EzSkinFamily.Scripted), Has.Exactly(1).Items);
            Assert.That(EzSkinEditorSkinScopedGroupRegistry.For(EzSkinFamily.None), Is.Empty);
        }
    }
}
